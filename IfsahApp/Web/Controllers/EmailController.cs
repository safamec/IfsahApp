using System;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IfsahApp.Infrastructure.Data;
using IfsahApp.Infrastructure.Services.Email;
using IfsahApp.Hubs;
using IfsahApp.Core.Models;

namespace IfsahApp.Web.Controllers;

[Route("api/email")]
[ApiController]
public class EmailController : ControllerBase
{
    private readonly IEmailService _email;
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<EmailController> _logger;

    public EmailController(
        IEmailService email,
        ApplicationDbContext context,
        IHubContext<NotificationHub> hub,
        ILogger<EmailController> logger)
    {
        _email   = email;
        _context = context;
        _hub     = hub;
        _logger  = logger;
    }

    // ---- للاختبار (اختياري)
    // GET /api/email/send
    [HttpGet("send")]
    public async Task<IActionResult> SendTest()
    {
        await _email.SendAsync(
            "ahmed.s.alwahaibi@mem.gov.om",
            "Hello from .NET",
            "<p>This is a test email.</p>",
            isHtml: true
        );
        return Ok(new { message = "Email sent!" });
    }

    // ====== استقبال طلب الاشتراك عبر البريد (كان في DisclosureController) ======
    // POST /api/email/subscribe
    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeEmailDto dto)
    {
        // نرجّع 200 دائمًا لنمنع enumeration
        try
        {
            if (string.IsNullOrWhiteSpace(dto?.ReportNumber) || string.IsNullOrWhiteSpace(dto?.Email))
                return Ok(new { ok = true });

            // تأكد من وجود البلاغ
            var reportExists = await _context.Disclosures
                .AsNoTracking()
                .AnyAsync(d => d.DisclosureNumber == dto.ReportNumber);

            if (!reportExists)
                return Ok(new { ok = true });

            // ابحث عن المستخدم بالبريد
            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email == dto.Email);

            if (user is null)
                return Ok(new { ok = true });

            // توليد توكن وتخزين هاشه فقط
            var rawToken  = GenerateToken();
            var tokenHash = Sha256Hex(rawToken);

            var ev = new EmailVerification
            {
                UserId    = user.Id,
                TokenHash = tokenHash,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                Purpose   = $"subscribe_report:{dto.ReportNumber}"
            };

            _context.Add(ev);
            await _context.SaveChangesAsync();

            // رابط التأكيد – يوجّه الآن إلى هذا الكونترولر
            var confirmUrl = Url.Action(
                nameof(ConfirmSubscription),
                "Email",
                new { token = rawToken, report = dto.ReportNumber },
                Request.Scheme
            ) ?? "#";

            var subject = $"تأكيد تفعيل التنبيهات لتحديثات البلاغ {dto.ReportNumber}";
            var html = $@"
<p>لتفعيل التنبيهات لتحديثات البلاغ رقم <strong>{dto.ReportNumber}</strong>، يُرجى الضغط على الرابط التالي:</p>
<p><a href=""{confirmUrl}"">تأكيد التفعيل</a></p>
<p>صلاحية الرابط 24 ساعة ويُستخدم مرة واحدة.</p>";

            await _email.SendAsync(dto.Email, subject, html, isHtml: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SubscribeEmail failed for {Email} / {Report}", dto?.Email, dto?.ReportNumber);
        }

        return Ok(new { ok = true });
    }

    // ====== تأكيد الاشتراك (كان في DisclosureController) ======
    // GET /api/email/confirm?token=...&report=...
    [AllowAnonymous]
    [HttpGet("confirm")]
    public async Task<IActionResult> ConfirmSubscription(string token, string report)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(report))
            return ViewResult("~/Web/Views/Disclosure/ConfirmSubscriptionError.cshtml");

        var hash = Sha256Hex(token);

        var ev = await _context.Set<EmailVerification>()
            .FirstOrDefaultAsync(x =>
                x.TokenHash  == hash &&
                x.Purpose    == $"subscribe_report:{report}" &&
                x.ConsumedAt == null &&
                x.ExpiresAt  > DateTime.UtcNow);

        if (ev is null)
            return ViewResult("~/Web/Views/Disclosure/ConfirmSubscriptionError.cshtml");

        ev.ConsumedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        try
        {
            // إضافة إشعار للمستخدم وتبليغ عبر SignalR
            var note = new Notification
            {
                RecipientId = ev.UserId,
                EventType   = "SubscribeReport",
                Message     = $"تم تفعيل التنبيهات لتحديثات البلاغ {report}."
            };
            _context.Add(note);
            await _context.SaveChangesAsync();

            await _hub.Clients.Group($"user-{ev.UserId}").SendAsync("Notify", new
            {
                id        = note.Id,
                eventType = note.EventType,
                message   = note.Message,
                createdAt = note.CreatedAt
            });

            // (اختياري) إعلام مجموعة الإداريين
            await _hub.Clients.Group("admins").SendAsync("Notify", new
            {
                id        = note.Id,
                eventType = "SubscribeReport",
                message   = $"تم تفعيل التنبيهات لتحديثات البلاغ {report}.",
                createdAt = note.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SignalR notify failed in ConfirmSubscription for report {Report}", report);
        }

        // نعرض نفس فيو النجاح الموجودة لديك (Views/Disclosure/ConfirmSubscriptionSuccess.cshtml)
        var vr = ViewResult("~/Web/Views/Disclosure/ConfirmSubscriptionSuccess.cshtml");
        vr.ViewData["Report"] = report; // يدعم ViewBag.Report في الـ .cshtml
        return vr;
    }

    // ====== DTO & Helpers ======
    public sealed class SubscribeEmailDto
    {
        public string? ReportNumber { get; set; }
        public string? Email        { get; set; }
    }

    private static string GenerateToken(int size = 32)
    {
        var bytes = RandomNumberGenerator.GetBytes(size);
        return Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(bytes);
    }

    private static string Sha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    // Helper: نعيد ViewResult بدون وراثة من Controller العادي
    private ViewResult ViewResult(string viewName, object? model = null)
    {
        var result = new ViewResult
        {
            ViewName = viewName,
            ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(
                new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),
                ModelState)
        };
        if (model is not null) result.ViewData.Model = model;
        return result;
    }
}

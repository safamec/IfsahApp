using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using IfsahApp.Core.Models;
using IfsahApp.Core.ViewModels;
using IfsahApp.Hubs;
using IfsahApp.Infrastructure.Data;
using IfsahApp.Infrastructure.Services;
using IfsahApp.Infrastructure.Services.Email;
using IfsahApp.Utils;
using IfsahApp.Utils.Helpers;
using IfsahApp.Core.Enums; // ✅ Added to access NotificationHelper


namespace IfsahApp.Web.Controllers
{
    public class DisclosureController(
        ApplicationDbContext context,
        IWebHostEnvironment env,
        IEnumLocalizer enumLocalizer,
        IMapper mapper,
        IHubContext<NotificationHub> hub,
        IEmailService email // ✅ نضيف خدمة الإيميل
    ) : Controller
    {
        private readonly ApplicationDbContext _context = context;
        private readonly IWebHostEnvironment _env = env;
        private readonly IEnumLocalizer _enumLocalizer = enumLocalizer;
        private readonly IMapper _mapper = mapper;
        private readonly IHubContext<NotificationHub> _hub = hub;
        private readonly IEmailService _email = email; // ✅

        // DTO داخلي بسيط بدون DataAnnotations
        public class SubscribeEmailDto
        {
            public string ReportNumber { get; set; } = "";
            public string Email { get; set; } = "";
        }

        private static bool IsValidEmail(string? v)
            => !string.IsNullOrWhiteSpace(v) &&
               System.Text.RegularExpressions.Regex.IsMatch(v.Trim(), @"^[^\s@]+@[^\s@]+\.[^\s@]+$");

        [HttpGet]
        public IActionResult Create()
        {
            var culture = Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName;
            var disclosureTypes = _context.DisclosureTypes.ToList();
            string displayField = culture == "ar" ? "ArabicName" : "EnglishName";
            ViewBag.DisclosureTypes = new SelectList(disclosureTypes, "Id", displayField);
            return View(new DisclosureFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DisclosureFormViewModel model)
        {
            if (ModelState.IsValid)
            {
                var disclosure = _mapper.Map<Disclosure>(model);
                disclosure.DisclosureNumber = DisclosureNumberGeneratorHelper.Generate();
                disclosure.SubmittedById = 1;

                disclosure.SuspectedPeople ??= new List<SuspectedPerson>();
                disclosure.RelatedPeople   ??= new List<RelatedPerson>();

                if (model.SuspectedPersons != null)
                    foreach (var s in model.SuspectedPersons) disclosure.SuspectedPeople.Add(s);

                if (model.RelatedPersons != null)
                    foreach (var r in model.RelatedPersons)   disclosure.RelatedPeople.Add(r);

                if (model.Attachments != null && model.Attachments.Count > 0)
                {
                    disclosure.Attachments ??= new List<DisclosureAttachment>();
                    foreach (var file in model.Attachments)
                    {
                        var (savedFileName, error) = await FilePathHelper.SaveFileAsync(file, _env);
                        if (savedFileName == null)
                        {
                            ModelState.AddModelError("Attachments", error ?? "Unknown error while saving the file.");
                            continue;
                        }
                        var extension = Path.GetExtension(savedFileName).TrimStart('.');
                        disclosure.Attachments.Add(new DisclosureAttachment
                        {
                            FileName = savedFileName,
                            FileType = extension,
                            FileSize = file.Length
                        });
                    }
                }

                _context.Disclosures.Add(disclosure);
                await _context.SaveChangesAsync();


                // Notifications
                // -------------------------------
                await NotificationHelper.NotifyAdminsAsync(_context, _hub, disclosure, Url);

                // ===== Notifications to Admins =====
                var recipients = await _context.Users
                    .Where(u => u.IsActive && u.Role == Role.Admin)
                    .Select(u => new { u.Id, u.Email, u.ADUserName })
                    .ToListAsync();

                var notes = recipients.Select(r => new Notification
                {
                    RecipientId  = r.Id,
                    EventType    = "Disclosure",
                    Message      = $"New disclosure {disclosure.DisclosureNumber} created",
                    EmailAddress = r.Email,
                    IsRead       = false,
                    CreatedAt    = DateTime.UtcNow
                }).ToList();

                _context.Notifications.AddRange(notes);
                await _context.SaveChangesAsync();

                // SignalR pushes
                await Task.WhenAll(notes.Select(n =>
                    _hub.Clients.Group($"user-{n.RecipientId}")
                        .SendAsync("Notify", new
                        {
                            id = n.Id,
                            eventType = n.EventType,
                            message = n.Message,
                            createdAt = n.CreatedAt.ToString("u"),
                            url = Url.Action("Details", "Dashboard", new { id = disclosure.Id })
                        })
                ));

                await Task.WhenAll(notes.Select(n =>
                {
                    var r = recipients.FirstOrDefault(x => x.Id == n.RecipientId);
                    if (r == null) return Task.CompletedTask;

                    var payload = new
                    {
                        id = n.Id,
                        eventType = n.EventType,
                        message = n.Message,
                        createdAt = n.CreatedAt.ToString("u"),
                        url = Url.Action("Details", "Dashboard", new { id = disclosure.Id })
                    };

                    var tasks = new List<Task>
                    {
                        _hub.Clients.Group($"user-{r.Id}").SendAsync("Notify", payload)
                    };
                    if (!string.IsNullOrWhiteSpace(r.Email))
                        tasks.Add(_hub.Clients.Group($"user-{r.Email}").SendAsync("Notify", payload));
                    if (!string.IsNullOrWhiteSpace(r.ADUserName))
                        tasks.Add(_hub.Clients.Group($"user-{r.ADUserName}").SendAsync("Notify", payload));

                    return Task.WhenAll(tasks);
                }));

                return RedirectToAction("SubmitDisclosure", new { reportNumber = disclosure.DisclosureNumber });
            }

            // repopulate SelectList when validation fails
            {
                var culture = Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName;
                var disclosureTypes = await _context.DisclosureTypes
                    .Select(dt => new
                    {
                        dt.Id,
                        ArabicName = dt.ArabicName ?? dt.EnglishName, // safe fallback
                        EnglishName = dt.EnglishName ?? dt.ArabicName
                    })
                    .ToListAsync();

                var displayField = (culture == "ar") ? "ArabicName" : "EnglishName";
                ViewBag.DisclosureTypes = new SelectList(disclosureTypes, "Id", displayField, model.DisclosureTypeId);
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult SubmitDisclosure(string reportNumber)
        {
            ViewData["ReportNumber"] = reportNumber;
            return View();
        }

        // ================================
        // SubscribeEmail (بدون موديلات جديدة)
        // ================================
        [HttpPost]
        [Route("Disclosure/SubscribeEmail")]
        [IgnoreAntiforgeryToken] // سهّلناها للتجربة. لو تبين، بدليه بـ [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubscribeEmail([FromBody] SubscribeEmailDto dto)
        {
            // تحقق يدوي
            if (string.IsNullOrWhiteSpace(dto.ReportNumber))
                return BadRequest(new { ok = false, message = "رقم البلاغ مطلوب" });

            if (!IsValidEmail(dto.Email))
                return BadRequest(new { ok = false, message = "البريد الإلكتروني غير صالح" });

            // تأكيد وجود البلاغ عبر DisclosureNumber
            var exists = await _context.Disclosures
                .AsNoTracking()
                .AnyAsync(d => d.DisclosureNumber == dto.ReportNumber);

            if (!exists)
                return NotFound(new { ok = false, message = "رقم البلاغ غير موجود" });

            // بدون تخزين في قاعدة البيانات (حسب طلبك). فقط نرسل تأكيد اشتراك عبر الإيميل.
            try
            {
                await _email.SendAsync(
                    dto.Email,
                    "تأكيد الاشتراك لتحديثات البلاغ",
                    $"تم تفعيل الإشعارات لبلاغك رقم: <strong>{dto.ReportNumber}</strong>",
                    isHtml: true
                );
            }
            catch (Exception)
            {
                // ما نفشل الطلب لو الإيميل فشل
            }

            return Ok(new { ok = true, message = "تم التحقق وإرسال التأكيد على البريد" });
        }
    }
}

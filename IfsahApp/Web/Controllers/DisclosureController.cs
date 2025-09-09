using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

using IfsahApp.Infrastructure.Data;          // ApplicationDbContext
using IfsahApp.Core.Models;                 // Disclosure, User, Notification, DisclosureAttachment
using IfsahApp.Core.Enums;                  // Role, DisclosureStatus
using IfsahApp.Hubs;                        // NotificationHub
using IfsahApp.Utils;                       // DisclosureNumberGeneratorHelper.Generate()

namespace IfsahApp.Controllers
{
    [Authorize]
    [Route("[controller]")]
    public class DisclosureController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly IHubContext<NotificationHub> _hub;

        public DisclosureController(
            ApplicationDbContext db,
            IWebHostEnvironment env,
            IHubContext<NotificationHub> hub)
        {
            _db = db;
            _env = env;
            _hub = hub;
        }

        // ----------------- Helpers -----------------
        private async Task<int> ResolveCurrentUserIdAsync()
        {
            // 1) Claim مخصص
            var custom = User.FindFirst("UserId")?.Value;
            if (int.TryParse(custom, out var cid) && cid > 0) return cid;

            // 2) Claim قياسي
            var std = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(std, out var sid) && sid > 0) return sid;

            // 3) من ADUserName
            var raw = User?.Identity?.Name; // DOMAIN\username
            var adUser = (raw?.Split('\\').LastOrDefault() ?? "").ToLower();
            if (!string.IsNullOrWhiteSpace(adUser))
            {
                var u = await _db.Users.AsNoTracking()
                    .FirstOrDefaultAsync(x => (x.ADUserName ?? "").ToLower() == adUser);
                if (u != null) return u.Id;
            }

            return 0;
        }

        // ----------------- GET: /Disclosure/Create -----------------
        [HttpGet("Create")]
        public async Task<IActionResult> Create()
        {
            ViewBag.DisclosureTypes = await _db.DisclosureTypes
                .AsNoTracking()
                .OrderBy(t => t.EnglishName)
                .ToListAsync(); // DisclosureType موجود كما في الموديل
            return View();
        }

        // ----------------- POST: /Disclosure/Create -----------------
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DisclosureFormViewModel model)
        {
            async Task Refill()
            {
                ViewBag.DisclosureTypes = await _db.DisclosureTypes
                    .AsNoTracking()
                    .OrderBy(t => t.EnglishName)
                    .ToListAsync();
            }

            if (!ModelState.IsValid)
            {
                await Refill();
                return View(model);
            }

            var currentUserId = await ResolveCurrentUserIdAsync();
            if (currentUserId == 0)
            {
                ModelState.AddModelError(string.Empty, "Cannot resolve current user.");
                await Refill();
                return View(model);
            }

            // بناء كيان الإفصاح بما يطابق نموذجك (SubmittedAt, Status enum, IncidentEndDate ليست إجبارية)
            var disclosure = new Disclosure
            {
                DisclosureNumber  = DisclosureNumberGeneratorHelper.Generate(), // نفس المولد في Seeder
                Description       = model.Description?.Trim() ?? string.Empty,
                Location          = string.IsNullOrWhiteSpace(model.Location) ? null : model.Location.Trim(),
                DisclosureTypeId  = model.DisclosureTypeId,
                IncidentStartDate = model.IncidentStartDate!.Value, // Required حسب الموديل
                IncidentEndDate   = model.IncidentEndDate,          // Optional
                SubmittedAt       = DateTime.UtcNow,
                Status            = DisclosureStatus.New,
                SubmittedById     = currentUserId,
                IsAccuracyConfirmed = false
            };

            _db.Disclosures.Add(disclosure);
            await _db.SaveChangesAsync(); // Disclosures موجود في DbContext
            // :contentReference[oaicite:8]{index=8} :contentReference[oaicite:9]{index=9}

            // -------- حفظ المرفقات (اختياري) --------
            if (model.Attachments is { Count: > 0 })
            {
                var root = (_env.WebRootPath ?? _env.ContentRootPath);
                var dir  = Path.Combine(root, "uploads", "disclosures", disclosure.Id.ToString());
                Directory.CreateDirectory(dir);

                foreach (var file in model.Attachments.Where(f => f?.Length > 0))
                {
                    var stored = $"{Guid.NewGuid():N}_{Path.GetFileName(file.FileName)}";
                    var path = Path.Combine(dir, stored);
                    using var fs = System.IO.File.Create(path);
                    await file.CopyToAsync(fs);

                    _db.DisclosureAttachments.Add(new DisclosureAttachment
                    {
                        DisclosureId = disclosure.Id,
                        FileName = stored,                      // الاسم الفعلي للملف المخزن
                        FileType = Path.GetExtension(file.FileName)?.TrimStart('.'), // مثل: pdf, jpg
                        FileSize = file.Length,
                        UploadedAt = DateTime.UtcNow
                    });
                }

                await _db.SaveChangesAsync(); // DisclosureAttachments موجود في DbContext
                // :contentReference[oaicite:10]{index=10}
            }

            // -------- اختيار المستلمين حسب Role + التفضيلات --------
            var adminIds = await _db.Users
                .AsNoTracking()
                .Where(u => u.IsActive && u.Role == Role.Admin)   // roles enum
                .Select(u => u.Id)
                .ToListAsync();
            // :contentReference[oaicite:11]{index=11}

            // احترمي تفضيلات الإشعار بالنظام (ViaSystem + NotifyOnSubmission)
            var preferMap = await _db.UserNotificationPreferences
                .AsNoTracking()
                .Where(p => adminIds.Contains(p.UserId) && p.ViaSystem && p.NotifyOnSubmission)
                .Select(p => p.UserId)
                .ToListAsync();
            // :contentReference[oaicite:12]{index=12}

            var recipients = preferMap.Count > 0 ? preferMap : adminIds; // fallback لو ما فيه تفضيلات

            var submittedMsg = $"New disclosure {disclosure.DisclosureNumber} was submitted.";
            var now = DateTime.UtcNow;

            // -------- خزّني الإشعارات في DB --------
            foreach (var uid in recipients)
            {
                _db.Notifications.Add(new Notification
                {
                    RecipientId     = uid,
                    EventType       = "Disclosure:Submitted",
                    Message         = submittedMsg,
                    IsRead          = false,
                    CreatedAt       = now,
                
                });
            }
            await _db.SaveChangesAsync(); // Notifications موجود في DbContext
            // :contentReference[oaicite:13]{index=13}

            // -------- بثّ فوري عبر SignalR (Toast) --------
            var link = Url.Action("Index", "Notifications"); // /Notifications
            foreach (var uid in recipients)
            {
                await _hub.Clients.Group($"user-{uid}")
                    .SendAsync("Notify", new
                    {
                        eventType = "Disclosure:Submitted",
                        message   = submittedMsg,
                        createdAt = now,
                        url       = link
                    });
            }

            return RedirectToAction(nameof(Success));
        }

        // ----------------- GET: /Disclosure/Success -----------------
        [HttpGet("Success")]
        public IActionResult Success() => View();
    }

    // ViewModel متوافق مع الموديل (EndDate اختياري)
    public class DisclosureFormViewModel
    {
        public int DisclosureTypeId { get; set; }
        public string? Description { get; set; }
        public string? Location { get; set; }
        public DateTime? IncidentStartDate { get; set; } // Required في الموديل؛ تحققي ModelState
        public DateTime? IncidentEndDate { get; set; }   // Optional
        public List<IFormFile>? Attachments { get; set; }
    }
}

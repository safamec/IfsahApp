using System;
using System.IO;                               // Path / File / Directory
using System.Linq;
using System.Text;
using System.Threading;                        // Thread.CurrentThread
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Claims;                  // Claims
using System.Security.Cryptography;            // RNG / SHA256

using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;            // IWebHostEnvironment
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

using IfsahApp.Core.Enums;
using IfsahApp.Core.Models;
using IfsahApp.Core.ViewModels;
using IfsahApp.Core.ViewModels.Emails;
using IfsahApp.Hubs;
using IfsahApp.Infrastructure.Data;
using IfsahApp.Infrastructure.Services;
using IfsahApp.Infrastructure.Services.Email;
using IfsahApp.Utils;
using IfsahApp.Utils.Helpers;

namespace IfsahApp.Web.Controllers
{
    [AutoValidateAntiforgeryToken]
    public class DisclosureController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IEnumLocalizer _enumLocalizer;
        private readonly IMapper _mapper;
        private readonly IHubContext<NotificationHub> _hub;
        private readonly IEmailService _email;
        private readonly ViewRenderService _viewRender;
        private readonly ILogger<DisclosureController> _logger;

        private const string TempDataKey = "DisclosureForm";
        private const string TempUploadsFolderName = "tempUploads";

        public DisclosureController(
            ApplicationDbContext context,
            IWebHostEnvironment env,
            IEnumLocalizer enumLocalizer,
            IMapper mapper,
            IHubContext<NotificationHub> hub,
            IEmailService email,
            ViewRenderService viewRender,
            ILogger<DisclosureController> logger)
        {
            _context       = context;
            _env           = env;
            _enumLocalizer = enumLocalizer;
            _mapper        = mapper;
            _hub           = hub;
            _email         = email;
            _viewRender    = viewRender;
            _logger        = logger;
        }

        // /Disclosure/Create -> step 1
        [HttpGet]
        public IActionResult Create() => RedirectToAction(nameof(FormDetails));

        #region Multi-Step

        // Step 1 - GET
        [HttpGet]
        public async Task<IActionResult> FormDetails()
        {
            var form = GetFormFromTempData() ?? new DisclosureFormViewModel { Step = 1 };
            await LoadDisclosureTypesAsync(form.DisclosureTypeId);
            TempData.Keep(TempDataKey);
            return View(form);
        }

        // Step 1 - POST
        [HttpPost]
        public async Task<IActionResult> FormDetails(DisclosureFormViewModel model, string? submitDir)
        {
            if (!ModelState.IsValid)
            {
                await LoadDisclosureTypesAsync(model.DisclosureTypeId);
                TempData.Keep(TempDataKey);
                return View(model);
            }

            var form = GetFormFromTempData() ?? new DisclosureFormViewModel();
            form.Step              = 1;
            form.DisclosureTypeId  = model.DisclosureTypeId;
            form.Description       = model.Description;
            form.Location          = model.Location;
            form.IncidentStartDate = model.IncidentStartDate;
            form.IncidentEndDate   = model.IncidentEndDate;

            SaveFormToTempData(form);
            TempData.Keep(TempDataKey);

            return RedirectToAction(nameof(SuspectedPeople));
        }

        // Step 2 - GET
        [HttpGet]
        public IActionResult SuspectedPeople()
        {
            TempData.Keep(TempDataKey);
            var model = GetFormFromTempData() ?? new DisclosureFormViewModel();
            model.Step = 2;
            return View(model);
        }

        // Step 2 - POST
        [HttpPost]
        public IActionResult SuspectedPeople(DisclosureFormViewModel model, string? submitDir)
        {
            var form = GetFormFromTempData() ?? new DisclosureFormViewModel();
            form.Step = 2;
            form.SuspectedPersons = model.SuspectedPersons ?? new List<SuspectedPerson>();

            SaveFormToTempData(form);
            TempData.Keep(TempDataKey);

            if (string.Equals(submitDir, "back", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction(nameof(FormDetails));

            return RedirectToAction(nameof(RelatedPeople));
        }

        // Step 3 - GET
        [HttpGet]
        public IActionResult RelatedPeople()
        {
            TempData.Keep(TempDataKey);
            var model = GetFormFromTempData() ?? new DisclosureFormViewModel();
            model.Step = 3;
            return View(model);
        }

        // Step 3 - POST
        [HttpPost]
        public IActionResult RelatedPeople(DisclosureFormViewModel model, string? submitDir)
        {
            var form = GetFormFromTempData() ?? new DisclosureFormViewModel();
            form.Step = 3;
            form.RelatedPersons = model.RelatedPersons ?? new List<RelatedPerson>();

            SaveFormToTempData(form);
            TempData.Keep(TempDataKey);

            if (string.Equals(submitDir, "back", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction(nameof(SuspectedPeople));

            return RedirectToAction(nameof(Attachments));
        }

        // Step 4 - GET
        [HttpGet]
        public IActionResult Attachments()
        {
            var form = GetFormFromTempData() ?? new DisclosureFormViewModel();
            form.Step = 4;

            // hydrate from TempData so the view can render existing files
            form.SavedAttachmentPaths =
                TempDataExtensions.Get<List<string>>(TempData, "TempAttachmentPaths")
                ?? form.SavedAttachmentPaths
                ?? new List<string>();

            TempData.Keep(TempDataKey);
            TempData.Keep("TempAttachmentPaths");
            return View(form);
        }

        // Step 4 - POST
        [HttpPost]
        public async Task<IActionResult> Attachments(DisclosureFormViewModel model, [FromForm] string? submitDir)
        {
            var form = GetFormFromTempData() ?? new DisclosureFormViewModel();
            form.Step = 4;

            // current set from TempData
            var files = TempDataExtensions.Get<List<string>>(TempData, "TempAttachmentPaths") ?? new List<string>();

            // add new uploads (if any)
            if (model.Attachments != null && model.Attachments.Any())
            {
                foreach (var file in model.Attachments)
                {
                    var (savedFileName, error) = await FilePathHelper.SaveFileAsync(file, _env);
                    if (!string.IsNullOrEmpty(error))
                    {
                        ModelState.AddModelError("Attachments", error);
                        continue;
                    }
                    if (!string.IsNullOrEmpty(savedFileName))
                        files.Add(savedFileName);
                }
            }

            // persist
            TempDataExtensions.Set(TempData, "TempAttachmentPaths", files);
            form.SavedAttachmentPaths = files;

            SaveFormToTempData(form);
            TempData.Keep(TempDataKey);
            TempData.Keep("TempAttachmentPaths");

            // routes by intent
            if (string.Equals(submitDir, "upload", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction(nameof(Attachments));        // stay and show list
            if (string.Equals(submitDir, "back", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction(nameof(RelatedPeople));      // go back

            return RedirectToAction(nameof(ReviewForm));             // next
        }

        // Step 5 - GET
        [HttpGet]
        public async Task<IActionResult> ReviewForm()
        {
            var form = GetFormFromTempData();
            if (form == null) return RedirectToAction(nameof(FormDetails));

            // hydrate attachments (as before)
            form.SavedAttachmentPaths =
                TempDataExtensions.Get<List<string>>(TempData, "TempAttachmentPaths")
                ?? new List<string>();

            // load select list so the view can map id -> text
            await LoadDisclosureTypesAsync(form.DisclosureTypeId);

            form.Step = 5;
            TempData.Keep(TempDataKey);
            TempData.Keep("TempAttachmentPaths");
            return View(form);
        }

       // Step 5 - POST (submit final)
[HttpPost, ActionName("ReviewForm")]
public async Task<IActionResult> ReviewFormPost()
{
    var form = GetFormFromTempData();
    if (form == null) return RedirectToAction(nameof(FormDetails));
    if (!ModelState.IsValid) return RedirectToAction(nameof(ReviewForm));

    // 1) Map & basic fields
    var disclosure = _mapper.Map<Disclosure>(form);
    disclosure.DisclosureNumber = DisclosureNumberGeneratorHelper.Generate();
    disclosure.SubmittedById    = await CurrentDbUserIdAsync();

    // 2) Save disclosure FIRST to get a real Id
    _context.Disclosures.Add(disclosure);
    await _context.SaveChangesAsync();                // <-- disclosure.Id is now available

    // 3) Add people (if any) and save
    AddPersonsToDisclosure(disclosure, form);        // adds to navigation collections
    await _context.SaveChangesAsync();

    // 4) Add attachments from temp and bind DisclosureId
    await TryAddAttachmentsFromTempAsync(disclosure, form.SavedAttachmentPaths);
    // TryAddAttachmentsFromTempAsync should create DisclosureAttachment rows with:
    //   DisclosureId = disclosure.Id, FileName, FileType, FileSize, UploadedAt
    // and call _context.DisclosureAttachments.AddRange(...) + SaveChangesAsync() inside
    // OR you can add a SaveChangesAsync() here if that method does not save.
    // await _context.SaveChangesAsync();

    // -------------------------------
    // Notifications
    // -------------------------------

    // 1) choose recipients (Admins)
    var recipients = await _context.Users
        .Where(u => u.IsActive && u.Role == Role.Admin)
        .Select(u => new { u.Id, u.Email })
        .ToListAsync();

    // 2) create rows
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

    // 3) live push via SignalR (by numeric id group)
    await Task.WhenAll(notes.Select(n =>
        _hub.Clients.Group($"user-{n.RecipientId}")
            .SendAsync("Notify", new
            {
                id        = n.Id,
                eventType = n.EventType,
                message   = n.Message,
                createdAt = n.CreatedAt.ToString("u"),
                url       = Url.Action("Details", "Dashboard", new { id = disclosure.Id })
            })
    ));

    // also push to email and ADUserName groups
    var recipientKeys = await _context.Users
        .Where(u => recipients.Select(r => r.Id).Contains(u.Id))
        .Select(u => new { u.Id, u.Email, u.ADUserName })
        .ToListAsync();

    await Task.WhenAll(notes.Select(async n =>
    {
        var r = recipientKeys.FirstOrDefault(x => x.Id == n.RecipientId);
        if (r == null) return;

        var payload = new
        {
            id        = n.Id,
            eventType = n.EventType,
            message   = n.Message,
            createdAt = n.CreatedAt.ToString("u"),
            url       = Url.Action("Details", "Dashboard", new { id = disclosure.Id })
        };

        var tasks = new List<Task>();
        if (!string.IsNullOrWhiteSpace(r.Email))
            tasks.Add(_hub.Clients.Group($"user-{r.Email}").SendAsync("Notify", payload));
        if (!string.IsNullOrWhiteSpace(r.ADUserName))
            tasks.Add(_hub.Clients.Group($"user-{r.ADUserName}").SendAsync("Notify", payload));

        await Task.WhenAll(tasks);
    }));

    // ✅ redirect after save
    return RedirectToAction(nameof(SubmitDisclosure), new { reportNumber = disclosure.DisclosureNumber });
}


        #endregion

        #region Utilities

        private async Task LoadDisclosureTypesAsync(int? selectedId = null)
        {
            var culture = Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName;
            var disclosureTypes = await _context.DisclosureTypes
                .Select(dt => new
                {
                    dt.Id,
                    ArabicName  = dt.ArabicName  ?? dt.EnglishName,
                    EnglishName = dt.EnglishName ?? dt.ArabicName
                })
                .ToListAsync();

            var displayField = (culture == "ar") ? "ArabicName" : "EnglishName";
            ViewBag.DisclosureTypes = new SelectList(disclosureTypes, "Id", displayField, selectedId);
        }

        private DisclosureFormViewModel? GetFormFromTempData()
        {
            if (TempData.TryGetValue(TempDataKey, out object? raw) && raw is string json)
            {
                TempData.Keep(TempDataKey);
                return JsonConvert.DeserializeObject<DisclosureFormViewModel>(json);
            }
            return null;
        }

        private void SaveFormToTempData(DisclosureFormViewModel model)
        {
            TempData[TempDataKey] = JsonConvert.SerializeObject(model);
        }

        private void AddPersonsToDisclosure(Disclosure disclosure, DisclosureFormViewModel model)
        {
            if (model.SuspectedPersons != null && model.SuspectedPersons.Count > 0)
            {
                disclosure.SuspectedPeople ??= new List<SuspectedPerson>();
                foreach (var suspected in model.SuspectedPersons)
                    disclosure.SuspectedPeople.Add(suspected);
            }

            if (model.RelatedPersons != null && model.RelatedPersons.Count > 0)
            {
                disclosure.RelatedPeople ??= new List<RelatedPerson>();
                foreach (var related in model.RelatedPersons)
                    disclosure.RelatedPeople.Add(related);
            }
        }

    private async Task TryAddAttachmentsFromTempAsync(Disclosure disclosure, List<string>? tempFileNames)
{
    if (tempFileNames == null || tempFileNames.Count == 0)
        return;

    var tempFolder      = Path.Combine(_env.WebRootPath, "tempUploads");
    var permanentFolder = Path.Combine(_env.WebRootPath, "uploads");
    if (!Directory.Exists(permanentFolder))
        Directory.CreateDirectory(permanentFolder);

    var newRows = new List<DisclosureAttachment>();

            foreach (var entry in tempFileNames)
            {
                // Normalize the source path:
                // - If absolute: use as is.
                // - If relative: look in /wwwroot/tempUploads.
                string srcPath = Path.IsPathRooted(entry) ? entry : Path.Combine(tempFolder, entry);

                // If it wasn't found in temp, also try uploads (user might have stored a full uploads path)
                if (!System.IO.File.Exists(srcPath))
                {
                    var maybeUploads = Path.Combine(permanentFolder, Path.GetFileName(entry));
                    if (System.IO.File.Exists(maybeUploads))
                        srcPath = maybeUploads;
                }

                if (!System.IO.File.Exists(srcPath))
                    continue;

                // If already inside /uploads, don't move—just reuse name; otherwise move from temp to uploads.
                string newFileName;
                string destPath;
                var isAlreadyInUploads = srcPath.Replace('\\', '/')
                                                .Contains("/uploads/", StringComparison.OrdinalIgnoreCase);

                if (isAlreadyInUploads)
                {
                    newFileName = Path.GetFileName(srcPath);
                    destPath = srcPath;
                }
                else
                {
                    newFileName = $"{Guid.NewGuid()}{Path.GetExtension(srcPath)}";
                    destPath = Path.Combine(permanentFolder, newFileName);
                    System.IO.File.Move(srcPath, destPath);
                }

                var fi = new FileInfo(destPath);

               newRows.Add(new DisclosureAttachment
{
    DisclosureId     = disclosure.Id,
    FileName         = newFileName,                   // الاسم المخزن (GUID)
    OriginalFileName = Path.GetFileName(entry),       // الاسم الأصلي
    FileType         = fi.Extension.TrimStart('.'),
    FileSize         = fi.Length,
    UploadedAt       = DateTime.UtcNow
});

        
    }
    

    if (newRows.Count > 0)
            {
                _context.DisclosureAttachments.AddRange(newRows);
                await _context.SaveChangesAsync();
            }
}


        // Resolve current principal to Users.Id in DB
        private async Task<int> CurrentDbUserIdAsync()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(idStr, out var id))
            {
                if (await _context.Users.AnyAsync(u => u.Id == id)) return id;
            }

            var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(email))
            {
                var found = await _context.Users
                    .Where(u => u.Email == email)
                    .Select(u => u.Id)
                    .FirstOrDefaultAsync();
                if (found != 0) return found;
            }

            var ad = User.FindFirstValue(ClaimTypes.WindowsAccountName) ?? User.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(ad))
            {
                var simple = ad.Contains('\\') ? ad.Split('\\').Last() : ad;
                var found  = await _context.Users
                    .Where(u => u.ADUserName == simple)
                    .Select(u => u.Id)
                    .FirstOrDefaultAsync();
                if (found != 0) return found;
            }

            return 0;
        }

        private static string GenerateToken(int size = 32)
        {
            var bytes = RandomNumberGenerator.GetBytes(size);
            return WebEncoders.Base64UrlEncode(bytes); // URL-safe
        }

        private static string Sha256Hex(string value)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        #endregion

        // ====== صفحة ما بعد الإرسال تعرض نموذج الاشتراك ======
        [HttpGet]
        public IActionResult SubmitDisclosure(string reportNumber)
        {
            ViewData["ReportNumber"] = reportNumber ?? "";
            return View(); // Views/Disclosure/SubmitDisclosure.cshtml
        }

        // ====== استقبال طلب الاشتراك عبر البريد ======
        public sealed class SubscribeEmailDto
        {
            public string? ReportNumber { get; set; }
            public string? Email        { get; set; }
        }

        [HttpPost("/Disclosure/SubscribeEmail")]
        public async Task<IActionResult> SubscribeEmail([FromBody] SubscribeEmailDto dto)
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

                // ابحث عن المستخدم بالبريد داخل قاعدة البيانات
                var user = await _context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Email == dto.Email);

                if (user is null)
                    return Ok(new { ok = true });

                // توليد توكن وتخزين هاشه فقط
                var rawToken = GenerateToken();
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

                // رابط التأكيد
                var confirmUrl = Url.Action(
                    "ConfirmSubscription", "Disclosure",
                    new { token = rawToken, report = dto.ReportNumber },
                    Request.Scheme
                ) ?? "#";

                // رسالة التأكيد
                var html = $@"
<p>لتأكيد الاشتراك لتحديثات البلاغ رقم <strong>{dto.ReportNumber}</strong>، اضغطي الرابط التالي:</p>
<p><a href=""{confirmUrl}"">تأكيد الاشتراك</a></p>
<p>صلاحية الرابط 24 ساعة ويُستخدم مرة واحدة.</p>";

                await _email.SendAsync(
                    dto.Email,
                    $"تأكيد الاشتراك لتحديثات البلاغ {dto.ReportNumber}",
                    html,
                    isHtml: true
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SubscribeEmail failed for {Email} / {Report}", dto?.Email, dto?.ReportNumber);
            }

            return Ok(new { ok = true });
        }

        // ====== تأكيد الاشتراك (مسموح بدون تسجيل) ======
        [AllowAnonymous]
        [HttpGet("/Disclosure/ConfirmSubscription")]
        public async Task<IActionResult> ConfirmSubscription(string token, string report)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(report))
                return View("ConfirmSubscriptionError");

            var hash = Sha256Hex(token);

            var ev = await _context.Set<EmailVerification>()
                .FirstOrDefaultAsync(x =>
                    x.TokenHash  == hash &&
                    x.Purpose    == $"subscribe_report:{report}" &&
                    x.ConsumedAt == null &&
                    x.ExpiresAt  > DateTime.UtcNow);

            if (ev is null)
                return View("ConfirmSubscriptionError");

            ev.ConsumedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            try
            {
                var note = new Notification
                {
                    RecipientId = ev.UserId,
                    EventType   = "SubscribeReport",
                    Message     = $"تم تأكيد الاشتراك لتحديثات البلاغ {report}."
                };
                _context.Add(note);
                await _context.SaveChangesAsync();

                // بث إلى مجموعة user-{DbId}
                await _hub.Clients.Group($"user-{ev.UserId}").SendAsync("Notify", new
                {
                    id        = note.Id,
                    eventType = note.EventType,
                    message   = note.Message,
                    createdAt = note.CreatedAt
                });

                // (اختياري) إشعار مجموعة الإداريين
                await _hub.Clients.Group("admins").SendAsync("Notify", new
                {
                    id        = note.Id,
                    eventType = "SubscribeReport",
                    message   = $"تم تأكيد اشتراك مستخدم لتحديثات البلاغ {report}.",
                    createdAt = note.CreatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SignalR notify failed in ConfirmSubscription for report {Report}", report);
            }

            ViewBag.Report = report;
            return View("ConfirmSubscriptionSuccess");
        }

        // ====== أدوات مساعدة أخرى ======
        private static bool IsValidEmail(string? v) =>
            !string.IsNullOrWhiteSpace(v) &&
            System.Text.RegularExpressions.Regex.IsMatch(v.Trim(), @"^[^\s@]+@[^\s@]+\.[^\s@]+$");

        // shortcuts
        public IActionResult Step1() => RedirectToAction(nameof(FormDetails));
        public IActionResult Step2() => RedirectToAction(nameof(SuspectedPeople));
        public IActionResult Step3() => RedirectToAction(nameof(RelatedPeople));
        public IActionResult Step4() => RedirectToAction(nameof(Attachments));
        public IActionResult Step5() => RedirectToAction(nameof(ReviewForm));
    }
}

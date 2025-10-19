using System;
using System.IO;                               // Path / File / Directory
using System.Linq;
using System.Text;
using System.Threading;                        // Thread.CurrentThread
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Claims;                  // Claims

using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;            // IWebHostEnvironment
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

using IfsahApp.Core.Enums;
using IfsahApp.Core.Models;
using IfsahApp.Core.ViewModels;
using IfsahApp.Hubs;
using IfsahApp.Infrastructure.Data;
using IfsahApp.Infrastructure.Services;
using IfsahApp.Utils;
using IfsahApp.Utils.Helpers;
using System.Resources;
using System.Globalization;

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
        private readonly ILogger<DisclosureController> _logger;

        private const string TempDataKey = "DisclosureForm";
        private const string TempUploadsFolderName = "tempUploads";

        public DisclosureController(
            ApplicationDbContext context,
            IWebHostEnvironment env,
            IEnumLocalizer enumLocalizer,
            IMapper mapper,
            IHubContext<NotificationHub> hub,
            ILogger<DisclosureController> logger)
        {
            _context       = context;
            _env           = env;
            _enumLocalizer = enumLocalizer;
            _mapper        = mapper;
            _hub           = hub;
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

            form.SavedAttachmentPaths =
                TempDataExtensions.Get<List<string>>(TempData, "TempAttachmentPaths")
                ?? form.SavedAttachmentPaths
                ?? new List<string>();

            TempData.Keep(TempDataKey);
            TempData.Keep("TempAttachmentPaths");
            return View(form);
        }

       // Step 4 - POST - معدل بشكل كامل
[HttpPost]
public async Task<IActionResult> Attachments(DisclosureFormViewModel model, [FromForm] string? submitDir)
{
    var form = GetFormFromTempData() ?? new DisclosureFormViewModel();
    form.Step = 4;

    // الحصول على الملفات المحفوظة مسبقاً
    form.SavedAttachmentPaths = TempDataExtensions.Get<List<string>>(TempData, "TempAttachmentPaths") ?? new List<string>();

    bool hasUploadErrors = false;

    // إذا كان هناك ملفات مرفوعة، التحقق منها وحفظها
    if (model.Attachments != null && model.Attachments.Any(f => f?.Length > 0))
    {
        // تنظيف ModelState للملفات فقط - هذا مهم جداً
        var attachmentKeys = ModelState.Keys.Where(k => k.Contains("Attachments")).ToList();
        foreach (var key in attachmentKeys)
        {
            ModelState.Remove(key);
        }

        foreach (var file in model.Attachments)
        {
            if (file == null || file.Length == 0) continue;

            // التحقق من الامتداد
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".mp4", ".mov", ".avi", ".wmv", ".mkv", ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx" };
            
            if (!allowedExtensions.Contains(ext))
            {
                string allowedList = string.Join(", ", allowedExtensions);
                var resourceManager = new ResourceManager("IfsahApp.Resources.Core.ViewModels.DisclosureFormViewModel", 
                    typeof(DisclosureFormViewModel).Assembly);
                
                string ErrorMessage;
                try
                {
                    ErrorMessage = resourceManager.GetString("FileInvalidExtension", CultureInfo.CurrentUICulture) 
                        ?? "File {0} has an invalid extension. Allowed extensions: {1}.";
                }
                catch
                {
                    ErrorMessage = "File {0} has an invalid extension. Allowed extensions: {1}.";
                }
                
                ModelState.AddModelError("Attachments", string.Format(ErrorMessage, file.FileName, allowedList));
                hasUploadErrors = true;
                continue;
            }

            // التحقق من الحجم
            if (file.Length > 10 * 1024 * 1024) // 10MB
            {
                var resourceManager = new ResourceManager("IfsahApp.Resources.Core.ViewModels.DisclosureFormViewModel", 
                    typeof(DisclosureFormViewModel).Assembly);
                
                string ErrorMessage;
                try
                {
                    ErrorMessage = resourceManager.GetString("FileMaxSize", CultureInfo.CurrentUICulture) 
                        ?? "File {0} exceeds the maximum allowed size of {1} MB.";
                }
                catch
                {
                    ErrorMessage = "File {0} exceeds the maximum allowed size of {1} MB.";
                }
                
                ModelState.AddModelError("Attachments", string.Format(ErrorMessage, file.FileName, 10));
                hasUploadErrors = true;
                continue;
            }

            // حفظ الملف إذا كان صالحاً
            try
            {
                var (savedFileName, error) = await FilePathHelper.SaveFileAsync(file, _env);
                if (!string.IsNullOrEmpty(error))
                {
                    ModelState.AddModelError("Attachments", error);
                    hasUploadErrors = true;
                    continue;
                }
                if (!string.IsNullOrEmpty(savedFileName))
                {
                    form.SavedAttachmentPaths.Add(savedFileName);
                    // إضافة رسالة نجاح (اختياري)
                    TempData["UploadSuccess"] = $"تم رفع الملف {file.FileName} بنجاح";
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("Attachments", $"خطأ في رفع الملف {file.FileName}: {ex.Message}");
                hasUploadErrors = true;
            }
        }
    }

    // حفظ الملفات في TempData دائماً (حتى إذا كان هناك أخطاء في الرفع الجديد)
    TempDataExtensions.Set(TempData, "TempAttachmentPaths", form.SavedAttachmentPaths);
    SaveFormToTempData(form);
    
    TempData.Keep(TempDataKey);
    TempData.Keep("TempAttachmentPaths");

    // إذا كان الزر "رفع" فقط، ابقى في الصفحة حتى مع الأخطاء
    if (string.Equals(submitDir, "upload", StringComparison.OrdinalIgnoreCase))
    {
        return RedirectToAction(nameof(Attachments));
    }

    // إذا كان هناك أخطاء في الرفع وليس زر "رفع"، امنع الانتقال
    if (hasUploadErrors && !string.Equals(submitDir, "upload", StringComparison.OrdinalIgnoreCase))
    {
        TempData.Keep(TempDataKey);
        TempData.Keep("TempAttachmentPaths");
        return View(form);
    }

    // إذا كان الزر "رجوع"
    if (string.Equals(submitDir, "back", StringComparison.OrdinalIgnoreCase))
    {
        return RedirectToAction(nameof(RelatedPeople));
    }

    // إذا كان الزر "التالي" ولا توجد أخطاء، انتقل
    return RedirectToAction(nameof(ReviewForm));
}

        // Step 5 - GET
        [HttpGet]
        public async Task<IActionResult> ReviewForm()
        {
            var form = GetFormFromTempData();
            if (form == null) return RedirectToAction(nameof(FormDetails));

            form.SavedAttachmentPaths =
                TempDataExtensions.Get<List<string>>(TempData, "TempAttachmentPaths")
                ?? new List<string>();

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

            var disclosure = _mapper.Map<Disclosure>(form);
            disclosure.DisclosureNumber = DisclosureNumberGeneratorHelper.Generate();
            disclosure.SubmittedById    = await CurrentDbUserIdAsync();

            _context.Disclosures.Add(disclosure);
            await _context.SaveChangesAsync();

            AddPersonsToDisclosure(disclosure, form);
            await _context.SaveChangesAsync();

            await TryAddAttachmentsFromTempAsync(disclosure, form.SavedAttachmentPaths);

            var recipients = await _context.Users
                .Where(u => u.IsActive && u.Role == Role.Admin)
                .Select(u => new { u.Id, u.Email })
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
                string srcPath = Path.IsPathRooted(entry) ? entry : Path.Combine(tempFolder, entry);

                if (!System.IO.File.Exists(srcPath))
                {
                    var maybeUploads = Path.Combine(permanentFolder, Path.GetFileName(entry));
                    if (System.IO.File.Exists(maybeUploads))
                        srcPath = maybeUploads;
                }

                if (!System.IO.File.Exists(srcPath))
                    continue;

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
                    FileName         = newFileName,
                    OriginalFileName = Path.GetFileName(entry),
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

        #endregion

        // ====== صفحة ما بعد الإرسال ======
        [HttpGet]
        public IActionResult SubmitDisclosure(string reportNumber)
        {
            ViewData["ReportNumber"] = reportNumber ?? "";
            return View(); // Views/Disclosure/SubmitDisclosure.cshtml
        }

        // shortcuts
        public IActionResult Step1() => RedirectToAction(nameof(FormDetails));
        public IActionResult Step2() => RedirectToAction(nameof(SuspectedPeople));
        public IActionResult Step3() => RedirectToAction(nameof(RelatedPeople));
        public IActionResult Step4() => RedirectToAction(nameof(Attachments));
        public IActionResult Step5() => RedirectToAction(nameof(ReviewForm));
    }
}

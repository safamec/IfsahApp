using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting; // IWebHostEnvironment
using IfsahApp.Core.Models;
using IfsahApp.Core.ViewModels;
using IfsahApp.Hubs;
using IfsahApp.Infrastructure.Data;
using IfsahApp.Infrastructure.Services;
using IfsahApp.Infrastructure.Services.Email;
using IfsahApp.Utils;
using IfsahApp.Utils.Helpers;
using IfsahApp.Core.Dtos;
using IfsahApp.Core.ViewModels.Emails;
using Newtonsoft.Json;
using System.Threading;        // Thread.CurrentThread
using System.IO;              // Path / File / Directory
using System.Security.Claims; // << ADDED for CurrentDbUserIdAsync
using System;                 // DateTime, Guid

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
            _context = context;
            _env = env;
            _enumLocalizer = enumLocalizer;
            _mapper = mapper;
            _hub = hub;
            _email = email;
            _viewRender = viewRender;
            _logger = logger;
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
            form.Step = 1;
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

            // 🔧 hydrate from TempData so the view can render existing files
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
        public IActionResult ReviewForm()
        {
            var form = GetFormFromTempData();
            if (form == null)
                return RedirectToAction(nameof(FormDetails));

            // 🔧 ensure attachments are visible on review
            form.SavedAttachmentPaths =
                TempDataExtensions.Get<List<string>>(TempData, "TempAttachmentPaths")
                ?? form.SavedAttachmentPaths
                ?? new List<string>();

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
            if (form == null)
                return RedirectToAction(nameof(FormDetails));

            if (!ModelState.IsValid)
                return RedirectToAction(nameof(ReviewForm));

            var disclosure = _mapper.Map<Disclosure>(form);
            disclosure.DisclosureNumber = DisclosureNumberGeneratorHelper.Generate();

            // << ADDED: resolve current DB user id instead of hard-coded 1
            var submitterId = await CurrentDbUserIdAsync();
            disclosure.SubmittedById = submitterId;

            AddPersonsToDisclosure(disclosure, form);

            // move files from tempUploads -> uploads
            await TryAddAttachmentsFromTempAsync(disclosure, form.SavedAttachmentPaths);

            _context.Disclosures.Add(disclosure);
            await _context.SaveChangesAsync();

            // << ADDED: create a notification for the submitter
            try
            {
                var link = Url.Action(
                    action: "SubmitDisclosure",
                    controller: "Disclosure",
                    values: new { reportNumber = disclosure.DisclosureNumber },
                    protocol: Request.Scheme
                ) ?? "#";

                var receipt = new Notification
                {
                    RecipientId = submitterId,
                    EventType   = "Receipt",
                    Message     = $"تم استلام البلاغ رقم {disclosure.DisclosureNumber}.",
                    CreatedAt   = DateTime.UtcNow,
                    IsRead      = false
                };

                _context.Notifications.Add(receipt);
                await _context.SaveChangesAsync();

                // بث فوري (SignalR) — يعتمد أن UserIdentifier يطابق Users.Id.ToString()
                try
                {
                    await _hub.Clients.User(submitterId.ToString()).SendAsync("Notify", new
                    {
                        id        = receipt.Id,
                        eventType = receipt.EventType,
                        message   = receipt.Message,
                        createdAt = receipt.CreatedAt,
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "SignalR notify (submitter) failed for report {Report}", disclosure.DisclosureNumber);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create submitter notification for report {Report}", disclosure.DisclosureNumber);
            }

            // cleanup TempData
            TempData.Remove(TempDataKey);
            TempData.Remove("TempAttachmentPaths");

            // موجود أصلاً عندك: يُبلغ الإداريين (إن لزم) وينشئ إشعاراتهم
            await NotificationHelper.NotifyAdminsAsync(_context, _hub, disclosure, Url);

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
                    ArabicName = dt.ArabicName ?? dt.EnglishName,
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

            disclosure.Attachments ??= new List<DisclosureAttachment>();

            var tempFolder = Path.Combine(_env.WebRootPath, TempUploadsFolderName);
            var permanentFolder = Path.Combine(_env.WebRootPath, "uploads");

            if (!Directory.Exists(permanentFolder))
                Directory.CreateDirectory(permanentFolder);

            foreach (var fileName in tempFileNames)
            {
                var tempFilePath = Path.Combine(tempFolder, fileName);
                if (!System.IO.File.Exists(tempFilePath))
                    continue;

                var newFileName = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";
                var permanentFilePath = Path.Combine(permanentFolder, newFileName);

                System.IO.File.Move(tempFilePath, permanentFilePath);

                var fileInfo = new FileInfo(permanentFilePath);

                disclosure.Attachments.Add(new DisclosureAttachment
                {
                    FileName = newFileName,
                    FileType = fileInfo.Extension.TrimStart('.'),
                    FileSize = fileInfo.Length
                });
            }

            await Task.CompletedTask;
        }

        // << ADDED: resolve current user to Users.Id in DB
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
                var found = await _context.Users
                    .Where(u => u.ADUserName == ad)
                    .Select(u => u.Id)
                    .FirstOrDefaultAsync();
                if (found != 0) return found;
            }

            return 0;
        }

        #endregion

        [HttpGet]
        public IActionResult SubmitDisclosure(string reportNumber)
        {
            ViewData["ReportNumber"] = reportNumber;
            return View();
        }

        [HttpPost("/Disclosure/SubscribeEmail")]
        public async Task<IActionResult> SubscribeEmail([FromBody] SubscribeEmailDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.ReportNumber))
                return BadRequest(new { ok = false, message = "رقم البلاغ مطلوب" });

            if (!IsValidEmail(dto.Email))
                return BadRequest(new { ok = false, message = "البريد الإلكتروني غير صالح" });

            var exists = await _context.Disclosures
                .AsNoTracking()
                .AnyAsync(d => d.DisclosureNumber == dto.ReportNumber);

            if (!exists)
                return NotFound(new { ok = false, message = "رقم البلاغ غير موجود" });

            try
            {
                var model = new DisclosureConfirmEmailViewModel
                {
                    ReportNumber = dto.ReportNumber,
                    ReceivedDate = DateTime.Now.ToString("yyyy/MM/dd"),
                    TrackUrl = Url.Action("Track", "Disclosure", new { id = dto.ReportNumber }, Request.Scheme) ?? "#",
                    LogoUrl = $"{Request.Scheme}://{Request.Host}/images/logo-mem.svg"
                };

                var html = await _viewRender.RenderToStringAsync("Emails/DisclosureConfirm", model);

                await _email.SendAsync(
                    dto.Email,
                    $"تأكيد الاشتراك لتحديثات البلاغ {dto.ReportNumber}",
                    html,
                    isHtml: true
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send confirmation email for report {Report}", dto.ReportNumber);
            }

            return Ok(new { ok = true, message = "تم التحقق وإرسال التأكيد على البريد" });
        }

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

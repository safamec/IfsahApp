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
using IfsahApp.Core.Dtos;
using IfsahApp.Core.ViewModels.Emails;
using Newtonsoft.Json;

namespace IfsahApp.Web.Controllers
{
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

        #region Multi-Step Actions

        // Step 1 - GET: FormDetails
        [HttpGet]
        public async Task<IActionResult> FormDetails()
        {
            await LoadDisclosureTypesAsync();
            return View(GetFormFromTempData() ?? new DisclosureFormViewModel());
        }

        // Step 1 - POST: FormDetails
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FormDetails(DisclosureFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await LoadDisclosureTypesAsync(model.DisclosureTypeId);
                return View(model);
            }

            var form = GetFormFromTempData() ?? new DisclosureFormViewModel();
            form.DisclosureTypeId = model.DisclosureTypeId;
            form.Description = model.Description;
            form.Location = model.Location;
            form.IncidentStartDate = model.IncidentStartDate;
            form.IncidentEndDate = model.IncidentEndDate;

            SaveFormToTempData(form);

            return RedirectToAction(nameof(SuspectedPeople));
        }

        // Step 2 - GET: SuspectedPeople
        [HttpGet]
        public IActionResult SuspectedPeople()
        {
            return View(GetFormFromTempData() ?? new DisclosureFormViewModel());
        }

        // Step 2 - POST: SuspectedPeople
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SuspectedPeople(DisclosureFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var form = GetFormFromTempData() ?? new DisclosureFormViewModel();
            form.SuspectedPersons = model.SuspectedPersons ?? new List<SuspectedPerson>();

            SaveFormToTempData(form);

            return RedirectToAction(nameof(RelatedPeople));
        }

        // Step 3 - GET: RelatedPeople
        [HttpGet]
        public IActionResult RelatedPeople()
        {
            return View(GetFormFromTempData() ?? new DisclosureFormViewModel());
        }

        // Step 3 - POST: RelatedPeople
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RelatedPeople(DisclosureFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var form = GetFormFromTempData() ?? new DisclosureFormViewModel();
            form.RelatedPersons = model.RelatedPersons ?? new List<RelatedPerson>();

            SaveFormToTempData(form);

            return RedirectToAction(nameof(Attachments));
        }

        // GET: Attachments
        [HttpGet]
        public IActionResult Attachments()
        {
            var form = GetFormFromTempData() ?? new DisclosureFormViewModel();

            // Important: Keep TempData so it persists for the next request as well
            TempData.Keep(TempDataKey);
            TempData.Keep("TempAttachmentPaths");

            return View(form);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Attachments(DisclosureFormViewModel model, [FromForm] string submitAction)
        {
            var form = GetFormFromTempData() ?? new DisclosureFormViewModel();

            if (model.Attachments == null || !model.Attachments.Any())
            {
                ModelState.AddModelError("Attachments", "Please upload at least one file.");
                return View(form); // Return the correct form state
            }

            // Retrieve existing files from TempData
            var existingFiles = TempDataExtensions.Get<List<string>>(TempData, "TempAttachmentPaths") ?? new List<string>();

            foreach (var file in model.Attachments)
            {
                var (savedFileName, error) = await FilePathHelper.SaveFileAsync(file, _env);

                if (!string.IsNullOrEmpty(error))
                {
                    ModelState.AddModelError("Attachments", error);
                    continue;
                }

                if (!string.IsNullOrEmpty(savedFileName))
                {
                    existingFiles.Add(savedFileName);
                }
                // Update TempData with new attachments
                TempDataExtensions.Set(TempData, "TempAttachmentPaths", existingFiles);

                // Update the form state
                form.SavedAttachmentPaths = existingFiles;
                SaveFormToTempData(form);

                TempData["UploadSuccess"] = true;

                // Persist TempData across the redirect or next request
                TempData.Keep(TempDataKey);
                TempData.Keep("TempAttachmentPaths");
            }
            return View(form);

        }


        // Step 5 - POST: Submit Final Disclosure
        [HttpGet]
        public IActionResult ReviewForm()
        {
            var form = GetFormFromTempData();
            if (form == null)
                return RedirectToAction(nameof(FormDetails));

            TempData.Keep(TempDataKey); // Keep TempData for next requests if needed
            return View(form);
        }

        [HttpPost]
        [ActionName("ReviewForm")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewFormPost()
        {
            var form = GetFormFromTempData();
            if (form == null)
                return RedirectToAction(nameof(FormDetails));

            if (!ModelState.IsValid)
                return RedirectToAction(nameof(ReviewForm));

            var disclosure = _mapper.Map<Disclosure>(form);
            disclosure.DisclosureNumber = DisclosureNumberGeneratorHelper.Generate();
            disclosure.SubmittedById = 1; // Replace with actual user ID

            AddPersonsToDisclosure(disclosure, form);

            // Move files from temp folder to permanent storage and add to disclosure
            await TryAddAttachmentsFromTempAsync(disclosure, form.SavedAttachmentPaths);

            _context.Disclosures.Add(disclosure);
            await _context.SaveChangesAsync();

            // Clear TempData keys after submission
            TempData.Remove(TempDataKey);
            TempData.Remove("TempAttachmentPaths");

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
                }).ToListAsync();

            var displayField = (culture == "ar") ? "ArabicName" : "EnglishName";
            ViewBag.DisclosureTypes = new SelectList(disclosureTypes, "Id", displayField, selectedId);
        }

        private DisclosureFormViewModel? GetFormFromTempData()
        {
            if (TempData.TryGetValue(TempDataKey, out object? raw) && raw is string json)
            {
                TempData.Keep(TempDataKey);
                return JsonConvert.DeserializeObject<DisclosureFormViewModel>(json); // ✅
            }
            return null;
        }

        private void SaveFormToTempData(DisclosureFormViewModel model)
        {
            TempData[TempDataKey] = JsonConvert.SerializeObject(model); // ✅
        }

        private void AddPersonsToDisclosure(Disclosure disclosure, DisclosureFormViewModel model)
        {
            if (model.SuspectedPersons != null)
            {
                disclosure.SuspectedPeople ??= new List<SuspectedPerson>();
                foreach (var suspected in model.SuspectedPersons) disclosure.SuspectedPeople.Add(suspected);
            }
            if (model.RelatedPersons != null)
            {
                disclosure.RelatedPeople ??= new List<RelatedPerson>();
                foreach (var related in model.RelatedPersons) disclosure.RelatedPeople.Add(related);
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

                // Move file from temp to permanent location
                System.IO.File.Move(tempFilePath, permanentFilePath);

                var fileInfo = new FileInfo(permanentFilePath);

                disclosure.Attachments.Add(new DisclosureAttachment
                {
                    FileName = newFileName,
                    FileType = fileInfo.Extension.TrimStart('.'),
                    FileSize = fileInfo.Length
                });
            }
        }

        #endregion

        [HttpGet]
        public IActionResult SubmitDisclosure(string reportNumber)
        {
            ViewData["ReportNumber"] = reportNumber;
            return View();
        }

        [HttpPost("/Disclosure/SubscribeEmail")]
        [ValidateAntiForgeryToken]
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
    }
}

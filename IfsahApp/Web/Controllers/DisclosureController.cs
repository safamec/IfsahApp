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
using IfsahApp.Core.Enums;
using IfsahApp.Core.Dtos;                          
using IfsahApp.Core.ViewModels.Emails;             
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Authorization;

namespace IfsahApp.Web.Controllers
{
    [AllowAnonymous] // ← This allows anonymous access to all actions
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

        public DisclosureController(
            ApplicationDbContext context,
            IWebHostEnvironment env,
            IEnumLocalizer enumLocalizer,
            IMapper mapper,
            IHubContext<NotificationHub> hub,
            IEmailService email,
            ViewRenderService viewRender,
            ILogger<DisclosureController> logger
        )
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

        private static bool IsValidEmail(string? v)
            => !string.IsNullOrWhiteSpace(v) &&
               System.Text.RegularExpressions.Regex.IsMatch(v.Trim(), @"^[^\s@]+@[^\s@]+\.[^\s@]+$");

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var culture = Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName;
            var disclosureTypes = _context.DisclosureTypes.ToList();
            string displayField = culture == "ar" ? "ArabicName" : "EnglishName";
            ViewBag.DisclosureTypes = new SelectList(disclosureTypes, "Id", displayField);
            await ProperSelectListType();
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

                // TODO: اربطيها بالمستخدم الحالي بدل القيمة الثابتة
                disclosure.SubmittedById = 1;

                disclosure.SuspectedPeople ??= new List<SuspectedPerson>();
                disclosure.RelatedPeople ??= new List<RelatedPerson>();

                if (model.SuspectedPersons != null)
                    foreach (var s in model.SuspectedPersons)
                        disclosure.SuspectedPeople.Add(s);

                AddPersonsToDisclosure(disclosure, model);
                await TryAddAttachmentsAsync(disclosure, model.Attachments);

                _context.Disclosures.Add(disclosure);
                await _context.SaveChangesAsync();

                await NotificationHelper.NotifyAdminsAsync(_context, _hub, disclosure, Url);

                return RedirectToAction("SubmitDisclosure", new { reportNumber = disclosure.DisclosureNumber });
            }

            await ProperSelectListType(model.DisclosureTypeId);
            return View(model);
        }

        [HttpGet]
        public IActionResult SubmitDisclosure(string reportNumber)
        {
            ViewData["ReportNumber"] = reportNumber;
            return View();
        }

        // ================================
        // SubscribeEmail: يستقبل JSON من الواجهة ويرسل إيميل بالقالب
        // ================================
        [HttpPost("/Disclosure/SubscribeEmail")]
        [ValidateAntiForgeryToken] // تأكد إرسال التوكن من JS
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
                    // نمرر رابط صورة مباشر من wwwroot
                    LogoUrl = $"{Request.Scheme}://{Request.Host}/images/logo-mem.svg"
                };

                // Render cshtml → HTML
                var html = await _viewRender.RenderToStringAsync("Emails/DisclosureConfirm", model);

                // إرسال الإيميل كـ HTML
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
                // لا نفشل الطلب بسبب الإيميل
            }

            return Ok(new { ok = true, message = "تم التحقق وإرسال التأكيد على البريد" });
        }

        // ================================

        private async Task ProperSelectListType(int? selectedId = null)
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

        private void AddPersonsToDisclosure(Disclosure disclosure, DisclosureFormViewModel model)
        {
            if (model.SuspectedPersons != null)
            {
                disclosure.SuspectedPeople ??= new List<SuspectedPerson>();
                foreach (var suspected in model.SuspectedPersons)
                    disclosure.SuspectedPeople.Add(suspected);
            }

            if (model.RelatedPersons != null)
            {
                disclosure.RelatedPeople ??= new List<RelatedPerson>();
                foreach (var related in model.RelatedPersons)
                    disclosure.RelatedPeople.Add(related);
            }
        }

        private async Task<bool> TryAddAttachmentsAsync(Disclosure disclosure, IList<IFormFile> attachments)
        {
            if (attachments == null || attachments.Count == 0)
                return true;

            disclosure.Attachments ??= new List<DisclosureAttachment>();

            foreach (var file in attachments)
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

            return true;
        }
    }
}

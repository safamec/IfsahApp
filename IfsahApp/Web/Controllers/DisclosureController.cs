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
using System.Threading;
using IfsahApp.Core.Dtos;

namespace IfsahApp.Web.Controllers
{
    public class DisclosureController(
        ApplicationDbContext context,
        IWebHostEnvironment env,
        IEnumLocalizer enumLocalizer,
        IMapper mapper,
        IHubContext<NotificationHub> hub,
        IEmailService email
    ) : Controller
    {
        private readonly ApplicationDbContext _context = context;
        private readonly IWebHostEnvironment _env = env;
        private readonly IEnumLocalizer _enumLocalizer = enumLocalizer;
        private readonly IMapper _mapper = mapper;
        private readonly IHubContext<NotificationHub> _hub = hub;
        private readonly IEmailService _email = email;

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

        // ================================
        // SubscribeEmail (بدون موديلات جديدة)
        // ================================
        [HttpPost]
        [Route("Disclosure/SubscribeEmail")]
        [IgnoreAntiforgeryToken]
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
                await _email.SendAsync(
                    dto.Email,
                    "تأكيد الاشتراك لتحديثات البلاغ",
                    $"تم تفعيل الإشعارات لبلاغك رقم: <strong>{dto.ReportNumber}</strong>",
                    isHtml: true
                );
            }
            catch (Exception)
            {
                // Do not fail the request on email failure
            }

            return Ok(new { ok = true, message = "تم التحقق وإرسال التأكيد على البريد" });
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

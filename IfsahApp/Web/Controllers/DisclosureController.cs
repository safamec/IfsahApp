using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering; // ✅ Needed for SelectList
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using IfsahApp.Core.Models;
using IfsahApp.Core.ViewModels;
using IfsahApp.Hubs;
using IfsahApp.Infrastructure.Data;
using IfsahApp.Infrastructure.Services;
using IfsahApp.Utils;
using IfsahApp.Utils.Helpers; // ✅ Added to access NotificationHelper


namespace IfsahApp.Web.Controllers
{
    public class DisclosureController(
        ApplicationDbContext context,
        IWebHostEnvironment env,
        IEnumLocalizer enumLocalizer,
        IMapper mapper,
        IHubContext<NotificationHub> hub) : Controller
    {
        private readonly ApplicationDbContext _context = context;
        private readonly IWebHostEnvironment _env = env;
        private readonly IEnumLocalizer _enumLocalizer = enumLocalizer;
        private readonly IMapper _mapper = mapper;
        private readonly IHubContext<NotificationHub> _hub = hub; // notifications hub

        [HttpGet]
        public IActionResult Create()
        {
            var culture = Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName;
            var disclosureTypes = _context.DisclosureTypes.ToList();

            // Choose display field based on culture
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
                disclosure.RelatedPeople ??= new List<RelatedPerson>();

                if (model.SuspectedPersons != null)
                {
                    foreach (var suspected in model.SuspectedPersons)
                        disclosure.SuspectedPeople.Add(suspected);
                }

                if (model.RelatedPersons != null)
                {
                    foreach (var related in model.RelatedPersons)
                        disclosure.RelatedPeople.Add(related);
                }

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


                return RedirectToAction("SubmitDisclosure", new { reportNumber = disclosure.DisclosureNumber });
            }

            // ❗❗ FIX: repopulate a proper SelectList when validation fails
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
    }
}

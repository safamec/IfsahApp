using Microsoft.AspNetCore.Mvc;
using IfsahApp.Core.Models;
using IfsahApp.Infrastructure.Data;
using IfsahApp.Core.ViewModels;
using IfsahApp.Infrastructure.Services;
using AutoMapper;
using IfsahApp.Utils;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace IfsahApp.Web.Controllers;

public class DisclosureController(ApplicationDbContext context, IWebHostEnvironment env, IEnumLocalizer enumLocalizer, IMapper mapper) : Controller
{
    private readonly ApplicationDbContext _context = context;
    private readonly IWebHostEnvironment _env = env;
    private readonly IEnumLocalizer _enumLocalizer = enumLocalizer;
    private readonly IMapper _mapper = mapper;

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

            return RedirectToAction("SubmitDisclosure", new { reportNumber = disclosure.DisclosureNumber });
        }

        ViewBag.DisclosureTypes = _context.DisclosureTypes.ToList();
        return View(model);
    }

    [HttpGet]
    public IActionResult SubmitDisclosure(string reportNumber)
    {
        ViewData["ReportNumber"] = reportNumber;
        return View();
    }
}


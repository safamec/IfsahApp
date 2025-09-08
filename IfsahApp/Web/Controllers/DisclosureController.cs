using Microsoft.AspNetCore.Mvc;
using IfsahApp.Core.Models;
using IfsahApp.Infrastructure.Data;
using IfsahApp.Core.ViewModels;
using IfsahApp.Infrastructure.Services;

namespace IfsahApp.Web.Controllers;

public class DisclosureController(ApplicationDbContext context, IWebHostEnvironment env, IEnumLocalizer enumLocalizer) : Controller
{
    private readonly ApplicationDbContext _context = context;
    private readonly IWebHostEnvironment _env = env;
    private readonly IEnumLocalizer _enumLocalizer = enumLocalizer;

    // GET: /Disclosure/Create
    [HttpGet]
    public IActionResult Create()
    {
        ViewBag.DisclosureTypes = _context.DisclosureTypes.ToList();

        var viewModel = new DisclosureFormViewModel
        {
            Disclosure = new Disclosure()
        };

        return View(viewModel);
    }

    // POST: /Disclosure/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DisclosureFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            var disclosure = model.Disclosure;

            if (disclosure != null)
            {
                disclosure.DisclosureNumber = $"DISC-{DateTime.UtcNow.Year}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";
                disclosure.SubmittedById = 1;
            }
            else
            {
                // Handle the case when Disclosure is null
                throw new InvalidOperationException("Disclosure cannot be null.");
            }

            disclosure.SuspectedPeople ??= [];

            disclosure.RelatedPeople ??= [];

            if (model.SuspectedPersons != null && model.SuspectedPersons.Count > 0)
            {
                foreach (var suspected in model.SuspectedPersons)
                {
                    disclosure.SuspectedPeople.Add(suspected);
                }
            }

            if (model.RelatedPersons != null && model.RelatedPersons.Count > 0)
            {
                foreach (var related in model.RelatedPersons)
                {
                    disclosure.RelatedPeople.Add(related);
                }
            }


            var allowedExtensions = new[] {
                 ".jpg", ".jpeg", ".png", ".gif", ".bmp",
                 ".mp4", ".mov", ".avi", ".wmv", ".mkv",
                 ".pdf",
                 ".doc", ".docx",
                 ".xls", ".xlsx",
                 ".ppt", ".pptx"
                 };

            const long maxFileSize = 10 * 1024 * 1024; // 10 MB

            if (model.Attachments != null && model.Attachments.Count > 0)
            {
                var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads");
                Directory.CreateDirectory(uploadsFolder);

                // Ensure list is initialized
                disclosure.Attachments ??= new List<DisclosureAttachment>();

                foreach (var file in model.Attachments)
                {
                    if (file.Length > 0)
                    {
                        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

                        // Check allowed extension
                        if (!allowedExtensions.Contains(extension))
                        {
                            ModelState.AddModelError("Attachments", $"File type '{extension}' is not allowed.");
                            continue; // Skip this file
                        }

                        // Check file size
                        if (file.Length > maxFileSize)
                        {
                            ModelState.AddModelError("Attachments", $"File '{file.FileName}' exceeds the 10MB size limit.");
                            continue; // Skip this file
                        }

                        // Generate unique filename
                        var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                        // Save file
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        // Save attachment info
                        disclosure.Attachments.Add(new DisclosureAttachment
                        {
                            FileName = uniqueFileName,
                            FileType = extension.TrimStart('.'),
                            FileSize = file.Length
                        });
                    }
                }
            }



            _context.Disclosures.Add(disclosure);
            await _context.SaveChangesAsync();

            return RedirectToAction("Success");
        }

        ViewBag.DisclosureTypes = _context.DisclosureTypes.ToList();

        return View(model);
    }


}

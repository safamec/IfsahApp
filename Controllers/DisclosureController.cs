using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using IfsahApp.Models;
using IfsahApp.Data;
using IfsahApp.Services;

namespace IfsahApp.Controllers;

public class DisclosureController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IEnumLocalizer _enumLocalizer;

    public DisclosureController(ApplicationDbContext context, IWebHostEnvironment env, IEnumLocalizer enumLocalizer)
    {
        _context = context;
        _env = env;
        _enumLocalizer = enumLocalizer;
    }

    // GET: /Disclosure/Create
    [HttpGet]
    public IActionResult Create()
    {
        ViewBag.DisclosureTypes = _context.DisclosureTypes.ToList();
        return View();
    }

    // POST: /Disclosure/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        Disclosure model,
        List<IFormFile> Attachments,
        List<string> SuspectedPeopleNames,
        List<string> RelatedPeopleNames)
    {
        if (ModelState.IsValid)
        {
            // Optional: Generate a disclosure number
            model.DisclosureNumber = $"DISC-{DateTime.UtcNow.Year}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}";

            // Set the current user (mock or get from auth)
            model.SubmittedById = 1; // Replace with actual user ID from login

            // Handle suspected people
            if (SuspectedPeopleNames != null)
            {
                foreach (var name in SuspectedPeopleNames)
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        model.SuspectedPeople.Add(new SuspectedPerson { Name = name });
                    }
                }
            }

            // Handle related people
            if (RelatedPeopleNames != null)
            {
                foreach (var name in RelatedPeopleNames)
                {
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        model.RelatedPeople.Add(new RelatedPerson { Name = name });
                    }
                }
            }

            // Handle file attachments
            if (Attachments != null && Attachments.Count > 0)
            {
                model.Attachments = new List<DisclosureAttachment>();

                foreach (var file in Attachments)
                {
                    if (file.Length > 0)
                    {
                        var uploads = Path.Combine(_env.WebRootPath, "uploads");
                        Directory.CreateDirectory(uploads);

                        var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                        var filePath = Path.Combine(uploads, fileName);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }

                        model.Attachments.Add(new DisclosureAttachment
                        {
                            FileName = fileName,
                            FileType = Path.GetExtension(file.FileName)?.TrimStart('.'),
                            FileSize = file.Length
                        });
                    }
                }
            }

            // Save to DB
            _context.Disclosures.Add(model);
            await _context.SaveChangesAsync();

            return RedirectToAction("Success"); // You can implement a success view
        }

        ViewBag.DisclosureTypes = _context.DisclosureTypes.ToList(); // Repopulate dropdown
        return View(model);
    }

    // Optional Success page
    public IActionResult Success()
    {
        return View();
    }
}

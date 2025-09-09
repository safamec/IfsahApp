using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IfsahApp.Infrastructure.Data;
using IfsahApp.Core.Models;
using IfsahApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;

namespace IfsahApp.Web.Controllers;
 [Authorize(Policy = "AuditTeam")]
public class ReviewController(ApplicationDbContext context, IEnumLocalizer enumLocalizer) : Controller
{
    private readonly ApplicationDbContext _context = context;
    private readonly IEnumLocalizer _enumLocalizer = enumLocalizer;

      public async Task<IActionResult> Index()
{
    var cases = await _context.Disclosures
        .Include(d => d.DisclosureType)
        .OrderByDescending(d => d.SubmittedAt)
                .Select(d => new CaseItem
            {
                Type = d.DisclosureType != null ? d.DisclosureType.EnglishName : "N/A",
                Reference = d.DisclosureNumber,
                Date = d.SubmittedAt,
                Location = d.Location ?? string.Empty,
                Status = _enumLocalizer.LocalizeEnum(d.Status),
                Description = d.Description ?? string.Empty
            })
            .ToListAsync();
    return View(cases);
}

    public IActionResult ReviewDisclosure()
    {
        var disclosureCases = new List<CaseItem>
            {
                new CaseItem
                {
                    Type = "Ethics Violation",
                    Reference = "VR-1704578901-xyz987abc",
                    Date = new DateTime(2025, 01, 10),
                    Location = "HR Department",
                    Status = "Pending",
                    Description = "Reported ethics violation in employee conduct"
                },
                new CaseItem
                {
                    Type = "Security Breach",
                    Reference = "VR-1704578910-qwe123asd",
                    Date = new DateTime(2025, 02, 05),
                    Location = "IT Department",
                    Status = "Investigating",
                    Description = "Suspicious access detected in secure servers"
                }
            };

        // Pass the first item only (a single CaseItem)
        return View(disclosureCases.FirstOrDefault());
    }
    [HttpPost]
    public async Task<IActionResult> SubmitReview(string reviewerNotes, IFormFile attachment)
    {
        // Optional: Save file if uploaded
        if (attachment != null && attachment.Length > 0)
        {
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var filePath = Path.Combine(uploadsFolder, Path.GetFileName(attachment.FileName));

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await attachment.CopyToAsync(stream);
            }

            // You can store this path if needed
        }

        // Optional: Log or store reviewer notes
        Console.WriteLine("Reviewer Notes: " + reviewerNotes);

        // Redirect or return a view
        return RedirectToAction("Index");
    }

}



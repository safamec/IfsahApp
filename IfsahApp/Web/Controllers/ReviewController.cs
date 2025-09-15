using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IfsahApp.Infrastructure.Data;
using IfsahApp.Core.Models;
using IfsahApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;

namespace IfsahApp.Web.Controllers;

[Authorize(Roles = "Examiner")]
public class ReviewController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IEnumLocalizer _enumLocalizer;

    public ReviewController(ApplicationDbContext context, IEnumLocalizer enumLocalizer)
    {
        _context = context;
        _enumLocalizer = enumLocalizer;
    }

    // ============================
    // REVIEW DASHBOARD (Index)
    // ============================
    public IActionResult Index(string? reference, int page = 1, int pageSize = 10)
    {
        var disclosures = _context.Disclosures
            .Include(d => d.DisclosureType)
            .OrderByDescending(d => d.SubmittedAt)
            .AsEnumerable()
            .ToList();

        // Map to CaseItem (Status as string)
        var cases = disclosures.Select(d => new CaseItem
        {
            Type = d.DisclosureType?.EnglishName ?? "N/A",
            Reference = d.DisclosureNumber,
            Date = d.SubmittedAt,
            Location = d.Location ?? string.Empty,
            Status = _enumLocalizer.LocalizeEnum(d.Status),  // Now string
            Description = d.Description ?? string.Empty
        });

        // Filter: show only Assigned disclosures
        cases = cases.Where(c => c.Status == "Assigned");

        // Filter by reference if provided
        if (!string.IsNullOrEmpty(reference))
            cases = cases.Where(c => c.Reference.Contains(reference));

        // Pagination
        var totalItems = cases.Count();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var pagedCases = cases
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // Pass data to view
        ViewBag.SelectedReference = reference;
        ViewBag.PageSize = pageSize;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;

        return View(pagedCases);
    }

    // ============================
    // REVIEW DISCLOSURE ACTION
    // ============================
    public IActionResult ReviewDisclosure(string reference)
    {
        var disclosure = _context.Disclosures
            .Include(d => d.DisclosureType)
            .FirstOrDefault(d => d.DisclosureNumber == reference);

        if (disclosure == null)
            return NotFound();

        var caseItem = new CaseItem
        {
            Type = disclosure.DisclosureType?.EnglishName ?? "N/A",
            Reference = disclosure.DisclosureNumber,
            Date = disclosure.SubmittedAt,
            Location = disclosure.Location ?? string.Empty,
            Status = _enumLocalizer.LocalizeEnum(disclosure.Status), // string
            Description = disclosure.Description ?? string.Empty
        };

        return View(caseItem);
    }

    // ============================
    // SUBMIT REVIEW ACTION
    // ============================
    [HttpPost]
    public async Task<IActionResult> SubmitReview(string reference, string reviewerNotes, IFormFile attachment)
    {
        if (attachment != null && attachment.Length > 0)
        {
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            var filePath = Path.Combine(uploadsFolder, Path.GetFileName(attachment.FileName));
            await using var stream = new FileStream(filePath, FileMode.Create);
            await attachment.CopyToAsync(stream);
        }

        Console.WriteLine($"Reference: {reference}");
        Console.WriteLine("Reviewer Notes: " + reviewerNotes);

        return RedirectToAction("Index");
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting; // IWebHostEnvironment
using IfsahApp.Core.Enums;
using IfsahApp.Core.Models;
using IfsahApp.Infrastructure.Data;
using IfsahApp.Infrastructure.Services;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace IfsahApp.Web.Controllers;

[Authorize(Roles = "Examiner,Admin")]
public class ReviewController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IEnumLocalizer _enumLocalizer;
    private readonly IWebHostEnvironment _env;

    public ReviewController(ApplicationDbContext context, IEnumLocalizer enumLocalizer, IWebHostEnvironment env)
    {
        _context = context;
        _enumLocalizer = enumLocalizer;
        _env = env;
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

        var cases = disclosures.Select(d => new CaseItem
        {
            Type        = d.DisclosureType?.EnglishName ?? "N/A",
            Reference   = d.DisclosureNumber,
            Date        = d.SubmittedAt,
            Location    = d.Location ?? string.Empty,
            Status      = _enumLocalizer.LocalizeEnum(d.Status),
            Description = d.Description ?? string.Empty
        });

        cases = cases.Where(c => c.Status == "Assigned");

        if (!string.IsNullOrEmpty(reference))
            cases = cases.Where(c => c.Reference.Contains(reference));

        var totalItems = cases.Count();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        var pagedCases = cases.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        ViewBag.SelectedReference = reference;
        ViewBag.PageSize = pageSize;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.Message = TempData["Message"];

        return View(pagedCases);
    }

    // ============================
    // REVIEW DISCLOSURE
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
            Type        = disclosure.DisclosureType?.EnglishName ?? "N/A",
            Reference   = disclosure.DisclosureNumber,
            Date        = disclosure.SubmittedAt,
            Location    = disclosure.Location ?? string.Empty,
            Status      = _enumLocalizer.LocalizeEnum(disclosure.Status),
            Description = disclosure.Description ?? string.Empty
        };

        return View(caseItem);
    }

    // ============================
    // EXTRA POPUP (Suspected / Related / Attachments)
    // ============================
[HttpGet]
public IActionResult Extras(string reference)
{
    var d = _context.Disclosures
        .AsNoTracking()
        .Include(x => x.SuspectedPeople)
        .Include(x => x.RelatedPeople)
        .FirstOrDefault(x => x.DisclosureNumber == reference);

    if (d == null) return NotFound();

    // People
    ViewBag.Suspected = d.SuspectedPeople
        .Select(p => new { p.Name, p.Email, p.Phone, p.Organization })
        .ToList();

    ViewBag.Related = d.RelatedPeople
        .Select(p => new { p.Name, p.Email, p.Phone, p.Organization })
        .ToList();

    // DB attachments for this disclosure
    var attachments = _context.DisclosureAttachments
        .AsNoTracking()
        .Where(a => a.DisclosureId == d.Id)
        .OrderByDescending(a => a.UploadedAt)
        .Select(a => new
        {
            Kind = "attachment",            // tag it
            a.Id,
            FileName = a.FileName,
            a.FileType,
            a.FileSize,
            Url = Url.Action("Download", "Files", new { id = a.Id })
        })
        .ToList();

    // Include the latest review report, if any
    var review = _context.Set<DisclosureReview>()
        .AsNoTracking()
        .Where(r => r.DisclosureId == d.Id && !string.IsNullOrWhiteSpace(r.ReportFilePath))
        .OrderByDescending(r => r.ReviewedAt)
        .FirstOrDefault();

    if (review != null)
    {
        // ReportFilePath should be a web path like /uploads/reviews/<guid>.<ext>
        var fileName = System.IO.Path.GetFileName(review.ReportFilePath);
        attachments.Add(new
        {
            Kind = "review",                // tag it for the view
            Id = 0,                         // not used
            FileName = fileName,
            FileType = "review",
            FileSize = 0L,
            Url = review.ReportFilePath     // direct link to static file
        });
    }

    ViewBag.Attachments = attachments;
    return PartialView("_ReviewExtras");
}

    // ============================
    // SUBMIT REVIEW (saves DisclosureReview)
    // ============================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitReview(
        string reference,
        string? reviewerNotes,
        IFormFile? reportFile,    // name MUST match input name in the form
        string? outcome           // Approved / Escalated / Closed (optional)
    )
    {
        if (string.IsNullOrWhiteSpace(reference))
            return BadRequest("Missing reference.");

        var disclosure = await _context.Disclosures
            .FirstOrDefaultAsync(d => d.DisclosureNumber == reference);

        if (disclosure == null)
            return NotFound("Disclosure not found.");

        // Save report (optional)
        string? reportRelativePath = null;
        if (reportFile != null && reportFile.Length > 0)
        {
            var folder = Path.Combine(_env.WebRootPath, "uploads", "reviews");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            var safeName = Path.GetFileName(reportFile.FileName);
            var newName = $"{Guid.NewGuid()}{Path.GetExtension(safeName)}";
            var physical = Path.Combine(folder, newName);

            await using (var fs = new FileStream(physical, FileMode.Create))
                await reportFile.CopyToAsync(fs);

            // store as web-relative path so you can link it later
            reportRelativePath = $"/uploads/reviews/{newName}";
        }

        // Upsert review row
        var reviewerId = await CurrentDbUserIdAsync();
        var review = await _context.Set<DisclosureReview>()
            .FirstOrDefaultAsync(r => r.DisclosureId == disclosure.Id);

        if (review == null)
        {
            review = new DisclosureReview
            {
                DisclosureId  = disclosure.Id,
                ReviewerId    = reviewerId,
                ReviewSummary = string.IsNullOrWhiteSpace(reviewerNotes) ? null : reviewerNotes.Trim(),
                ReportFilePath= reportRelativePath,
                Outcome       = string.IsNullOrWhiteSpace(outcome) ? null : outcome.Trim(),
                ReviewedAt    = DateTime.UtcNow
            };
            _context.Add(review);
        }
        else
        {
            review.ReviewerId    = reviewerId;
            review.ReviewSummary = string.IsNullOrWhiteSpace(reviewerNotes) ? review.ReviewSummary : reviewerNotes.Trim();
            if (!string.IsNullOrWhiteSpace(reportRelativePath))
                review.ReportFilePath = reportRelativePath;
            if (!string.IsNullOrWhiteSpace(outcome))
                review.Outcome = outcome.Trim();
            review.ReviewedAt = DateTime.UtcNow;
            _context.Update(review);
        }

        await _context.SaveChangesAsync();

        TempData["Message"] = $"Review for disclosure {reference} saved.";
        return RedirectToAction("Index", "Dashboard");
    }

    // ============================
    // CANCEL / REJECT
    // ============================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CancelDisclosure(string reference)
    {
        if (string.IsNullOrEmpty(reference))
            return BadRequest();

        var disclosure = _context.Disclosures.FirstOrDefault(d => d.DisclosureNumber == reference);
        if (disclosure == null)
            return NotFound();

        disclosure.Status = DisclosureStatus.Rejected;
        _context.SaveChanges();

        TempData["Message"] = $"Disclosure {reference} has been successfully Rejected.";
        return RedirectToAction("Index", "Dashboard");
    }

    // ============================
    // Helpers
    // ============================
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

        return 0;
    }
}

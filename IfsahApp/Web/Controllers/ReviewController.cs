using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting; // IWebHostEnvironment
using Microsoft.AspNetCore.Http;    // IFormFile
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
    public async Task<IActionResult> Index(string? reference, int page = 1, int pageSize = 10)
    {
        // Resolve current DB user robustly
        var dbUser = await CurrentDbUserAsync();
        var currentDbUserId = dbUser?.Id ?? 0;

        // Can see all if: identity role Admin OR DB role Admin OR custom claim perm=ReviewAll
        var canSeeAll = User.IsInRole("Admin")
                        || (dbUser?.Role == Role.Admin)
                        || User.HasClaim("perm", "ReviewAll");

        var query = _context.Disclosures
            .Include(d => d.DisclosureType)
            .Include(d => d.AssignedToUser)
            .OrderByDescending(d => d.SubmittedAt)
            .AsQueryable();

        if (!canSeeAll)
        {
            // Examiner-only view: only items assigned to them and in Assigned status
            if (currentDbUserId == 0)
            {
                // No DB user resolved -> nothing would match; return empty safely
                ViewBag.SelectedReference = reference;
                ViewBag.PageSize = pageSize;
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = 0;
                ViewBag.Message = TempData["Message"];
                return View(Enumerable.Empty<CaseItem>().ToList());
            }

            query = query.Where(d =>
                d.AssignedToUserId == currentDbUserId &&
                d.Status == DisclosureStatus.Assigned);
        }

        if (!string.IsNullOrWhiteSpace(reference))
        {
            query = query.Where(d => d.DisclosureNumber.Contains(reference));
        }

        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new CaseItem
            {
                Type        = d.DisclosureType != null ? d.DisclosureType.EnglishName : "N/A",
                Reference   = d.DisclosureNumber,
                Date        = d.SubmittedAt,
                Location    = d.Location ?? string.Empty,
                Status      = _enumLocalizer.LocalizeEnum(d.Status),
                Description = d.Description ?? string.Empty
            })
            .ToListAsync();

        ViewBag.SelectedReference = reference;
        ViewBag.PageSize = pageSize;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.Message = TempData["Message"];

        return View(items);
    }

    // ============================
    // REVIEW DISCLOSURE
    // ============================
    public async Task<IActionResult> ReviewDisclosure(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return NotFound();

        var disclosure = await _context.Disclosures
            .Include(d => d.DisclosureType)
            .FirstOrDefaultAsync(d => d.DisclosureNumber == reference);

        if (disclosure == null)
            return NotFound();

        // If user is Examiner (no Admin claim), enforce assignment ownership
        var isExaminerOnly = User.IsInRole("Examiner") && !User.IsInRole("Admin");
        if (isExaminerOnly)
        {
            var dbUser = await CurrentDbUserAsync();
            var currentDbUserId = dbUser?.Id ?? 0;
            if (disclosure.AssignedToUserId != currentDbUserId || disclosure.Status != DisclosureStatus.Assigned)
                return Forbid();
        }

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
        if (string.IsNullOrWhiteSpace(reference))
            return NotFound();

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
                Kind = "attachment",
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
            var fileName = System.IO.Path.GetFileName(review.ReportFilePath);
            attachments.Add(new
            {
                Kind = "review",
                Id = 0,
                FileName = fileName,
                FileType = "review",
                FileSize = 0L,
                Url = review.ReportFilePath
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

        // Examiner-only restriction
        var isExaminerOnly = User.IsInRole("Examiner") && !User.IsInRole("Admin");
        if (isExaminerOnly)
        {
            var dbUser = await CurrentDbUserAsync();
            var currentDbUserId = dbUser?.Id ?? 0;
            if (disclosure.AssignedToUserId != currentDbUserId || disclosure.Status != DisclosureStatus.Assigned)
                return Forbid();
        }

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

            reportRelativePath = $"/uploads/reviews/{newName}";
        }

        // Upsert review row
        var reviewer = await CurrentDbUserAsync();
        var reviewerId = reviewer?.Id ?? 0;

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
        return RedirectToAction("Index", "Review");
    }

    // ============================
    // CANCEL / REJECT
    // ============================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelDisclosure(string reference)
    {
        if (string.IsNullOrEmpty(reference))
            return BadRequest();

        var disclosure = await _context.Disclosures.FirstOrDefaultAsync(d => d.DisclosureNumber == reference);
        if (disclosure == null)
            return NotFound();

        // Examiner-only restriction
        var isExaminerOnly = User.IsInRole("Examiner") && !User.IsInRole("Admin");
        if (isExaminerOnly)
        {
            var dbUser = await CurrentDbUserAsync();
            var currentDbUserId = dbUser?.Id ?? 0;
            if (disclosure.AssignedToUserId != currentDbUserId || disclosure.Status != DisclosureStatus.Assigned)
                return Forbid();
        }

        disclosure.Status = DisclosureStatus.Rejected;
        await _context.SaveChangesAsync();

        TempData["Message"] = $"Disclosure {reference} has been successfully Rejected.";
        return RedirectToAction("Index", "Review");
    }

    // ============================
    // Helpers
    // ============================
    private async Task<User?> CurrentDbUserAsync()
    {
        // 1) Try NameIdentifier as int (DB Id)
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(idStr, out var id))
        {
            var byId = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (byId != null) return byId;
        }

        // 2) Try Email claim
        var email = User.FindFirstValue(ClaimTypes.Email);

        // 3) Try ADUserName via DOMAIN\username or plain username
        var rawName = User.Identity?.Name;
        string? adUser = null;
        if (!string.IsNullOrWhiteSpace(rawName))
            adUser = rawName.Contains('\\') ? rawName.Split('\\').Last() : rawName;

        // Single DB roundtrip that checks both
        return await _context.Users
            .FirstOrDefaultAsync(u =>
                (!string.IsNullOrWhiteSpace(email) && u.Email == email) ||
                (!string.IsNullOrWhiteSpace(adUser) && u.ADUserName != null && u.ADUserName.ToLower() == adUser.ToLower()));
    }
}

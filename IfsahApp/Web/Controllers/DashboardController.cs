using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Security.Claims;
using System.Threading.Tasks;
using IfsahApp.Infrastructure.Data;
using IfsahApp.Core.Enums;
using IfsahApp.Core.Models;
using IfsahApp.Core.ViewModels;
using IfsahApp.Infrastructure.Services; // IEnumLocalizer
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace IfsahApp.Web.Controllers;

[Authorize(Roles = "Admin")]
public class DashboardController(
    ApplicationDbContext context,
    IWebHostEnvironment env,
    IEnumLocalizer enumLocalizer
) : Controller
{
    private readonly ApplicationDbContext _context = context;
    private readonly IWebHostEnvironment _env = env;
    private readonly IEnumLocalizer _enumLocalizer = enumLocalizer;

    // GET: Dashboard
    public async Task<IActionResult> Index(
        string status = "All",
        string user = "",
        string reference = "",
        int page = 1,
        int pageSize = 5)
    {
        ViewBag.PageSize = pageSize;
        ViewBag.CurrentPage = page;
        ViewBag.SelectedReference = reference;
        ViewBag.SelectedStatus = status;

        var query = _context.Disclosures
            .Include(d => d.DisclosureType)
            .Include(d => d.AssignedToUser)
            .Include(d => d.SubmittedBy)
            .AsQueryable();

        if (!string.Equals(status, "All", StringComparison.OrdinalIgnoreCase) &&
            Enum.TryParse<DisclosureStatus>(status, true, out var enumStatus))
        {
            query = query.Where(d => d.Status == enumStatus);
        }

        if (!string.IsNullOrWhiteSpace(reference))
        {
            query = query.Where(d => d.DisclosureNumber.Contains(reference));
        }

        var raw = await query
            .OrderByDescending(d => d.SubmittedAt)
            .Select(d => new
            {
                d.Id,
                Reference = d.DisclosureNumber,
                TypeAr = d.DisclosureType != null ? d.DisclosureType.ArabicName : null,
                TypeEn = d.DisclosureType != null ? d.DisclosureType.EnglishName : null,
                d.SubmittedAt,
                d.Location,
                d.Status,
                d.Description
            })
            .AsNoTracking()
            .ToListAsync();

        var useArabic = CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "ar";
        var model = raw.Select(d => new DisclosureDashboardViewModel
        {
            Id = d.Id,
            Reference = d.Reference,
            Type = useArabic ? (d.TypeAr ?? d.TypeEn ?? "N/A") : (d.TypeEn ?? d.TypeAr ?? "N/A"),
            Date = d.SubmittedAt,
            Location = d.Location ?? string.Empty,
            Status = d.Status,
            Description = d.Description ?? string.Empty
        }).ToList();

        ViewBag.TotalPages = (int)Math.Ceiling((double)model.Count / pageSize);
        model = model.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var statusList = Enum.GetValues<DisclosureStatus>()
            .Select(s => new SelectListItem
            {
                Text = s.ToString(),
                Value = s.ToString(),
                Selected = s.ToString().Equals(status, StringComparison.OrdinalIgnoreCase)
            })
            .ToList();

        statusList.Insert(0, new SelectListItem
        {
            Text = "All",
            Value = "All",
            Selected = status.Equals("All", StringComparison.OrdinalIgnoreCase)
        });

        ViewBag.StatusList = statusList;

        return View(model);
    }

    public async Task<IActionResult> Details(int id)
    {
        var disclosure = await _context.Disclosures
            .Include(d => d.DisclosureType)
            .Include(d => d.SubmittedBy)
            .Include(d => d.AssignedToUser)
            .Include(d => d.Comments)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (disclosure == null) return NotFound();

        ViewBag.Disclosers = await _context.Users
            .Where(u => u.IsActive && u.Role == Role.Examiner)
            .AsNoTracking()
            .ToListAsync();

        return View(disclosure);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int disclosureId, string? commentText, int? assignToDiscloserId)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
            return RedirectToAction("AccessDenied", "Account");

        var disclosure = await _context.Disclosures.FindAsync(disclosureId);
        if (disclosure == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(commentText))
        {
            _context.Comments.Add(new Comment
            {
                DisclosureId = disclosureId,
                Text = commentText,
                AuthorId = currentUser.Id,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (assignToDiscloserId.HasValue)
        {
            var validAssignee = await _context.Users
                .AnyAsync(u => u.Id == assignToDiscloserId.Value && u.IsActive && u.Role == Role.Examiner);

            if (validAssignee)
            {
                disclosure.AssignedToUserId = assignToDiscloserId.Value;
                disclosure.Status = DisclosureStatus.Assigned;
            }
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "تم حفظ التعيين/التعليق.";
        return RedirectToAction(nameof(Details), new { id = disclosureId });
    }

    [HttpGet]
    public IActionResult ReviewDisclosure(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return NotFound();

        var disclosure = _context.Disclosures
            .Include(d => d.DisclosureType)
            .FirstOrDefault(d => d.DisclosureNumber == reference);

        if (disclosure == null) return NotFound();

        var caseItem = new CaseItem
        {
            Type = disclosure.DisclosureType?.EnglishName ?? "N/A",
            Reference = disclosure.DisclosureNumber,
            Date = disclosure.SubmittedAt,
            Location = disclosure.Location ?? string.Empty,
            Status = _enumLocalizer.LocalizeEnum(disclosure.Status),
            Description = disclosure.Description ?? string.Empty
        };

        ViewBag.Examiners = _context.Users
            .Where(u => u.IsActive && u.Role == Role.Examiner)
            .Select(u => new SelectListItem
            {
                Value = u.Id.ToString(),
                Text = u.FullName ?? u.Email
            })
            .ToList();

        return View(caseItem);
    }

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

        ViewBag.Suspected = d.SuspectedPeople
            .Select(p => new { p.Name, p.Email, p.Phone, p.Organization })
            .ToList();

        ViewBag.Related = d.RelatedPeople
            .Select(p => new { p.Name, p.Email, p.Phone, p.Organization })
            .ToList();

        var attachments = _context.DisclosureAttachments
            .AsNoTracking()
            .Where(a => a.DisclosureId == d.Id)
            .OrderByDescending(a => a.UploadedAt)
            .Select(a => new
            {
                Kind = "attachment",
                a.Id,
                a.FileName,
                a.FileType,
                a.FileSize,
                Url = Url.Action("Download", "Files", new { id = a.Id })
            })
            .ToList();

        var review = _context.Set<DisclosureReview>()
            .AsNoTracking()
            .Where(r => r.DisclosureId == d.Id && !string.IsNullOrWhiteSpace(r.ReportFilePath))
            .OrderByDescending(r => r.ReviewedAt)
            .FirstOrDefault();

        if (review != null)
        {
            var fileName = Path.GetFileName(review.ReportFilePath);
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitReview(
        string reference,
        string? reviewerNotes,
        IFormFile? reportFile,
        string? outcome,
        int? assignToDiscloserId
    )
    {
        if (string.IsNullOrWhiteSpace(reference))
            return BadRequest("Missing reference.");

        var disclosure = await _context.Disclosures
            .FirstOrDefaultAsync(d => d.DisclosureNumber == reference);

        if (disclosure == null)
            return NotFound("Disclosure not found.");

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

        var reviewer = await GetCurrentUserAsync();
        if (reviewer == null) return RedirectToAction("AccessDenied", "Account");

        var review = await _context.Set<DisclosureReview>()
            .FirstOrDefaultAsync(r => r.DisclosureId == disclosure.Id);

        if (review == null)
        {
            review = new DisclosureReview
            {
                DisclosureId = disclosure.Id,
                ReviewerId = reviewer.Id,
                ReviewSummary = string.IsNullOrWhiteSpace(reviewerNotes) ? null : reviewerNotes.Trim(),
                ReportFilePath = reportRelativePath,
                Outcome = string.IsNullOrWhiteSpace(outcome) ? null : outcome.Trim(),
                ReviewedAt = DateTime.UtcNow
            };
            _context.Add(review);
        }
        else
        {
            review.ReviewerId = reviewer.Id;
            review.ReviewSummary = string.IsNullOrWhiteSpace(reviewerNotes) ? review.ReviewSummary : reviewerNotes.Trim();
            if (!string.IsNullOrWhiteSpace(reportRelativePath)) review.ReportFilePath = reportRelativePath;
            if (!string.IsNullOrWhiteSpace(outcome)) review.Outcome = outcome.Trim();
            review.ReviewedAt = DateTime.UtcNow;
            _context.Update(review);
        }

        if (assignToDiscloserId.HasValue)
        {
            var validAssignee = await _context.Users
                .AnyAsync(u => u.Id == assignToDiscloserId.Value && u.IsActive && u.Role == Role.Examiner);

            if (validAssignee)
            {
                disclosure.AssignedToUserId = assignToDiscloserId.Value;
                disclosure.Status = DisclosureStatus.Assigned;
            }
        }

        await _context.SaveChangesAsync();

        TempData["Message"] = $"Review for disclosure {reference} saved.";
        return RedirectToAction("ReviewDisclosure", new { reference });
    }

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

    // Helper to get current user from AD
    private async Task<User?> GetCurrentUserAsync()
    {
        var adUserName = User.Identity?.Name?.Split('\\').Last();
        if (string.IsNullOrEmpty(adUserName)) return null;

        return await _context.Users.FirstOrDefaultAsync(u => u.ADUserName.ToLower() == adUserName.ToLower());
    }
}

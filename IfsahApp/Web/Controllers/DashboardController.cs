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
using Microsoft.AspNetCore.Http; // IFormFile
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
        // Set ViewBags for filter and pagination
        ViewBag.PageSize = pageSize;
        ViewBag.CurrentPage = page;
        ViewBag.SelectedReference = reference;
        ViewBag.SelectedStatus = status;

        // Base query
        var query = _context.Disclosures
            .Include(d => d.DisclosureType)
            .Include(d => d.AssignedToUser)
            .Include(d => d.SubmittedBy)
            .AsQueryable();

        // Filter by status
        if (!string.Equals(status, "All", StringComparison.OrdinalIgnoreCase) &&
            Enum.TryParse<DisclosureStatus>(status, true, out var enumStatus))
        {
            query = query.Where(d => d.Status == enumStatus);
        }

        // Filter by reference
        if (!string.IsNullOrWhiteSpace(reference))
        {
            query = query.Where(d => d.DisclosureNumber.Contains(reference));
        }

        // Materialize data
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

        // Map to ViewModel
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

        // Calculate total pages for pagination
        ViewBag.TotalPages = (int)Math.Ceiling((double)model.Count / pageSize);

        // Apply pagination
        model = model.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        // Prepare status dropdown
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

    // GET: Dashboard/Details/5
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

    // POST: Dashboard/AddComment
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(int disclosureId, string? commentText, int? assignToDiscloserId)
    {
        // اسم المستخدم من AD
        var adUserName = User.Identity?.Name?.Split('\\').Last();
        if (string.IsNullOrEmpty(adUserName))
            return RedirectToAction("AccessDenied", "Account");

        var currentUser = await _context.Users
            .FirstOrDefaultAsync(u => u.ADUserName.ToLower() == adUserName.ToLower());
        if (currentUser == null)
            return RedirectToAction("AccessDenied", "Account");

        var disclosure = await _context.Disclosures.FindAsync(disclosureId);
        if (disclosure == null) return NotFound();

        // أضف تعليق فقط إذا موجود
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

        // التعيين (اختياري)
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

    // ============================
    // REVIEW DISCLOSURE
    // ============================
    [HttpGet]
    public IActionResult ReviewDisclosure(string reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return NotFound();

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

        // قائمة الممتحنين لواجهة ReviewDisclosure
        ViewBag.Examiners = _context.Users
            .Where(u => u.IsActive && u.Role == Role.Examiner)
            .Select(u => new SelectListItem
            {
                Value = u.Id.ToString(),
                Text = u.FullName ?? u.Email
            })
            .ToList();

        return View(caseItem); // Views/Dashboard/ReviewDisclosure.cshtml
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
        return PartialView("_ReviewExtras"); // Views/Dashboard/_ReviewExtras.cshtml
    }

    // ============================
    // SUBMIT REVIEW (saves DisclosureReview)
    // ============================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitReview(
        string reference,
        string? reviewerNotes,
        IFormFile? reportFile,
        string? outcome,
        int? assignToDiscloserId // <--- تمت إضافتها لتمكين التعيين من شاشة المراجعة
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

        // ---- التعيين للممتحّن (اختياري من نفس النموذج) ----
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

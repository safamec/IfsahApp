// Controllers/DashboardController.cs
using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Collections.Generic;
using IfsahApp.Infrastructure.Data;
using IfsahApp.Infrastructure.Services.AdUser;
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
using ClosedXML.Excel;

namespace IfsahApp.Web.Controllers;

[Authorize]
public class DashboardController(
    ApplicationDbContext context,
    IWebHostEnvironment env,
    IEnumLocalizer enumLocalizer,
    ILogger<LdapAdUserService> logger
) : Controller
{
    private readonly ApplicationDbContext _context = context;
    private readonly IWebHostEnvironment _env = env;
    private readonly IEnumLocalizer _enumLocalizer = enumLocalizer;
    private readonly ILogger<LdapAdUserService> _logger = logger;

    // GET: Dashboard
   public async Task<IActionResult> Index(
    string status = "All",
    string user = "",
    string reference = "",
    DateTime? fromDate = null,
    DateTime? toDate = null,
    int page = 1,
    int pageSize = 5)
{

        ViewBag.PageSize = pageSize;
        ViewBag.CurrentPage = page;
        ViewBag.SelectedReference = reference;
        ViewBag.SelectedStatus = status;
        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");


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

    // جلب آخر Review عشان نظهر الـ Notes (من المختبر أو من الأدمن لو عدّلها)
    var review = _context.Set<DisclosureReview>()
        .AsNoTracking()
        .FirstOrDefault(r => r.DisclosureId == disclosure.Id);

    ViewBag.ReviewerNotes = review?.ReviewSummary;   // نفس الحقل المشترك

    var caseItem = new CaseItem
    {
        Type        = disclosure.DisclosureType?.EnglishName ?? "N/A",
        Reference   = disclosure.DisclosureNumber,
        Date        = disclosure.SubmittedAt,
        Location    = disclosure.Location ?? string.Empty,
        Status      = _enumLocalizer.LocalizeEnum(disclosure.Status),
        Description = disclosure.Description ?? string.Empty
    };

    ViewBag.Examiners = _context.Users
        .Where(u => u.IsActive && u.Role == Role.Examiner)
        .Select(u => new SelectListItem
        {
            Value = u.Id.ToString(),
            Text  = u.FullName ?? u.Email
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
    int? assignToDiscloserId
)
{
    if (string.IsNullOrWhiteSpace(reference))
        return BadRequest("Missing reference.");

    var disclosure = await _context.Disclosures
        .FirstOrDefaultAsync(d => d.DisclosureNumber == reference);

    if (disclosure == null)
        return NotFound("Disclosure not found.");

    var reviewer = await GetCurrentUserAsync();
    if (reviewer == null)
        return RedirectToAction("AccessDenied", "Account");

    // استخدام نفس ReviewSummary كملاحظات مشتركة
    var review = await _context.Set<DisclosureReview>()
        .FirstOrDefaultAsync(r => r.DisclosureId == disclosure.Id);

    if (review == null)
    {
        review = new DisclosureReview
        {
            DisclosureId  = disclosure.Id,
            ReviewerId    = reviewer.Id,
            ReviewSummary = string.IsNullOrWhiteSpace(reviewerNotes) ? null : reviewerNotes.Trim(),
            ReviewedAt    = DateTime.UtcNow
        };
        _context.Add(review);
    }
    else
    {
        review.ReviewerId = reviewer.Id;
        if (!string.IsNullOrWhiteSpace(reviewerNotes))
            review.ReviewSummary = reviewerNotes.Trim();
        review.ReviewedAt = DateTime.UtcNow;
        _context.Update(review);
    }

    // تعيين للمختبر (نفس السابق)
    if (assignToDiscloserId.HasValue)
    {
        var assigneeId = assignToDiscloserId.Value;

        var validAssignee = await _context.Users.AnyAsync(u =>
            u.Id == assigneeId &&
            u.IsActive &&
            u.Role == Role.Examiner);

        if (validAssignee)
        {
            disclosure.AssignedToUserId = assigneeId;
            disclosure.Status = DisclosureStatus.Assigned;
        }
    }
    else
    {
        disclosure.Status = DisclosureStatus.InReview;
    }

    await _context.SaveChangesAsync();

    TempData["Message"] = $"Review for disclosure {reference} saved.";
    return RedirectToAction("Index", "Dashboard");
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

    private async Task<User?> GetCurrentUserAsync()
    {
        var adUserName = User.Identity?.Name?.Split('\\').Last();
        if (string.IsNullOrEmpty(adUserName)) return null;

        return await _context.Users.FirstOrDefaultAsync(u => u.ADUserName.ToLower() == adUserName.ToLower());
    }

    [AllowAnonymous]
    public IActionResult DashboardSummary(DateTime? fromDate, DateTime? toDate, int? year, int? month)
    {
        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate   = toDate?.ToString("yyyy-MM-dd");
        ViewBag.SelectedYear  = year;
        ViewBag.SelectedMonth = month;

        ViewBag.Years = _context.Disclosures
            .Where(d => d.IncidentStartDate.HasValue)
            .Select(d => d.IncidentStartDate!.Value.Year)
            .Distinct()
            .OrderBy(y => y)
            .ToList();

        var query = _context.Disclosures
            .Where(d => d.IncidentStartDate.HasValue)
            .AsQueryable();

        if (fromDate.HasValue)
        {
            DateTime from = fromDate.Value.Date;
            query = query.Where(d => d.IncidentStartDate!.Value.Date >= from);
        }

        if (toDate.HasValue)
        {
            DateTime to = toDate.Value.Date.AddDays(1);
            query = query.Where(d => d.IncidentStartDate!.Value < to);
        }

        if (year.HasValue)
        {
            query = query.Where(d => d.IncidentStartDate!.Value.Year == year.Value);
        }

        if (month.HasValue)
        {
            query = query.Where(d => d.IncidentStartDate!.Value.Month == month.Value);
        }

        var summary = query
            .AsEnumerable()
            .GroupBy(d => new
            {
                Y = d.IncidentStartDate!.Value.Year,
                M = d.IncidentStartDate!.Value.Month
            })
            .OrderBy(g => g.Key.Y)
            .ThenBy(g => g.Key.M)
            .Select(g =>
            {
                var dt = new DateTime(g.Key.Y, g.Key.M, 1);

                return new DashboardSummaryViewModel
                {
                    Month = dt.ToString("yyyy/MM/dd"),
                    NumberOfDisclosures = g.Count(),
                    UnderReview = g.Count(d => d.Status == DisclosureStatus.InReview),
                    UnderExamination = g.Count(d => d.Status == DisclosureStatus.Assigned),
                    Completed = g.Count(d => d.Status == DisclosureStatus.Completed),
                    Rejected = g.Count(d => d.Status == DisclosureStatus.Rejected)
                };
            })
            .ToList();

        foreach (var row in summary)
        {
            row.NewRequests = row.NumberOfDisclosures
                             - row.Completed
                             - row.Rejected
                             - row.UnderReview
                             - row.UnderExamination;
        }

        return View(summary);
    }

    [AllowAnonymous]
    public IActionResult ExportSummaryToExcel(DateTime? fromDate, DateTime? toDate, int? year, int? month)
    {
        var data = DashboardSummary(fromDate, toDate, year, month) as ViewResult;
        var model = data?.Model as IEnumerable<DashboardSummaryViewModel>;

        var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Summary");

        ws.Cell(1, 1).Value = "Date";
        ws.Cell(1, 2).Value = "Total";
        ws.Cell(1, 3).Value = "New";
        ws.Cell(1, 4).Value = "Under Review";
        ws.Cell(1, 5).Value = "Under Examination";
        ws.Cell(1, 6).Value = "Completed";
        ws.Cell(1, 7).Value = "Rejected";

        int row = 2;

        foreach (var d in model!)
        {
            ws.Cell(row, 1).Value = d.Month;
            ws.Cell(row, 2).Value = d.NumberOfDisclosures;
            ws.Cell(row, 3).Value = d.NewRequests;
            ws.Cell(row, 4).Value = d.UnderReview;
            ws.Cell(row, 5).Value = d.UnderExamination;
            ws.Cell(row, 6).Value = d.Completed;
            ws.Cell(row, 7).Value = d.Rejected;
            row++;
        }

        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        var content = stream.ToArray();

        return File(content,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "DashboardSummary.xlsx");
    }
}

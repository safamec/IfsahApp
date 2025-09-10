using System.Globalization;
using IfsahApp.Infrastructure.Data;
using IfsahApp.Core.Enums;
using IfsahApp.Core.Models;
using IfsahApp.Core.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using IfsahApp.Core.Enums;
using Microsoft.AspNetCore.Authorization;

namespace IfsahApp.Web.Controllers;

[Authorize(Roles = "Admin")]
public class DashboardController(ApplicationDbContext context) : Controller
{
    private readonly ApplicationDbContext _context = context;

    // GET: Dashboard
    public async Task<IActionResult> Index(string status = "All")
    {
        var query = _context.Disclosures
            .Include(d => d.DisclosureType)
            .AsQueryable();

        if (!string.Equals(status, "All", StringComparison.OrdinalIgnoreCase) &&
            Enum.TryParse<DisclosureStatus>(status, true, out var enumStatus))
        {
            query = query.Where(d => d.Status == enumStatus);
        }

        // Materialize first to avoid EF translating CultureInfo/DisplayName
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
            .Include(d => d.Comments).ThenInclude(c => c.Author)
            .AsNoTracking()
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
    public async Task<IActionResult> AddComment(int disclosureId, string commentText, int? assignToDiscloserId)
    {
        if (string.IsNullOrWhiteSpace(commentText))
            return RedirectToAction(nameof(Details), new { id = disclosureId });

        // Logged-in AD username (DOMAIN\samAccountName)
        var adUserName = User.Identity?.Name?.Split('\\').LastOrDefault();
        if (string.IsNullOrEmpty(adUserName))
            return RedirectToAction("AccessDenied", "Account");

        var currentUser = await _context.Users
            .FirstOrDefaultAsync(u => u.ADUserName.ToLower() == adUserName.ToLower());
        if (currentUser == null)
            return RedirectToAction("AccessDenied", "Account");

        _context.Comments.Add(new Comment
        {
            DisclosureId = disclosureId,
            Text = commentText,
            AuthorId = currentUser.Id,
            CreatedAt = DateTime.UtcNow
        });

        if (assignToDiscloserId.HasValue)
        {
            var disclosure = await _context.Disclosures.FindAsync(disclosureId);
            if (disclosure != null)
                disclosure.AssignedToUserId = assignToDiscloserId.Value;
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = disclosureId });
    }
}
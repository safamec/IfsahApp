using System.Globalization;
using IfsahApp.Infrastructure.Data;
using IfsahApp.Core.Enums;
using IfsahApp.Core.Models;
using IfsahApp.Core.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace IfsahApp.Web.Controllers;

[Authorize(Roles = "Admin")]
public class DashboardController(ApplicationDbContext context) : Controller
{
    private readonly ApplicationDbContext _context = context;

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
    public async Task<IActionResult> AddComment(int disclosureId, string commentText, int? assignToDiscloserId)
    {
        if (string.IsNullOrWhiteSpace(commentText))
            return RedirectToAction(nameof(Details), new { id = disclosureId });

        // Get logged-in AD username
        var adUserName = User.Identity?.Name?.Split('\\').Last();
        if (string.IsNullOrEmpty(adUserName))
            return RedirectToAction("AccessDenied", "Account");

        // Find user in DB
        var currentUser = await _context.Users
            .FirstOrDefaultAsync(u => u.ADUserName.ToLower() == adUserName.ToLower());
        if (currentUser == null)
            return RedirectToAction("AccessDenied", "Account");

        // Add comment
        _context.Comments.Add(new Comment
        {
            DisclosureId = disclosureId,
            Text = commentText,
            AuthorId = currentUser.Id,
            CreatedAt = DateTime.UtcNow
        });

        // Assign disclosure if needed
        if (assignToDiscloserId.HasValue)
        {
            var disclosure = await _context.Disclosures.FindAsync(disclosureId);
            if (disclosure != null)
            {
                disclosure.AssignedToUserId = assignToDiscloserId.Value;
            }
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = disclosureId });
    }
}

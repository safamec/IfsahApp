using IfsahApp.Data;
using IfsahApp.Enums;
using IfsahApp.Services;
using IfsahApp.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace IfsahApp.Controllers;

public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IEnumLocalizer _enumLocalizer;
    private readonly IStringLocalizer<SharedResource> _sharedLocalizer;


    public DashboardController(ApplicationDbContext context,
     IEnumLocalizer enumLocalizer, IStringLocalizer<SharedResource> sharedLocalizer)
    {
        _context = context;
        _enumLocalizer = enumLocalizer;
        _sharedLocalizer = sharedLocalizer;
    }

    public async Task<IActionResult> Index(string status = "All")
    {
        var query = _context.Disclosures.Include(d => d.DisclosureType).AsQueryable();

        if (!string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
        {
            if (Enum.TryParse<DisclosureStatus>(status, out var enumStatus))
            {
                query = query.Where(d => d.Status == enumStatus);
            }
        }

        var model = await query
        .OrderByDescending(d => d.SubmittedAt)
        .Select(d => new DisclosureDashboardViewModel
        {
            Id = d.Id,
            Reference = d.DisclosureNumber,
            Type = d.DisclosureType.Name,
            Date = d.SubmittedAt,
            Location = d.Location,
            Status = d.Status, // keep enum
            Description = d.Description
        })
        .ToListAsync();

        // Build enum dropdown
        var statusList = Enum.GetValues(typeof(DisclosureStatus))
            .Cast<DisclosureStatus>()
            .Select(s => new SelectListItem
            {
                Text = _enumLocalizer.LocalizeEnum(s), // localized enum
                Value = s.ToString(),
                Selected = s.ToString() == status
            })
            .ToList();

        // Add "All" option using IViewLocalizer
        statusList.Insert(0, new SelectListItem
        {
            Text = _sharedLocalizer["AllStatus"], // localized "All"
            Value = "All",
            Selected = string.Equals(status, "All", StringComparison.OrdinalIgnoreCase)
        });

        ViewBag.StatusList = statusList;

        return View(model);
    }
}


using IfsahApp.Data;
using IfsahApp.Models;
using IfsahApp.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace IfsahApp.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Dashboard
        public async Task<IActionResult> Index(string status = "All")
        {
            var query = _context.Disclosures
                .Include(d => d.DisclosureType)
                .AsQueryable();

            if (!string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
            {
                if (Enum.TryParse<Enums.DisclosureStatus>(status, out var enumStatus))
                {
                    query = query.Where(d => d.Status == enumStatus);
                }
            }

            // Map to ViewModel
            var model = await query
                .OrderByDescending(d => d.SubmittedAt)
                .Select(d => new DisclosureDashboardViewModel
                {
                    Id = d.Id,
                    Reference = d.DisclosureNumber,
                    Type = d.DisclosureType.Name,
                    Date = d.SubmittedAt,
                    Location = d.Location,
                    Status = d.Status,
                    Description = d.Description
                })
                .ToListAsync();

            // Build status filter dropdown
            var statusList = Enum.GetValues(typeof(Enums.DisclosureStatus))
                .Cast<Enums.DisclosureStatus>()
                .Select(s => new SelectListItem
                {
                    Text = s.ToString(),
                    Value = s.ToString(),
                    Selected = s.ToString() == status
                })
                .ToList();

            statusList.Insert(0, new SelectListItem
            {
                Text = "All",
                Value = "All",
                Selected = status == "All"
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
                .Include(d => d.Comments) // now Author is string
                .FirstOrDefaultAsync(d => d.Id == id);

            if (disclosure == null)
                return NotFound();

            // List of active disclosers for assignment dropdown
            ViewBag.Disclosers = await _context.Users
                .Where(u => u.IsActive && u.Role == "Discloser")
                .ToListAsync();

            return View(disclosure);
        }

        // POST: Dashboard/AddComment
        [HttpPost]
        public async Task<IActionResult> AddComment(int disclosureId, string commentText, int? assignToDiscloserId)
        {
            if (string.IsNullOrWhiteSpace(commentText))
                return RedirectToAction(nameof(Details), new { id = disclosureId });

            // Add comment
            var comment = new Comment
            {
                DisclosureId = disclosureId,
                Text = commentText,
                Author = "Admin Name", // replace with actual logged-in admin name
                CreatedAt = DateTime.UtcNow
            };
            _context.Comments.Add(comment);

            // Assign disclosure to discloser if selected
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
}

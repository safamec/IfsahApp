using IfsahApp.Data;
using IfsahApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace IfsahApp.Controllers
{
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string status = "All")
        {
            var query = _context.Disclosures
                                .Include(d => d.DisclosureType)
                                .AsQueryable();

            // Filter by Status
            if (!string.IsNullOrEmpty(status) && status != "All")
            {
                query = query.Where(d => d.Status == status);
            }

            var model = await query.Select(d => new DisclosureDashboardViewModel
            {
                Reference = d.DisclosureNumber,
                Type = d.DisclosureType.Name,
                Date = d.SubmittedAt,
                Location = d.Location,
                Status = d.Status,
                // Audit removed
                ActionUrl = Url.Action("Details", "Disclosure", new { id = d.Id }),
                EditUrl = Url.Action("Edit", "Disclosure", new { id = d.Id })
            }).ToListAsync();

            ViewData["SelectedStatus"] = status;

            return View(model);
        }
    }
}

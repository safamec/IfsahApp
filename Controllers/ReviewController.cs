using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IfsahApp.Data;
using IfsahApp.Models;

namespace IfsahApp.Controllers
{
    public class ReviewController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReviewController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Get all disclosures including their type
            var cases = await _context.Disclosures
                .Include(d => d.DisclosureType)
                .OrderByDescending(d => d.SubmittedAt) // newest first
                .Select(d => new CaseItem
                {
                    Type = d.DisclosureType.Name,
                    Reference = d.DisclosureNumber,
                    Date = d.SubmittedAt,
                    Location = d.Location,
                    Status = d.Status,
                    Description = d.Description
                })
                .ToListAsync();

            return View(cases);
        }
    }
}

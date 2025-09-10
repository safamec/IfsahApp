using IfsahApp.Core.Models;
using IfsahApp.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IfsahApp.Web.Controllers
{
    [Authorize]
    [Route("DisclosureTypes")]   // allow plural base URL
    [Route("DisclosureType")]    // (optional) also allow singular
    public class DisclosureTypeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DisclosureTypeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /DisclosureTypes
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var items = await _context.DisclosureTypes.ToListAsync();
            return View(items);
        }

        // GET: /DisclosureTypes/Details/5
        [HttpGet("Details/{id:int}")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();
            var disclosureType = await _context.DisclosureTypes.FirstOrDefaultAsync(m => m.Id == id);
            if (disclosureType == null) return NotFound();
            return View(disclosureType);
        }

        // GET: /DisclosureTypes/Create
        [HttpGet("Create")]
        public IActionResult Create() => View();

        // POST: /DisclosureTypes/Create
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DisclosureType disclosureType)
        {
            if (!ModelState.IsValid) return View(disclosureType);
            _context.Add(disclosureType);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: /DisclosureTypes/Edit/5
        [HttpGet("Edit/{id:int}")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var disclosureType = await _context.DisclosureTypes.FindAsync(id);
            if (disclosureType == null) return NotFound();
            return View(disclosureType);
        }

        // POST: /DisclosureTypes/Edit/5
        [HttpPost("Edit/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, DisclosureType disclosureType)
        {
            if (id != disclosureType.Id) return NotFound();
            if (!ModelState.IsValid) return View(disclosureType);

            try
            {
                _context.Update(disclosureType);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.DisclosureTypes.Any(e => e.Id == disclosureType.Id)) return NotFound();
                throw;
            }
            return RedirectToAction(nameof(Index));
        }
        // POST: /DisclosureTypes/ToggleActive/5 (AJAX)
        [HttpPost("ToggleActive/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var type = await _context.DisclosureTypes.FindAsync(id);
            if (type == null) return NotFound();

            type.IsActive = !type.IsActive;
            await _context.SaveChangesAsync();

            return Json(new
            {
                ok = true,
                id,
                isActive = type.IsActive,
                text = type.IsActive ? "✓ Active" : "✗ Inactive",
                btnClass = type.IsActive ? "btn-outline-success" : "btn-outline-secondary"
            });
        }


        
    }
}

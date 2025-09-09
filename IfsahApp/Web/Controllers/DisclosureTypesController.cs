using IfsahApp.Core.Models;
using IfsahApp.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IfsahApp.Core.Models
{

    [Authorize]
    public class DisclosureTypeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DisclosureTypeController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: DisclosureType
        public async Task<IActionResult> Index()
        {
            var items = await _context.DisclosureTypes.ToListAsync();
            return View(items);
        }

        // GET: DisclosureType/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var disclosureType = await _context.DisclosureTypes
                .FirstOrDefaultAsync(m => m.Id == id);
            if (disclosureType == null) return NotFound();

            return View(disclosureType);
        }

        // GET: DisclosureType/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: DisclosureType/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DisclosureType disclosureType)
        {
            if (ModelState.IsValid)
            {
                _context.Add(disclosureType);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(disclosureType);
        }

        // GET: DisclosureType/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var disclosureType = await _context.DisclosureTypes.FindAsync(id);
            if (disclosureType == null) return NotFound();

            return View(disclosureType);
        }

        // POST: DisclosureType/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, DisclosureType disclosureType)
        {
            if (id != disclosureType.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(disclosureType);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DisclosureTypeExists(disclosureType.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(disclosureType);
        }

        // GET: DisclosureType/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var disclosureType = await _context.DisclosureTypes
                .FirstOrDefaultAsync(m => m.Id == id);
            if (disclosureType == null) return NotFound();

            return View(disclosureType);
        }

        // POST: DisclosureType/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var disclosureType = await _context.DisclosureTypes.FindAsync(id);
            if (disclosureType != null)
            {
                _context.DisclosureTypes.Remove(disclosureType);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool DisclosureTypeExists(int id)
        {
            return _context.DisclosureTypes.Any(e => e.Id == id);
        }
    }
}


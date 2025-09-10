using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using IfsahApp.Infrastructure.Data;

[Authorize]
public class NotificationsController : Controller
{
    private readonly ApplicationDbContext _db;
    public NotificationsController(ApplicationDbContext db) => _db = db;

    // Resolve the *DB* User.Id for current principal (by numeric id, then Email, then ADUserName/User.Identity.Name)
    private async Task<int> CurrentDbUserIdAsync()
    {
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(idStr, out var id))
        {
            if (await _db.Users.AnyAsync(u => u.Id == id)) return id;
        }

        var email = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(email))
        {
            var found = await _db.Users.Where(u => u.Email == email).Select(u => u.Id).FirstOrDefaultAsync();
            if (found != 0) return found;
        }

        // Try AD username (sAM/UPN)
        var ad = User.FindFirstValue(ClaimTypes.WindowsAccountName) ?? User.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(ad))
        {
            var found = await _db.Users.Where(u => u.ADUserName == ad).Select(u => u.Id).FirstOrDefaultAsync();
            if (found != 0) return found;
        }

        return 0;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var uid = await CurrentDbUserIdAsync();
        var items = await _db.Notifications
            .Where(n => n.RecipientId == uid)
            .OrderByDescending(n => n.CreatedAt)
            .Take(200)
            .ToListAsync();
        return View(items);
    }

    // Returns a LIST (what your JS expects)
    [HttpGet]
    public async Task<IActionResult> Unread()
    {
        var uid = await CurrentDbUserIdAsync();
        var items = await _db.Notifications
            .Where(n => n.RecipientId == uid && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new {
                id = n.Id,
                eventType = n.EventType,
                message = n.Message,
                createdAt = n.CreatedAt,
                url = (string?)null
            })
            .ToListAsync();
        return Json(items);
    }

    [HttpGet]
    public async Task<IActionResult> Feed([FromQuery] int take = 20)
    {
        var uid = await CurrentDbUserIdAsync();
        var items = await _db.Notifications
            .Where(n => n.RecipientId == uid)
            .OrderByDescending(n => n.CreatedAt)
            .Take(Math.Clamp(take, 1, 200))
            .Select(n => new {
                id = n.Id,
                eventType = n.EventType,
                message = n.Message,
                createdAt = n.CreatedAt,
                url = (string?)null
            })
            .ToListAsync();
        return Json(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead([FromQuery] int id)
    {
        var uid = await CurrentDbUserIdAsync();
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.RecipientId == uid);
        if (n == null) return NotFound();
        if (!n.IsRead) { n.IsRead = true; await _db.SaveChangesAsync(); }
        return Ok();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        var uid = await CurrentDbUserIdAsync();
        var notes = await _db.Notifications.Where(n => n.RecipientId == uid && !n.IsRead).ToListAsync();
        if (notes.Count > 0) { notes.ForEach(n => n.IsRead = true); await _db.SaveChangesAsync(); }
        TempData["Msg"] = "All notifications marked as read.";
        return RedirectToAction(nameof(Index));
    }

    // Aliases used by your JS
    [HttpPost("Notifications/MarkAllAsRead")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> MarkAllAsRead() => MarkAllRead();

    [HttpPost("Notifications/MarkRead")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> MarkRead([FromQuery] int id) => MarkAsRead(id);
}

using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using IfsahApp.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IfsahApp.Web.Controllers
{
    [Authorize]
    [Route("[controller]")]
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _db;
        public NotificationsController(ApplicationDbContext db) => _db = db;

        // Resolve current user -> Users.Id (Id -> Email -> Name(email) -> ADUserName)
        private async Task<int> CurrentDbUserIdAsync()
        {
            // 1) NameIdentifier as int
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(idStr, out var numericId) && numericId > 0)
            {
                if (await _db.Users.AnyAsync(u => u.Id == numericId)) return numericId;
            }

            // 2) Email claim
            var email = User.FindFirstValue(ClaimTypes.Email);
            if (!string.IsNullOrWhiteSpace(email))
            {
                var byEmail = await _db.Users.Where(u => u.Email == email)
                                             .Select(u => u.Id).FirstOrDefaultAsync();
                if (byEmail != 0) return byEmail;
            }

            // 3) Identity.Name -> email or DOMAIN\user or user
            var name = User.Identity?.Name ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(name))
            {
                if (name.Contains("@"))
                {
                    var byNameEmail = await _db.Users.Where(u => u.Email == name)
                                                     .Select(u => u.Id).FirstOrDefaultAsync();
                    if (byNameEmail != 0) return byNameEmail;
                }

                var simple = name.Contains('\\') ? name.Split('\\').Last() : name;
                var byAd = await _db.Users.Where(u => u.ADUserName == simple)
                                          .Select(u => u.Id).FirstOrDefaultAsync();
                if (byAd != 0) return byAd;
            }

            // 4) WindowsAccountName claim
            var sam = User.FindFirstValue(ClaimTypes.WindowsAccountName);
            if (!string.IsNullOrWhiteSpace(sam))
            {
                var simpleSam = sam.Contains('\\') ? sam.Split('\\').Last() : sam;
                var bySam = await _db.Users.Where(u => u.ADUserName == simpleSam)
                                           .Select(u => u.Id).FirstOrDefaultAsync();
                if (bySam != 0) return bySam;
            }

            return 0;
        }

        private static bool IsAjax(HttpRequest r) =>
            string.Equals(r.Headers["X-Requested-With"], "XMLHttpRequest", System.StringComparison.OrdinalIgnoreCase) ||
            r.Headers["Accept"].ToString().Contains("application/json");

        // GET: /Notifications
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var uid = await CurrentDbUserIdAsync();
            if (uid == 0) return Challenge(); // redirect to login page via cookie auth

            var items = await _db.Notifications
                .Where(n => n.RecipientId == uid)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return View(items);
        }

        // POST: /Notifications/MarkAsRead (per-row)
        [HttpPost("MarkAsRead")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsRead([FromQuery] int id)
        {
            var uid = await CurrentDbUserIdAsync();
            if (uid == 0) return Challenge();

            var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.RecipientId == uid);
            if (n == null) return NotFound();

            if (!n.IsRead)
            {
                n.IsRead = true;
                await _db.SaveChangesAsync();
            }

            if (IsAjax(Request)) return Ok(new { ok = true });
            return RedirectToAction(nameof(Index));
        }

        // POST: /Notifications/MarkAllRead (header button)
        [HttpPost("MarkAllRead")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            var uid = await CurrentDbUserIdAsync();
            if (uid == 0) return Challenge();

            var notes = await _db.Notifications.Where(n => n.RecipientId == uid && !n.IsRead).ToListAsync();
            if (notes.Count > 0)
            {
                notes.ForEach(n => n.IsRead = true);
                await _db.SaveChangesAsync();
            }

            if (IsAjax(Request)) return Ok(new { ok = true });
            return RedirectToAction(nameof(Index));
        }

        // GET: /Notifications/UnreadCount (for navbar badge)
        [HttpGet("UnreadCount")]
        public async Task<IActionResult> UnreadCount()
        {
            var uid = await CurrentDbUserIdAsync();
            if (uid == 0) return Unauthorized();

            var count = await _db.Notifications
                .CountAsync(n => n.RecipientId == uid && !n.IsRead);

            return Json(new { count });
        }

        // GET: /Notifications/Unread (for navbar list)
        [HttpGet("Unread")]
        public async Task<IActionResult> Unread()
        {
            var uid = await CurrentDbUserIdAsync();
            if (uid == 0) return Unauthorized();

            var items = await _db.Notifications
                .Where(n => n.RecipientId == uid && !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new
                {
                    id = n.Id,
                    title = n.EventType,
                    message = n.Message,
                    createdAt = n.CreatedAt
                })
                .ToListAsync();

            return Json(items);
        }
    }
}

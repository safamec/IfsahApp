// File: Controllers/NotificationsController.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;

using IfsahApp.Core.Models;                 // Notification model (your namespace)
using IfsahApp.Infrastructure.Data;         // ApplicationDbContext (your DI-registered context)
using IfsahApp.Hubs;                        // NotificationHub

namespace IfsahApp.Controllers
{
    // NOTE: لو حابة تقيّدها للأدمن فقط، بدّلي السطر التالي إلى [Authorize(Roles = "Admin")]
    [Authorize]
    [Route("[controller]")]
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IHubContext<NotificationHub> _hub;

        public NotificationsController(ApplicationDbContext db, IHubContext<NotificationHub> hub)
        {
            _db = db;
            _hub = hub;
        }

        // =========================================================
        // Helpers
        // =========================================================
        private int ResolveCurrentUserId(int? fallbackFromQuery = null)
        {
            // 1) حاول Claim باسم UserId (لو أنتي ضايفته)
            var custom = User.FindFirst("UserId")?.Value;
            if (int.TryParse(custom, out var cid) && cid > 0) return cid;

            // 2) حاول Claim القياسي NameIdentifier
            var std = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(std, out var sid) && sid > 0) return sid;

            // 3) استخدم fallback من الـ query لو موجود
            if (fallbackFromQuery.HasValue && fallbackFromQuery.Value > 0) return fallbackFromQuery.Value;

            // 4) آخر حل: خذي ADUserName من User.Identity.Name وابحثي في Db.Users
            var rawName = User?.Identity?.Name; // DOMAIN\username
            var adUserName = (rawName?.Split('\\').LastOrDefault() ?? "").ToLower();
            if (!string.IsNullOrWhiteSpace(adUserName))
            {
                var user = _db.Users.AsNoTracking()
                                    .FirstOrDefault(u => (u.ADUserName ?? "").ToLower() == adUserName);
                if (user != null) return user.Id;
            }

            return 0; // مجهول
        }

        // =========================================================
        // 1) MVC View: /Notifications  (قائمة الإشعارات للمستخدم الحالي)
        // =========================================================
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var userId = ResolveCurrentUserId();
            if (userId == 0) return Unauthorized();

            var items = await _db.Notifications
                .Where(n => n.RecipientId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            // View تتوقع: @model IEnumerable<IfsahApp.Core.Models.Notification>
            return View(items);
        }

        // =========================================================
        // 2) Actions لزرّيك في الـ View
        // =========================================================

        // POST: /Notifications/MarkAsRead?id=123    (من جدول الـ View)
        [HttpPost("MarkAsRead")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var userId = ResolveCurrentUserId();
            if (userId == 0) return Unauthorized();

            var notif = await _db.Notifications
                                 .FirstOrDefaultAsync(n => n.Id == id && n.RecipientId == userId);
            if (notif == null) return NotFound();

            if (!notif.IsRead)
            {
                notif.IsRead = true;
                await _db.SaveChangesAsync();
            }

            return Ok();
        }

        // POST: /Notifications/MarkAllAsRead
        [HttpPost("MarkAllAsRead")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = ResolveCurrentUserId();
            if (userId == 0) return Unauthorized();

            var notifs = await _db.Notifications
                                  .Where(n => n.RecipientId == userId && !n.IsRead)
                                  .ToListAsync();

            if (notifs.Count > 0)
            {
                foreach (var n in notifs) n.IsRead = true;
                await _db.SaveChangesAsync();
            }

            return Ok();
        }

        // GET: /Notifications/UnreadCount   (لشريط الجرس في النافبار)
        [HttpGet("UnreadCount")]
        public async Task<IActionResult> UnreadCount()
        {
            var userId = ResolveCurrentUserId();
            if (userId == 0) return Unauthorized();

            var count = await _db.Notifications
                                 .Where(n => n.RecipientId == userId && !n.IsRead)
                                 .CountAsync();

            return Json(new { count });
        }

        // GET: /Notifications/Feed?take=20   (JSON بسيط لو حبّيتي رندر بالـ JS)
        [HttpGet("Feed")]
        public async Task<IActionResult> Feed(int take = 20)
        {
            var userId = ResolveCurrentUserId();
            if (userId == 0) return Unauthorized();

            take = Math.Clamp(take, 1, 100);

            var data = await _db.Notifications
                .Where(n => n.RecipientId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(take)
                .Select(n => new
                {
                    n.Id,
                    n.EventType,
                    n.Message,
                    n.IsRead,
                    createdAt = n.CreatedAt       // سيُسلسل كـ ISO 8601
                })
                .ToListAsync();

            return Json(data);
        }

        // =========================================================
        // 3) Endpoints للـ JS (notifications.js)
        // =========================================================

        // GET: /Notifications/Unread?userId=123
        [HttpGet("Unread")]
        public async Task<IActionResult> Unread(int? userId)
        {
            var uid = ResolveCurrentUserId(userId);
            if (uid == 0) return Unauthorized();

            var items = await _db.Notifications
                .Where(n => n.RecipientId == uid && !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new
                {
                    n.Id,
                    title = n.EventType,
                    n.Message,
                    createdAt = n.CreatedAt,   // ISO 8601
                    url = ""                   // عدّلي لو عندك رابط تفاصيل
                })
                .ToListAsync();

            return Json(items);
        }

        // POST: /Notifications/MarkRead?id=5&userId=123
        [HttpPost("MarkRead")]
        public async Task<IActionResult> MarkRead(int id, int? userId)
        {
            var uid = ResolveCurrentUserId(userId);
            if (uid == 0) return Unauthorized();

            var note = await _db.Notifications
                                .FirstOrDefaultAsync(n => n.Id == id && n.RecipientId == uid);
            if (note == null) return NotFound();

            if (!note.IsRead)
            {
                note.IsRead = true;
                await _db.SaveChangesAsync();
            }

            return Ok();
        }

        // =========================================================
        // 4) اختبار الإرسال + البث الفوري عبر SignalR
        // =========================================================

        // POST: /Notifications/TestSend?recipientId=123&msg=hello
        [HttpPost("TestSend")]
        public async Task<IActionResult> TestSend(int recipientId, string msg = "Hello")
        {
            // خزّني في DB (عشان يظهر لاحقًا لو المستخدم أوفلاين)
            var n = new Notification
            {
                RecipientId = recipientId,
                EventType = "Test",
                Message = msg,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _db.Notifications.Add(n);
            await _db.SaveChangesAsync();

            // ادفعيه فورًا للي أونلاين (group: user-{recipientId})
            await _hub.Clients.Group($"user-{recipientId}")
                .SendAsync("Notify", new
                {
                    id = n.Id,
                    eventType = n.EventType,
                    message = n.Message,
                    createdAt = n.CreatedAt,
                    url = "" // اختياري
                });

            return Ok(new { n.Id });

        }
        
        
    }
}

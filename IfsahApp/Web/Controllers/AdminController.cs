// Web/Controllers/AdminController.cs
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IfsahApp.Core.Enums;                  // Role enum: Admin, Examiner, User
using IfsahApp.Core.Models;                 // User model
using IfsahApp.Infrastructure.Data;         // ApplicationDbContext
using IfsahApp.Infrastructure.Services.AdUser;
using IfsahApp.Infrastructure.Services.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IfsahApp.Web.Controllers
{
    // DTOs لطلبات الـ API
    public record AssignRoleRequest(string Sam, string DisplayName, string Email, string Department, string Role);
    public record SamOnlyRequest(string Sam);
    public record SetActiveRequest(string Sam, bool Active);

    [Authorize]               // (اختياري) علّقيها مؤقتًا لو عندك مشكلة دخول
    [Route("Admin")]          // أساس الراوت لكل الأكشنات هنا
    public class AdminController : Controller
    {
        private readonly IAdUserService _adUsers;
        private readonly ApplicationDbContext _db;
        private readonly IEmailService _mailer;

        public AdminController(IAdUserService adUsers, ApplicationDbContext db, IEmailService mailer)
        {
            _adUsers = adUsers;
            _db      = db;
            _mailer  = mailer;
        }

        // ---------- صفحات ----------
        // GET https://localhost:5001/Admin/Index
        [HttpGet("")]
        [HttpGet("Index")]
        public IActionResult Index() => View();

        // GET https://localhost:5001/Admin/ExaminersPage
        [HttpGet("ExaminersPage")]
        public IActionResult ExaminersPage() => View();

        // ---------- البحث العام عن مستخدمي AD (للاوتوكومبليت) ----------
        // GET /Admin/SearchAdUsers?q=...&take=8
        [HttpGet("SearchAdUsers")]
        public async Task<IActionResult> SearchAdUsers(string q, int take = 8, CancellationToken ct = default)
        {
            take = Math.Clamp(take <= 0 ? 8 : take, 1, 50);
            var list = await _adUsers.SearchAsync(q ?? string.Empty, take, ct);
            return Json(list.Select(u => new
            {
                sam  = u.SamAccountName,
                name = u.DisplayName,
                email= u.Email,
                dept = u.Department
            }));
        }

        // GET /Admin/GetAdUser?sam=ahmed.wahaibi
        [HttpGet("GetAdUser")]
        public async Task<IActionResult> GetAdUser(string sam, CancellationToken ct = default)
        {
            sam = (sam ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(sam))
                return BadRequest(new { message = "sam is required." });

            var one = (await _adUsers.SearchAsync(sam, 1, ct)).FirstOrDefault();
            if (one is null) return NotFound(new { message = "User not found." });

            return Json(new
            {
                sam  = one.SamAccountName,
                name = one.DisplayName,
                email= one.Email,
                dept = one.Department
            });
        }

        // ---------- حفظ/تحديث الدور + إرسال بريد ----------
        // POST /Admin/AssignRole
        [HttpPost("AssignRole")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignRole([FromBody] AssignRoleRequest req, CancellationToken ct = default)
        {
            if (req is null || string.IsNullOrWhiteSpace(req.Sam))
                return BadRequest(new { message = "User (sam) is required." });

            var role = MapRole(req.Role); // Admin/Examiner/User (default User)

            var samNorm  = req.Sam.Trim();
            var samLower = samNorm.ToLower();

            var user = _db.Users.FirstOrDefault(u => u.ADUserName.ToLower() == samLower);

            if (user is null)
            {
                user = new User
                {
                    ADUserName = samNorm,
                    FullName   = string.IsNullOrWhiteSpace(req.DisplayName) ? samNorm : req.DisplayName.Trim(),
                    Email      = (req.Email ?? string.Empty).Trim(),
                    Department = string.IsNullOrWhiteSpace(req.Department) ? null : req.Department.Trim(),
                    Role       = role,
                    IsActive   = true
                };
                _db.Users.Add(user);
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(req.DisplayName)) user.FullName   = req.DisplayName.Trim();
                if (!string.IsNullOrWhiteSpace(req.Email))       user.Email      = req.Email.Trim();
                if (!string.IsNullOrWhiteSpace(req.Department))  user.Department = req.Department.Trim();
                user.Role = role;
            }

            await _db.SaveChangesAsync(ct);

            await TrySendMail(
                user.Email,
                subject: "Access Granted / Updated",
                htmlBody: $@"
<p>Dear {Escape(user.FullName)},</p>
<p>Your access has been {(role == Role.Examiner ? "granted as Examiner" : "updated")}.</p>
<ul>
  <li><b>Username:</b> {Escape(user.ADUserName)}</li>
  <li><b>Role:</b> {Escape(user.Role.ToString())}</li>
  <li><b>Department:</b> {Escape(user.Department ?? "")}</li>
</ul>
<p>Regards,<br/>System Admin</p>",
                ct);

            return Ok(new { success = true, role = user.Role.ToString() });
        }

        // ---------- إدارة الممتحنين (Examiners) ----------
        // GET /Admin/Examiners  -> بيانات الجدول
        [HttpGet("Examiners")]
        public IActionResult Examiners()
        {
            var items = _db.Users
                .Where(u => u.Role == Role.Examiner)
                .OrderBy(u => u.FullName)
                .Select(u => new
                {
                    sam    = u.ADUserName,
                    name   = u.FullName,
                    email  = u.Email,
                    dept   = u.Department,
                    active = u.IsActive
                })
                .ToList();

            return Json(items);
        }// POST /Admin/AddExaminer
[HttpPost("AddExaminer")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> AddExaminer([FromBody] AssignRoleRequest req, CancellationToken ct = default)
{
    if (req is null || string.IsNullOrWhiteSpace(req.Sam))
        return BadRequest(new { message = "User (sam) is required." });

    var samNorm  = req.Sam.Trim();
    var samLower = samNorm.ToLower();

    var user = _db.Users.FirstOrDefault(u => u.ADUserName.ToLower() == samLower);

    if (user is null)
    {
        user = new User
        {
            ADUserName = samNorm,
            FullName   = string.IsNullOrWhiteSpace(req.DisplayName) ? samNorm : req.DisplayName.Trim(),
            Email      = (req.Email ?? string.Empty).Trim(),
            Department = string.IsNullOrWhiteSpace(req.Department) ? null : req.Department.Trim(),
            Role       = Role.Examiner,   // افتراضي ممتحن
            IsActive   = true
        };
        _db.Users.Add(user);
    }
    else
    {
        if (!string.IsNullOrWhiteSpace(req.DisplayName)) user.FullName   = req.DisplayName.Trim();
        if (!string.IsNullOrWhiteSpace(req.Email))       user.Email      = req.Email.Trim();
        if (!string.IsNullOrWhiteSpace(req.Department))  user.Department = req.Department.Trim();
        user.Role     = Role.Examiner;
        user.IsActive = true;
    }

    await _db.SaveChangesAsync(ct);

    await TrySendMail(
        user.Email,
        subject: "Examiner Access Granted",
        htmlBody: $@"
<p>Dear {Escape(user.FullName)},</p>
<p>You have been granted Examiner access.</p>
<ul>
  <li><b>Username:</b> {Escape(user.ADUserName)}</li>
  <li><b>Role:</b> Examiner</li>
  <li><b>Department:</b> {Escape(user.Department ?? "")}</li>
  <li><b>Active:</b> {user.IsActive}</li>
</ul>
<p>Regards,<br/>System Admin</p>",
        ct);

    // ✅ بعد النجاح، ارجعي مباشرة لصفحة جدول الممتحنين
    return RedirectToAction("ExaminersPage");
}


        // POST /Admin/RemoveExaminer  -> يحوله User (ويرسل إيميل إزالة)
        [HttpPost("RemoveExaminer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveExaminer([FromBody] SamOnlyRequest req, CancellationToken ct = default)
        {
            if (req is null || string.IsNullOrWhiteSpace(req.Sam))
                return BadRequest(new { message = "User (sam) is required." });

            var samNorm  = req.Sam.Trim();
            var samLower = samNorm.ToLower();

            var user = _db.Users.FirstOrDefault(u => u.ADUserName.ToLower() == samLower);
            if (user is null) return NotFound(new { message = "User not found." });

            user.Role = Role.User;
            await _db.SaveChangesAsync(ct);

            await TrySendMail(
                user.Email,
                subject: "Examiner Access Removed",
                htmlBody: $@"
<p>Dear {Escape(user.FullName)},</p>
<p>Your Examiner role has been removed. Your role is now: <b>User</b>.</p>
<ul>
  <li><b>Username:</b> {Escape(user.ADUserName)}</li>
  <li><b>Active:</b> {user.IsActive}</li>
</ul>
<p>Regards,<br/>System Admin</p>",
                ct);

            return Ok(new { success = true });
        }

        // POST /Admin/SetActive  -> تفعيل/تعطيل (ويرسل إيميل بالحالة)
        [HttpPost("SetActive")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetActive([FromBody] SetActiveRequest req, CancellationToken ct = default)
        {
            if (req is null || string.IsNullOrWhiteSpace(req.Sam))
                return BadRequest(new { message = "User (sam) is required." });

            var samNorm  = req.Sam.Trim();
            var samLower = samNorm.ToLower();

            var user = _db.Users.FirstOrDefault(u => u.ADUserName.ToLower() == samLower);
            if (user is null) return NotFound(new { message = "User not found." });

            user.IsActive = req.Active;
            await _db.SaveChangesAsync(ct);

            await TrySendMail(
                user.Email,
                subject: req.Active ? "Account Activated" : "Account Deactivated",
                htmlBody: $@"
<p>Dear {Escape(user.FullName)},</p>
<p>Your account has been {(req.Active ? "activated" : "deactivated")}.</p>
<ul>
  <li><b>Username:</b> {Escape(user.ADUserName)}</li>
  <li><b>Role:</b> {Escape(user.Role.ToString())}</li>
  <li><b>Active:</b> {user.IsActive}</li>
</ul>
<p>Regards,<br/>System Admin</p>",
                ct);

            return Ok(new { success = true, active = user.IsActive });
        }

        // ---------- Helpers ----------
        private Role MapRole(string? roleText)
        {
            if (string.IsNullOrWhiteSpace(roleText)) return Role.User;
            if (roleText.Equals("Admin", StringComparison.OrdinalIgnoreCase))    return Role.Admin;
            if (roleText.Equals("Examiner", StringComparison.OrdinalIgnoreCase)) return Role.Examiner;
            return Role.User;
        }

        private async Task TrySendMail(string? email, string subject, string htmlBody, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(email)) return;
            try
            {
                await _mailer.SendAsync(email.Trim(), subject, htmlBody, isHtml: true, ct);
            }
            catch
            {
                // نتجاهل خطأ SMTP حتى لا يفشل طلب الـ API كله
            }
        }

        private static string Escape(string s) => (s ?? string.Empty)
            .Replace("<", "&lt;")
            .Replace(">", "&gt;");
    }
}

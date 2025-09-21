// Web/Controllers/AdminController.cs
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using IfsahApp.Core.Enums;                  // Role enum: Admin, Examiner, User (string delegation uses "Admin")
using IfsahApp.Core.Models;                 // User, RoleDelegation
using IfsahApp.Core.ViewModels;             // AddExaminerVM, ExaminerRowVM
using IfsahApp.Infrastructure.Data;         // ApplicationDbContext
using IfsahApp.Infrastructure.Services.AdUser;
using IfsahApp.Infrastructure.Services.Email;

namespace IfsahApp.Web.Controllers
{
    [Authorize]
    [Route("Admin")]
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

        // =========================================================
        // PAGES (Views)
        // =========================================================

        // GET /Admin  (redirect to AdminPanal)
        [HttpGet("")]
        [HttpGet("Index")]
        public IActionResult Index() => RedirectToAction(nameof(AdminPanal));

        // GET /Admin/AdminPanal  -> list page using Views/Admin/AdminPanal.cshtml
        [HttpGet("AdminPanal")]
        public async Task<IActionResult> AdminPanal(CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;

            var model = await _db.Users
                .OrderBy(u => u.FullName)
                .Select(u => new ExaminerRowVM
                {
                    Id          = u.Id,
                    ADUserName  = u.ADUserName,
                    FullName    = u.FullName,
                    Email       = u.Email,
                    Department  = u.Department,
                    Role        = u.Role.ToString(),
                    HasActiveTempAdmin = _db.RoleDelegations.Any(d =>
                        d.ToUserId == u.Id &&
                        d.Role == "Admin" &&                 // <-- string-based delegation, "Admin"
                        d.StartDate <= now &&
                        (d.EndDate == null || d.EndDate >= now))
                })
                .ToListAsync(ct);

            return View("AdminPanal", model);               // Views/Admin/AdminPanal.cshtml
        }

        // (Optional) Legacy: /Admin/Examiners -> also show the same page
        [HttpGet("Examiners")]
        public Task<IActionResult> Examiners(CancellationToken ct = default)
            => AdminPanal(ct);

        // GET /Admin/AddExaminer  -> add form
        [HttpGet("AddExaminer")]
        public IActionResult AddExaminer() => View("AddExaminer", new AddExaminerVM());

        // POST /Admin/AddExaminer  -> handle form submit
        [HttpPost("AddExaminer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddExaminer([FromForm] AddExaminerVM vm, CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return View("AddExaminer", vm);

            var sam = NormalizeSam(vm.ADUserName);
            if (string.IsNullOrWhiteSpace(sam))
            {
                ModelState.AddModelError(nameof(vm.ADUserName), "اسم AD مطلوب");
                return View("AddExaminer", vm);
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.ADUserName.ToLower() == sam.ToLower(), ct);
            if (user is null)
            {
                user = new User
                {
                    ADUserName = sam,
                    FullName   = string.IsNullOrWhiteSpace(vm.FullName) ? sam : vm.FullName.Trim(),
                    Email      = vm.Email?.Trim(),
                    Department = string.IsNullOrWhiteSpace(vm.Department) ? null : vm.Department!.Trim(),
                    Role       = Role.Examiner,
                    IsActive   = true
                };
                _db.Users.Add(user);
            }
            else
            {
                user.FullName = string.IsNullOrWhiteSpace(vm.FullName) ? user.FullName : vm.FullName.Trim();
                if (!string.IsNullOrWhiteSpace(vm.Email))      user.Email      = vm.Email.Trim();
                if (!string.IsNullOrWhiteSpace(vm.Department)) user.Department = vm.Department.Trim();
                user.Role     = Role.Examiner;
                user.IsActive = true;
            }

            await _db.SaveChangesAsync(ct);

            TempData["ok"] = "تمت إضافة/تحديث الممتحن بنجاح.";
            return RedirectToAction(nameof(AdminPanal));
        }

        // POST /Admin/PromoteToAdmin
        [HttpPost("PromoteToAdmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PromoteToAdmin([FromForm] int id, CancellationToken ct)
        {
            var u = await _db.Users.FindAsync(new object?[] { id }, ct);
            if (u == null) { TempData["err"] = "المستخدم غير موجود"; return RedirectToAction(nameof(AdminPanal)); }

            u.Role = Role.Admin;
            await _db.SaveChangesAsync(ct);

            TempData["ok"] = $"تمت ترقية {u.FullName} إلى Admin.";
            return RedirectToAction(nameof(AdminPanal));
        }

        // POST /Admin/DemoteToExaminer
        [HttpPost("DemoteToExaminer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DemoteToExaminer([FromForm] int id, CancellationToken ct)
        {
            var u = await _db.Users.FindAsync(new object?[] { id }, ct);
            if (u == null) { TempData["err"] = "المستخدم غير موجود"; return RedirectToAction(nameof(AdminPanal)); }

            u.Role = Role.Examiner;
            await _db.SaveChangesAsync(ct);

            TempData["ok"] = $"تم إرجاع {u.FullName} إلى Examiner.";
            return RedirectToAction(nameof(AdminPanal));
        }

        // POST /Admin/GrantTempAdmin  (from modal in list page)
        [HttpPost("GrantTempAdmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GrantTempAdmin([FromForm] int id, [FromForm] DateTime endLocal, [FromForm] string? reason, CancellationToken ct)
        {
            var to = await _db.Users.FindAsync(new object?[] { id }, ct);
            if (to == null) { TempData["err"] = "المستخدم غير موجود"; return RedirectToAction(nameof(AdminPanal)); }

            // current (effective) admin
            var currentSam = GetCurrentSam();
            var from = await GetOrCreateUserBySamAsync(currentSam, ct);
            if (from == null || EffectiveRole(from) != Role.Admin)
            {
                TempData["err"] = "غير مصرح.";
                return RedirectToAction(nameof(AdminPanal));
            }

            var startUtc = DateTime.UtcNow;
            var endUtc   = DateTime.SpecifyKind(endLocal, DateTimeKind.Local).ToUniversalTime();
            if (endUtc <= startUtc)
            {
                TempData["err"] = "تاريخ الانتهاء يجب أن يكون لاحقاً للآن.";
                return RedirectToAction(nameof(AdminPanal));
            }

            var entry = new RoleDelegation
            {
                FromUserId  = from.Id,
                ToUserId    = to.Id,
                Role        = "Admin",                    // <-- string-based temp admin delegation
                IsPermanent = false,
                StartDate   = startUtc,
                EndDate     = endUtc,
                Reason      = string.IsNullOrWhiteSpace(reason) ? null : reason
            };
            _db.RoleDelegations.Add(entry);
            await _db.SaveChangesAsync(ct);

            TempData["ok"] = $"تم منح صلاحية Admin مؤقتة إلى {to.FullName} حتى {endLocal}.";
            return RedirectToAction(nameof(AdminPanal));
        }

        // =========================================================
        // JSON helper (AD Search)
        // =========================================================

        // GET /Admin/SearchAdUsers?q=ali&take=8   (also accepts 'query=')
        [HttpGet("SearchAdUsers")]
        public async Task<IActionResult> SearchAdUsers(string q, string? query, int take = 8, CancellationToken ct = default)
        {
            var term = (q ?? query ?? string.Empty).Trim();
            take = Math.Clamp(take <= 0 ? 8 : take, 1, 50);

            var list = await _adUsers.SearchAsync(term, take, ct);

            // optional: fallback to local DB during dev
            if ((list == null || list.Count == 0) && !string.IsNullOrWhiteSpace(term))
            {
                var dbHits = _db.Users
                    .Where(u => u.ADUserName.Contains(term) || u.FullName.Contains(term) || (u.Email ?? "").Contains(term))
                    .OrderBy(u => u.FullName)
                    .Take(take)
                    .Select(u => new { u.ADUserName, u.FullName, u.Email, u.Department })
                    .ToList();

                return Json(dbHits.Select(u => new {
                    sam   = u.ADUserName,
                    name  = u.FullName,
                    email = u.Email,
                    dept  = u.Department
                }));
            }

            return Json(list.Select(u => new
            {
                sam   = u.SamAccountName,
                name  = u.DisplayName,
                email = u.Email,
                dept  = u.Department
            }));
        }

        // =========================================================
        // Helpers
        // =========================================================

        private async Task TrySendMail(string? email, string subject, string htmlBody, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(email)) return;
            try { await _mailer.SendAsync(email.Trim(), subject, htmlBody, isHtml: true, ct); }
            catch { /* ignore SMTP errors to not break page flow */ }
        }

        private static string NormalizeSam(string sam)
        {
            if (string.IsNullOrWhiteSpace(sam)) return string.Empty;
            if (sam.Contains('\\')) return sam.Split('\\').Last().Trim(); // DOMAIN\sam -> sam
            if (sam.Contains('@'))  return sam.Split('@').First().Trim(); // sam@domain -> sam
            return sam.Trim();
        }

        private string GetCurrentSam()
        {
            // Adjust to your identity provider as needed:
            var name = User?.Identity?.Name ?? string.Empty; // often "DOMAIN\\sam"
            var fromClaim = User?.FindFirst("samAccountName")?.Value;
            var raw = string.IsNullOrWhiteSpace(fromClaim) ? name : fromClaim;
            return NormalizeSam(raw);
        }

        private async Task<User?> GetOrCreateUserBySamAsync(string sam, CancellationToken ct)
        {
            var samNorm = NormalizeSam(sam);
            if (string.IsNullOrWhiteSpace(samNorm)) return null;

            var samLower = samNorm.ToLower();
            var user = _db.Users.FirstOrDefault(u => u.ADUserName.ToLower() == samLower);
            if (user != null) return user;

            // Try resolve from AD
            var ad = (await _adUsers.SearchAsync(samNorm, 1, ct)).FirstOrDefault();
            user = new User
            {
                ADUserName = samNorm,
                FullName   = ad?.DisplayName ?? samNorm,
                Email      = ad?.Email ?? "",
                Department = string.IsNullOrWhiteSpace(ad?.Department) ? null : ad!.Department,
                Role       = Role.User,
                IsActive   = true
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);
            return user;
        }

        private bool HasActiveAdminDelegation(int userId)
        {
            var now = DateTime.UtcNow;
            return _db.RoleDelegations.Any(d =>
                d.ToUserId == userId &&
                d.Role == "Admin" &&                      // <-- string comparison for delegation
                d.StartDate <= now &&
                (d.EndDate == null || d.EndDate >= now));
        }

        private Role EffectiveRole(User u) =>
            (u.Role == Role.Admin || HasActiveAdminDelegation(u.Id)) ? Role.Admin : u.Role;
    }
}

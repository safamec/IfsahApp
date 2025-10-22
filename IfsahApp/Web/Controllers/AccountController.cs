using System.Security.Claims;
using System.Linq;
using IfsahApp.Core.Enums;
using IfsahApp.Core.Models;
using IfsahApp.Core.ViewModels.Account; // ConfirmEmailResultVm
using IfsahApp.Infrastructure.Data;
using IfsahApp.Infrastructure.Services;
using IfsahApp.Infrastructure.Services.AdUser;
using IfsahApp.Infrastructure.Services.Email;
using IfsahApp.Utils.Security; // EmailTokenHelper
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IfsahApp.Web.Controllers;

[Authorize]
public class AccountController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AccountController> _logger;
    private readonly IEmailService _email;
    private readonly ViewRenderService _viewRender;

    public AccountController(
        ApplicationDbContext context,
        ILogger<AccountController> logger,
        IEmailService email,
        ViewRenderService viewRender)
    {
        _context = context;
        _logger  = logger;
        _email   = email;
        _viewRender = viewRender;
    }

    // =============================
    // Issue email confirmation link
    // =============================
    private async Task IssueEmailConfirmationAsync(User user, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(user.Email)) return;

        // Throttle resends
        var last = await _context.EmailVerifications
            .Where(v => v.UserId == user.Id && v.Purpose == "email_confirm")
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (last != null && (DateTime.UtcNow - last.CreatedAt) < TimeSpan.FromMinutes(2))
        {
            _logger.LogInformation("Skip issuing email confirm token (throttled) for user {UserId}", user.Id);
            return;
        }

        var token = EmailTokenHelper.GenerateToken();
        var hash  = EmailTokenHelper.Sha256Hex(token);

        var ev = new EmailVerification
        {
            UserId    = user.Id,
            TokenHash = hash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            Purpose   = "email_confirm"
        };

        _context.EmailVerifications.Add(ev);
        await _context.SaveChangesAsync(ct);

        var link = Url.Action(
            action: nameof(ConfirmEmail),
            controller: "Account",
            values: new { uid = user.Id, token },
            protocol: Request.Scheme,
            host: Request.Host.ToString());

        // Render Razor email
        var model = new { FullName = user.FullName, Link = link! };
        var html  = await _viewRender.RenderToStringAsync("Emails/ConfirmEmail", model);

        try
        {
            await _email.SendAsync(user.Email, "Confirm your email - IfsahApp", html, isHtml: true, ct: ct);
            _logger.LogInformation("Sent confirmation email to {Email} for user {UserId}", user.Email, user.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed sending confirmation email to {Email}", user.Email);
        }
    }

    // =============================
    // Login (GET) — supports AD; falls back to seeded Admin when AD is absent
    // =============================
    [HttpGet]
    public async Task<IActionResult> Login(CancellationToken ct)
    {
        // Try AD from middleware
        if (HttpContext.Items.TryGetValue("AdUser", out var obj) && obj is AdUser adUser)
        {
            _logger.LogInformation("AD user detected: {Sam} | {Display} | {Email}",
                adUser.SamAccountName, adUser.DisplayName, adUser.Email);

            return await SignInWithUserAsync(adUser.SamAccountName, adUser.DisplayName, adUser.Email, adUser.Department, ct);
        }

        // Fallback (no AD): use first active Admin from DB (for dev/testing)
        var admin = await _context.Users
            .Where(u => u.IsActive && u.Role == Role.Admin)
            .OrderBy(u => u.Id)
            .FirstOrDefaultAsync(ct);

        if (admin == null)
        {
            _logger.LogWarning("No active Admin found for fallback login.");
            return RedirectToAction("AccessDenied");
        }

        _logger.LogWarning("No AdUser found; using fallback Admin login for '{Sam}'.", admin.ADUserName);

        // Build fake display/email/department from DB for claims
        return await SignInWithUserAsync(
            samAccountName: admin.ADUserName,
            displayName:    admin.FullName ?? admin.ADUserName,
            email:          admin.Email ?? string.Empty,
            department:     admin.Department ?? string.Empty,
            ct:             ct
        );
    }

    // Unified sign-in helper (used by AD path and fallback path)
    private async Task<IActionResult> SignInWithUserAsync(string samAccountName, string? displayName, string? email, string? department, CancellationToken ct)
    {
        // DB lookup (case-insensitive by ADUserName)
        var user = _context.Users
            .AsEnumerable()
            .FirstOrDefault(u => string.Equals(u.ADUserName, samAccountName, StringComparison.OrdinalIgnoreCase));

        // --- Auto-create new user if not found ---
        if (user is null)
        {
            _logger.LogInformation("New AD user '{Sam}' not found in DB — creating a new record.", samAccountName);

            user = new User
            {
                ADUserName = samAccountName,
                FullName = displayName ?? samAccountName,
                Email = email ?? string.Empty,
                Department = department ?? "Unknown",
                Role = Role.User,             // default role
                IsActive = false,
                IsEmailConfirmed = false       // assume AD users are verified
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync(ct);

            _logger.LogInformation("User '{Sam}' created successfully with default Role: {Role}.", samAccountName, user.Role);
        }

        // --- Check if user is active ---
        if (!user.IsActive)
        {
            _logger.LogWarning("Login failed: user {Sam} is inactive.", samAccountName);
            return RedirectToAction("AccessDenied");
        }

        // --- Enforce email confirmation for all EXCEPT Admin ---
        if (user.Role != Role.Admin && !user.IsEmailConfirmed && !string.IsNullOrWhiteSpace(user.Email))
        {
            await IssueEmailConfirmationAsync(user, ct);
            await HttpContext.SignOutAsync("Cookies");
            return View("EmailNotConfirmed", new { Email = user.Email, UserId = user.Id });
        }

        // --- Build claims for authentication ---
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.ADUserName),
            new Claim(ClaimTypes.GivenName, displayName ?? user.FullName ?? string.Empty),
            new Claim(ClaimTypes.Email, email ?? user.Email ?? string.Empty),
            new Claim("Department", department ?? user.Department ?? string.Empty),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var identity  = new ClaimsIdentity(claims, "Cookies");
        var principal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync("Cookies", principal);

        _logger.LogInformation("User {User} logged in with role {Role}.", user.ADUserName, user.Role);

        // Redirect by role
        return user.Role switch
        {
            Role.Admin    => RedirectToAction("Index",  "Dashboard"),
            Role.Examiner => RedirectToAction("Index",  "Review"),
            Role.User     => RedirectToAction("Create", "Disclosure"),
            _             => RedirectToAction("AccessDenied"),
        };
    }

    // =============================
    // Confirm Email (GET)
    // =============================
    [HttpGet]
    [IgnoreAntiforgeryToken] // GET shouldn't need CSRF
    public async Task<IActionResult> ConfirmEmail(int uid, string token, CancellationToken ct)
    {
        if (uid <= 0 || string.IsNullOrWhiteSpace(token))
            return View("ConfirmEmailResult", new ConfirmEmailResultVm
            {
                Success = false,
                Title   = "رابط غير صالح",
                Message = "رابط التأكيد غير صحيح."
            });

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == uid, ct);
        if (user == null)
            return View("ConfirmEmailResult", new ConfirmEmailResultVm {
                Success = false,
                Title   = "المستخدم غير موجود",
                Message = "تعذر العثور على المستخدم المطلوب."
            });

        if (user.IsEmailConfirmed)
            return View("ConfirmEmailResult", new ConfirmEmailResultVm {
                Success = true,
                Title   = "تم التأكيد مسبقًا",
                Message = "بريدك الإلكتروني مؤكد مسبقًا. يمكنك تسجيل الدخول الآن."
            });

        var ev = await _context.EmailVerifications
            .Where(v => v.UserId == uid && v.Purpose == "email_confirm" && v.ConsumedAt == null)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (ev == null)
            return View("ConfirmEmailResult", new ConfirmEmailResultVm {
                Success = false,
                Title   = "لا يوجد طلب صالح",
                Message = "لا يوجد طلب تأكيد صالح. يرجى إعادة إرسال رابط جديد."
            });

        if (ev.ExpiresAt < DateTime.UtcNow)
            return View("ConfirmEmailResult", new ConfirmEmailResultVm {
                Success = false,
                Title   = "انتهت صلاحية الرابط",
                Message = "انتهت صلاحية رابط التأكيد. يرجى إعادة الإرسال."
            });

        ev.Attempts++;
        if (ev.Attempts > 5)
        {
            await _context.SaveChangesAsync(ct);
            return View("ConfirmEmailResult", new ConfirmEmailResultVm {
                Success = false,
                Title   = "محاولات كثيرة",
                Message = "تجاوزت الحد المسموح من المحاولات. أعد إرسال رابط جديد."
            });
        }

        var tokenHash = EmailTokenHelper.Sha256Hex(token);
        if (!string.Equals(ev.TokenHash, tokenHash, StringComparison.Ordinal))
        {
            await _context.SaveChangesAsync(ct);
            return View("ConfirmEmailResult", new ConfirmEmailResultVm {
                Success = false,
                Title   = "رابط غير صحيح",
                Message = "رمز التأكيد غير مطابق."
            });
        }

        ev.ConsumedAt = DateTime.UtcNow;
        user.IsEmailConfirmed = true;
        user.EmailConfirmedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return View("ConfirmEmailResult", new ConfirmEmailResultVm {
            Success = true,
            Title   = "تم تأكيد البريد الإلكتروني",
            Message = "تم تأكيد بريدك الإلكتروني بنجاح. يمكنك تسجيل الدخول الآن."
        });
    }

    // =============================
    // Resend Confirmation (POST)
    // =============================
    [HttpPost]
    [ValidateAntiForgeryToken] // keep CSRF on POST
    public async Task<IActionResult> ResendConfirmation(int uid, CancellationToken ct)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == uid, ct);
        if (user == null)
            return View("ConfirmationSent", new { Email = "غير معروف" });

        if (user.IsEmailConfirmed)
            return View("ConfirmationSent", new { Email = user.Email, Already = true });

        await IssueEmailConfirmationAsync(user, ct);

        return View("ConfirmationSent", new { Email = user.Email, Already = false });
    }

    // =============================
    // Access Denied & Logout
    // =============================
    public IActionResult AccessDenied() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("Cookies"); // explicit scheme
        _logger.LogInformation("User logged out.");
        return RedirectToAction("Login");
    }

    // Optional quick diagnostics
    // public IActionResult WhoAmI()
    // {
    //     var isAuth = User?.Identity?.IsAuthenticated ?? false;
    //     var name   = User?.Identity?.Name ?? "(anon)";
    //     var roles  = string.Join(",", User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value));
    //     return Content($"IsAuthenticated: {isAuth}\nName: {name}\nRoles: {roles}", "text/plain; charset=utf-8");
    // }
}

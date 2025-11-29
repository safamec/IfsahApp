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
using Microsoft.AspNetCore.Authentication.Cookies;
using IfsahApp.Core.ViewModels;
using AspNetCoreGeneratedDocument;

namespace IfsahApp.Web.Controllers;

[Authorize]
public class AccountController(
    ApplicationDbContext context,
    ILogger<AccountController> logger,
    IEmailService email,
    ViewRenderService viewRender,
    IAdUserService adUserService,
    IWebHostEnvironment env) : Controller
{
    private readonly ApplicationDbContext _context = context;
    private readonly ILogger<AccountController> _logger = logger;
    private readonly IEmailService _email = email;
    private readonly ViewRenderService _viewRender = viewRender;
    private readonly IAdUserService _adUserService = adUserService;
    private readonly IWebHostEnvironment _env = env;

// =============================
// Login (GET) — AD first, fallback to local Admin
// =============================
[AllowAnonymous]
[HttpGet]
public async Task<IActionResult> Login(CancellationToken ct)
{
    // --- FIX: allow email confirmation without auto-login ---
    if (Request.Query.ContainsKey("uid") && Request.Query.ContainsKey("token"))
    {
        return RedirectToAction(nameof(ConfirmEmail), new {
            uid = Request.Query["uid"],
            token = Request.Query["token"]
        });
    }

    // // If already logged in → redirect
    // if (User?.Identity?.IsAuthenticated == true)
    //     return RedirectToHomeByRole();

    // ---------------------------
    // 1) Silent Windows Login (Stag/Prod Only)
    // ---------------------------
    if (!_env.IsDevelopment())
    {
        string? windowsName = User.Identity?.Name;

        // Browser DID NOT send Windows credentials
        if (string.IsNullOrWhiteSpace(windowsName))
        {
            _logger.LogWarning("No silent Windows login. Showing login page.");
            return View(); // (You can create a simple blank view)
        }

        // Browser DID send credentials → validate with LDAP
        var adUser = await _adUserService.FindByWindowsIdentityAsync(windowsName, ct);

        if (adUser != null)
        {
            _logger.LogInformation("Silent Windows login succeeded for {Sam}", adUser.SamAccountName);

            return await SignInWithUserAsync(
                adUser.SamAccountName,
                adUser.DisplayName,
                adUser.Email,
                adUser.Department,
                ct);
        }

        // Windows credentials sent but not found in AD → fallback to login page
        _logger.LogWarning("Windows user not found in AD: {Win}", windowsName);
        return View();
    }

    // ---------------------------
    // 2) DEV: Always do fallback Admin Login (No Windows Auth)
    // ---------------------------
    var admin = await _context.Users
        .Where(u => u.IsActive && u.Role == Role.Admin)
        .FirstOrDefaultAsync(ct);

    if (admin != null)
    {
        _logger.LogInformation("Dev fallback login as Admin {Sam}", admin.ADUserName);

        return await SignInWithUserAsync(
            admin.ADUserName,
            admin.FullName,
            admin.Email,
            admin.Department,
            ct);
    }

    return RedirectToAction("AccessDenied");
}

[AllowAnonymous]
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
{
    if (!ModelState.IsValid)
    {
        return View(model);
    }

    // --- Try AD user lookup via LdapAdUserService ---
    var adUser = await _adUserService.FindByCredentialsAsync(model.UserName, model.Password, ct);

    if (adUser is not null)
    {
        _logger.LogInformation("Manual AD login successful for {Sam}", adUser.SamAccountName);

        return await SignInWithUserAsync(
            adUser.SamAccountName,
            adUser.DisplayName,
            adUser.Email,
            adUser.Department,
            ct
        );
    }

    // --- Optional: fallback to local DB users (if you allow it in dev/staging) ---
    var localUser = await _context.Users
        .Where(u => u.IsActive && u.ADUserName.ToLower() == model.UserName.ToLower())
        .FirstOrDefaultAsync(ct);

    if (localUser != null)
    {
        // NOTE: In prod you may want to require password check
        _logger.LogWarning("Fallback local login for {Sam}", localUser.ADUserName);

        return await SignInWithUserAsync(
            localUser.ADUserName,
            localUser.FullName,
            localUser.Email,
            localUser.Department,
            ct
        );
    }

    // --- Failed login ---
    _logger.LogWarning("Login failed for {User}", model.UserName);
    model.ErrorMessage = "Invalid username or password.";
    return View(model);
}

// =============================
// Unified sign-in logic
// =============================
private async Task<IActionResult> SignInWithUserAsync(
    string samAccountName,
    string? displayName,
    string? email,
    string? department,
    CancellationToken ct)
{
    // Normalize username
    string normalizedSam = samAccountName.ToLower();

    // Find existing user
    var user = await _context.Users
        .FirstOrDefaultAsync(u => u.ADUserName.ToLower() == normalizedSam, ct);

    // Auto-create if missing
    if (user is null)
    {
        user = new User
        {
            ADUserName = samAccountName,
            FullName = displayName ?? samAccountName,
            Email = email ?? string.Empty,
            Department = department ?? "Unknown",
            Role = Role.User,
            IsActive = true,
            IsEmailConfirmed = false
        };

        _context.Users.Add(user);
       try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            Console.WriteLine("DbUpdateException: " + ex.Message);
            if (ex.InnerException != null)
                Console.WriteLine("InnerException: " + ex.InnerException.Message);
            throw; // rethrow or handle
        }

        _logger.LogInformation("New user {Sam} created with role {Role}", samAccountName, user.Role);
    }

    // Block inactive
    if (!user.IsActive)
    {
        _logger.LogWarning("Blocked inactive user {Sam}", samAccountName);
        return RedirectToAction("AccessDenied");
    }

    // Email confirmation (non-admin only)
    if (user.Role != Role.Admin &&
        !user.IsEmailConfirmed &&
        !string.IsNullOrWhiteSpace(user.Email))
    {
        await IssueEmailConfirmationAsync(user, ct);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return View("EmailNotConfirmed", new { Email = user.Email, UserId = user.Id });
    }

    // Build claims
    var claims = new List<Claim>
    {
        new Claim(ClaimTypes.Name, user.ADUserName),
        new Claim(ClaimTypes.GivenName, displayName ?? user.FullName ?? ""),
        new Claim(ClaimTypes.Email, email ?? user.Email ?? ""),
        new Claim("Department", department ?? user.Department ?? ""),
        new Claim(ClaimTypes.Role, user.Role.ToString())
    };

    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new ClaimsPrincipal(identity);

    await HttpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        principal
    );

    _logger.LogInformation("User {Sam} logged in as {Role}", user.ADUserName, user.Role);

    // Redirect based on role
    return user.Role switch
    {
        Role.Admin    => RedirectToAction("Index", "Dashboard"),
        Role.Examiner => RedirectToAction("Index", "Review"),
        Role.User     => RedirectToAction("Create", "Disclosure"),
        _             => RedirectToAction("AccessDenied")
    };
}

    // =============================
    // Confirm Email (GET)
    // =============================
    [AllowAnonymous]
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

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using IfsahApp.Core.Enums;
using IfsahApp.Core.Models;
using IfsahApp.Infrastructure.Data;
using IfsahApp.Infrastructure.Services.AdUser;
using IfsahApp.Infrastructure.Services.Email;
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

    public AccountController(ApplicationDbContext context, ILogger<AccountController> logger, IEmailService email)
    {
        _context = context;
        _logger = logger;
        _email = email;
    }

    // =============================
    // Helpers for secure tokens
    // =============================
    private static string GenerateToken(int bytes = 32)
    {
        var data = RandomNumberGenerator.GetBytes(bytes);
        // URL-safe Base64 (no padding)
        return Convert.ToBase64String(data)
                      .TrimEnd('=')
                      .Replace('+', '-')
                      .Replace('/', '_');
    }

    private static string Sha256Hex(string input)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        var sb = new StringBuilder(hash.Length * 2);
        foreach (var b in hash) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }

    private async Task IssueEmailConfirmationAsync(User user, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(user.Email)) return;

        // Throttle resends: only issue a new token if last < 2 minutes ago
        var last = await _context.EmailVerifications
            .Where(v => v.UserId == user.Id && v.Purpose == "email_confirm")
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (last != null && (DateTime.UtcNow - last.CreatedAt) < TimeSpan.FromMinutes(2))
        {
            _logger.LogInformation("Skip issuing email confirm token (throttled) for user {UserId}", user.Id);
            return;
        }

        var token = GenerateToken();
        var hash  = Sha256Hex(token);

        var ev = new EmailVerification
        {
            UserId   = user.Id,
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

        var html = $@"
<h2>تأكيد البريد الإلكتروني</h2>
<p>مرحبًا {user.FullName}, الرجاء تأكيد بريدك لإكمال الوصول للحساب.</p>
<p><a href=""{link}"">اضغط هنا لتأكيد بريدك</a></p>
<p>سينتهي الرابط بعد 24 ساعة، ويُستخدم مرة واحدة فقط.</p>";

        try
        {
            await _email.SendAsync(user.Email, "Confirm your email - IfsahApp", html, isHtml: true, ct: ct);
            _logger.LogInformation("Sent confirmation email to {Email} for user {UserId}", user.Email, user.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed sending confirmation email to {Email}", user.Email);
            // We don't throw; user can retry resend later
        }
    }

    // =============================
    // Login (GET)
    // =============================
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Login()
    {
        // 1. AD Lookup (from middleware)
        if (!HttpContext.Items.TryGetValue("AdUser", out var obj) || obj is not AdUser adUser)
        {
            _logger.LogWarning("Login attempt failed: No AdUser found in HttpContext.");
            return RedirectToAction("AccessDenied");
        }

        _logger.LogInformation(
            "AD user detected: {SamAccountName}, DisplayName: {DisplayName}, Email: {Email}",
            adUser.SamAccountName, adUser.DisplayName, adUser.Email);

        // 2. DB Lookup (case-insensitive)
        var user = _context.Users
            .AsEnumerable()
            .FirstOrDefault(u =>
                string.Equals(u.ADUserName, adUser.SamAccountName, StringComparison.OrdinalIgnoreCase));

        if (user is null)
        {
            _logger.LogWarning("Login failed: AD user {SamAccountName} not found in DB.", adUser.SamAccountName);
            return RedirectToAction("AccessDenied");
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login failed: User {SamAccountName} is inactive.", adUser.SamAccountName);
            return RedirectToAction("AccessDenied");
        }

        // Enforce email confirmation if email exists on user
        if (!user.IsEmailConfirmed && !string.IsNullOrWhiteSpace(user.Email))
        {
            // Issue/ensure a token is available
            await IssueEmailConfirmationAsync(user);

            // Make sure user is not signed-in until confirmation
            await HttpContext.SignOutAsync();

            // Simple message (no view dependency) + a quick resend link
            var resendLink = Url.Action(nameof(ResendConfirmation), "Account", new { uid = user.Id }, Request.Scheme);
            return Content($"""
الحساب غير مفعل بالبريد الإلكتروني.
لقد أرسلنا رابط تأكيد إلى: {user.Email}
إذا لم يصلك خلال دقائق، يمكنك إعادة الإرسال عبر POST إلى: {resendLink}
""", "text/plain; charset=utf-8");
        }

        _logger.LogInformation("DB user found: {FullName}, Role: {Role}", user.FullName, user.Role);

        // 3. Build claims (for authorization)
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, adUser.SamAccountName),
            new Claim(ClaimTypes.GivenName, adUser.DisplayName ?? string.Empty),
            new Claim(ClaimTypes.Email, adUser.Email ?? string.Empty),
            new Claim("Department", adUser.Department ?? string.Empty),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var identity = new ClaimsIdentity(claims, "Cookies");
        var principal = new ClaimsPrincipal(identity);

        // 4. Sign in (persistent cookie)
        await HttpContext.SignInAsync(principal);

        _logger.LogInformation("User {User} logged in with role {Role}.",
            adUser.SamAccountName, user.Role);

        // 5. Redirect by role
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
    // /Account/ConfirmEmail?uid=123&token=XYZ
    // =============================
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ConfirmEmail(int uid, string token, CancellationToken ct)
    {
        if (uid <= 0 || string.IsNullOrWhiteSpace(token))
            return BadRequest("Invalid confirmation link.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == uid, ct);
        if (user == null) return NotFound("User not found.");

        if (user.IsEmailConfirmed)
            return Content("البريد الإلكتروني مؤكد مسبقًا. يمكنك تسجيل الدخول الآن.", "text/plain; charset=utf-8");

        var tokenHash = Sha256Hex(token);
        var ev = await _context.EmailVerifications
            .Where(v => v.UserId == uid && v.Purpose == "email_confirm" && v.ConsumedAt == null)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (ev == null) return BadRequest("لا يوجد طلب تأكيد صالح.");
        if (ev.ExpiresAt < DateTime.UtcNow) return BadRequest("انتهت صلاحية رابط التأكيد.");

        // brute-force guard
        ev.Attempts++;
        if (ev.Attempts > 5)
        {
            await _context.SaveChangesAsync(ct);
            return BadRequest("محاولات كثيرة. أعد الإرسال.");
        }

        if (!string.Equals(ev.TokenHash, tokenHash, StringComparison.Ordinal))
        {
            await _context.SaveChangesAsync(ct);
            return BadRequest("رابط التأكيد غير صحيح.");
        }

        // success: consume + confirm
        ev.ConsumedAt = DateTime.UtcNow;
        user.IsEmailConfirmed = true;
        user.EmailConfirmedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);

        return Content("تم تأكيد بريدك الإلكتروني بنجاح. يمكنك تسجيل الدخول الآن.", "text/plain; charset=utf-8");
    }

    // =============================
    // Resend Confirmation (POST)
    // body: uid=123
    // =============================
    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendConfirmation(int uid, CancellationToken ct)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == uid, ct);
        if (user == null) return NotFound(new { ok = false, message = "User not found." });

        if (user.IsEmailConfirmed)
            return Ok(new { ok = true, message = "Already confirmed." });

        await IssueEmailConfirmationAsync(user, ct);
        return Ok(new { ok = true, message = "Confirmation email sent." });
    }

    // =============================
    // Access Denied Page
    // =============================
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    // =============================
    // Logout
    // =============================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        _logger.LogInformation("User logged out.");
        return RedirectToAction("Login");
    }
}

using IfsahApp.Core.Enums;
using IfsahApp.Infrastructure.Data;
using IfsahApp.Infrastructure.Services.AdUser;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace IfsahApp.Web.Controllers;

[Authorize]
public class AccountController(ApplicationDbContext context, ILogger<AccountController> logger) : Controller
{
    private readonly ApplicationDbContext _context = context;
    private readonly ILogger<AccountController> _logger = logger;

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
            Role.Admin => RedirectToAction("Index", "Dashboard"),
            Role.Examiner => RedirectToAction("Index", "Review"),
            Role.User => RedirectToAction("Create", "Disclosure"),
            _ => RedirectToAction("AccessDenied"),
        };
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

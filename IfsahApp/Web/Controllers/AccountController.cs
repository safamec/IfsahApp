using IfsahApp.Core.Enums;
using IfsahApp.Infrastructure.Data;
using IfsahApp.Infrastructure.Services.AdUser;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IfsahApp.Web.Controllers;

[Authorize]
public class AccountController(ApplicationDbContext context, ILogger<AccountController> logger) : Controller
{
    private readonly ApplicationDbContext _context = context;
    private readonly ILogger<AccountController> _logger = logger;

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login()
    {
        // 1. AD Lookup (who logged in)
        if (!HttpContext.Items.TryGetValue("AdUser", out var obj) || obj is not AdUser adUser)
        {
            _logger.LogWarning("Login attempt failed: No AdUser found in HttpContext.");
            return RedirectToAction("AccessDenied");
        }

        _logger.LogInformation("AD user detected: {SamAccountName}, DisplayName: {DisplayName}, Email: {Email}",
            adUser.SamAccountName, adUser.DisplayName, adUser.Email);

        // 2. DB Lookup (case-insensitive using ToLower)
        var adUserName = adUser.SamAccountName;

        var user = _context.Users
            .AsEnumerable() // loads all users into memory
            .FirstOrDefault(u => string.Equals(u.ADUserName, adUserName, StringComparison.OrdinalIgnoreCase));

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

        // 3. Redirect by Role
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
    // Optional: Log out (for dev)
    // =============================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.SignOutAsync(); // only meaningful in FakeAuth
        _logger.LogInformation("User logged out.");
        return RedirectToAction("Login");
    }
}
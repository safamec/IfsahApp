using IfsahApp.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IfsahApp.Controllers;

[Authorize] // All actions require authenticated users
public class AccountController : Controller
{
    // =============================
    // Login / Landing Page
    // =============================
    [HttpGet]
    public IActionResult Login()
    {
        // AD user injected by middleware
        if (HttpContext.Items.TryGetValue("AdUser", out var obj) && obj is AdUser adUser)
        {
            // Pass user to the view
            return View(adUser);
        }

        // If no AD profile found, redirect to AccessDenied
        return RedirectToAction("AccessDenied");
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
        // For Windows Authentication, this usually does nothing
        // But for FakeAuth, we can sign out
        HttpContext.SignOutAsync();
        return RedirectToAction("Login");
    }
}

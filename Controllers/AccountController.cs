
using IfsahApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace IfsahApp.Controllers;


public class AccountController : Controller
{
    private readonly IAdUserService _adUserService;

    public AccountController(IAdUserService adUserService)
    {
        _adUserService = adUserService;
    }

    public IActionResult Login()
    {
        // Get current Windows user
        var username = User.Identity?.Name; // DOMAIN\username

        if (string.IsNullOrEmpty(username))
        {
            return Unauthorized("Windows user not detected");
        }

        // Optionally remove DOMAIN prefix
        var accountName = username.Contains("\\") ? username.Split('\\')[1] : username;

        // Call your AD service
        //var userInfo = _adUserService.GetUserByAccountName(accountName);

        // if (userInfo == null)
        // {
        //     return Unauthorized("User not found in AD");
        // }

        // Here you can sign in the user using cookies if needed
        // For now, just show info
        return View();
    }
}
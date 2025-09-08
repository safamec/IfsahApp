using IfsahApp.Config;
using IfsahApp.Infrastructure.Services.AdUser;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace IfsahApp.Web.Controllers;

#if DEBUG
[AllowAnonymous]
public class DevLoginController(IAdUserService adUserService, IOptions<DevUserOptions> devUserOptions) : Controller
{
    private readonly IAdUserService _adUserService = adUserService;
    private readonly DevUserOptions _devUserOptions = devUserOptions.Value;

    [HttpGet]
    public IActionResult Index()
    {
        // Get list of fake users for dropdown
        var fakeUsers = (_adUserService as FakeAdUserService)?.Users ?? [];
        return View(fakeUsers);
    }

    [HttpPost]
    public IActionResult Index(string selectedSamAccountName)
    {
        if (string.IsNullOrEmpty(selectedSamAccountName))
            return RedirectToAction(nameof(Index));

        // Set the selected dev user
        _devUserOptions.SamAccountName = selectedSamAccountName;

        // Redirect to your normal login action
        return RedirectToAction("Login", "Account");
    }
}
#endif

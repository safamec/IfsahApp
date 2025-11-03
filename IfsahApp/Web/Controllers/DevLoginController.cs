using IfsahApp.Config;
using IfsahApp.Infrastructure.Services.AdUser;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace IfsahApp.Web.Controllers;

#if DEBUG
[AllowAnonymous]
public class DevLoginController : Controller
{
    private readonly IAdUserService _adUserService;
    private readonly DevUserOptions _devUserOptions;

    public DevLoginController(IAdUserService adUserService, IOptions<DevUserOptions> devUserOptions)
    {
        _adUserService = adUserService;
        _devUserOptions = devUserOptions.Value;
    }

    [HttpGet]
    public IActionResult Index()
    {
        // قائمة المستخدمين الوهميين لعرضها في الـView
        var fakeUsers = (_adUserService as FakeAdUserService)?.Users ?? [];
        return View(fakeUsers);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Index(string selectedSamAccountName)
    {
        if (string.IsNullOrWhiteSpace(selectedSamAccountName))
            return RedirectToAction(nameof(Index));

        // نحدد المستخدم المختار
        _devUserOptions.SamAccountName = selectedSamAccountName;

        // نرجع لعملية الدخول الطبيعية
        return RedirectToAction("Login", "Account");
    }
}
#endif

using IfsahApp.Core.Enums;
using IfsahApp.Core.ViewModels;
using IfsahApp.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[AllowAnonymous]
public class ErrorController : Controller
{
    private readonly IEnumERLocalizer _enumLocalizer;

    public ErrorController(IEnumERLocalizer enumLocalizer)
    {
        _enumLocalizer = enumLocalizer;
    }

    [Route("Error/{statusCode}")]
    public IActionResult HttpStatusCodeHandler(int statusCode)
    {
        var code = (HttpStatusCode)statusCode;

        var model = new ErrorViewModel
        {
            StatusCode = statusCode,
            ErrorTitle = _enumLocalizer.LocalizeEnumTitle(code),
            ErrorDescription = _enumLocalizer.LocalizeEnumDescription(code)
        };

        return View("Error", model);
    }
}

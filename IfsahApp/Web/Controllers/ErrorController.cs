using IfsahApp.Core.Enums;
using IfsahApp.Infrastructure.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Http;

namespace IfsahApp.Web.Controllers
{
    public class ErrorController : Controller
    {
        private readonly ILogger<ErrorController> _logger;
        private readonly IHttpStatusLocalizer _errorLocalizer;

        public ErrorController(ILogger<ErrorController> logger, IHttpStatusLocalizer errorLocalizer)
        {
            _logger = logger;
            _errorLocalizer = errorLocalizer;
        }

        [Route("Error/{statusCode?}")]
        public IActionResult HttpStatusCodeHandler(int? statusCode = null)
        {
            var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
            var status = statusCode ?? (int)HttpStatusCode.InternalServerError;

            _logger.LogError("حدث خطأ {StatusCode} - Request ID: {RequestId}", status, requestId);

            var errorViewModel = ErrorService.GetErrorInfo(status, _errorLocalizer, requestId);

            return View("Error", errorViewModel);
        }

        [Route("Error")]
        public IActionResult Error()
        {
            return HttpStatusCodeHandler((int)HttpStatusCode.InternalServerError);
        }

        // إضافة action لتغيير اللغة إذا needed
        [HttpPost]
        public IActionResult SetLanguage(string culture, string returnUrl)
        {
            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
            );

            return LocalRedirect(returnUrl);
        }
    }
}
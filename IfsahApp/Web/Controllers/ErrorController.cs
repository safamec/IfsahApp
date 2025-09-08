using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using IfsahApp.Models;

public class ErrorController : Controller
{
    [Route("Error/{statusCode}")]
    public IActionResult HttpStatusCodeHandler(int statusCode)
    {
        switch (statusCode)
        {
            case 401:
                return View("401"); 
            case 403:
                return View("403"); 
            case 404:
                return View("404");
            default:
                return View("Error", new ErrorViewModel
                {
                    RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
                });
        }
    }

    [Route("Error")]
    public IActionResult Error()
    {
        return View("Error", new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}

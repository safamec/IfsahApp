using Microsoft.AspNetCore.Mvc;
using IfsahApp.Core.Models;
using IfsahApp.Infrastructure.Data;
using IfsahApp.Core.ViewModels;
using System.IO;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using IfsahApp.Infrastructure.Services;

namespace IfsahApp.Web.Controllers;

public class SubmitDisclosureController : Controller
{
    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    public IActionResult CreatePost()
    {
        // توليد رقم البلاغ عشوائي
        var reportNumber = new Random().Next(1000, 9999);

        // إعادة توجيه إلى صفحة الشكر مع رقم البلاغ
        return RedirectToAction("SubmitDisclosure", new { reportNumber = reportNumber });
    }

    [HttpGet]
    public IActionResult SubmitDisclosure(int reportNumber)
    {
        ViewData["ReportNumber"] = reportNumber;
        return View();
    }
}

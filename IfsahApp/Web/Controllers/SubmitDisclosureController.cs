using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using IfsahApp.Hubs;                 // NotificationHub
using System;
using System.Threading.Tasks;

namespace IfsahApp.Web.Controllers
{
    public class SubmitDisclosureController : Controller
    {
        private readonly IHubContext<NotificationHub> _hub;
        private readonly ILogger<SubmitDisclosureController> _logger;

        public SubmitDisclosureController(
            IHubContext<NotificationHub> hub,
            ILogger<SubmitDisclosureController> logger)
        {
            _hub = hub;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePost()
        {
            // توليد رقم بلاغ لطيف (يمكنك استبداله بالتخزين الفعلي في DB)
            var reportNumber = $"DISC-{RandomString(8)}";

            // (اختياري) هنا تحفظين البلاغ في قاعدة البيانات لو حابة

            // بثّ إشعار فوري لكل الإداريين
            try
            {
                await _hub.Clients.Group("admins").SendAsync("Notify", new
                {
                    id        = reportNumber,
                    eventType = "NewDisclosure",
                    message   = $"بلاغ جديد: {reportNumber}",
                    createdAt = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SignalR broadcast failed for {Report}", reportNumber);
            }

            // الانتقال لصفحة الشكر/التأكيد
            return RedirectToAction(nameof(SubmitDisclosure), new { reportNumber });
        }

        [HttpGet]
        public IActionResult SubmitDisclosure(string reportNumber)
        {
            ViewData["ReportNumber"] = reportNumber;
            return View();
        }

        // مولّد بسيط لسلسلة رُمزية
        private static string RandomString(int length)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var rng = Random.Shared;
            var buffer = new char[length];
            for (int i = 0; i < length; i++)
                buffer[i] = chars[rng.Next(chars.Length)];
            return new string(buffer);
        }
    }
}

using System.Threading.Tasks;
using IfsahApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace IfsahApp.Controllers
{
    [Route("api/email")]
    public class EmailController : Controller
    {
        private readonly IEmailService _email;

        public EmailController(IEmailService email)
        {
            _email = email;
        }

        [HttpGet("send")]
        public async Task<IActionResult> SendTest()
        {
            await _email.SendAsync("ahmed.s.alwahaibi@mem.gov.om", "Hello from .NET", "<p>This is a test email.</p>", isHtml: true);
            return Ok(new { message = "Email sent!" });
        }
    }
}

using IfsahApp.Infrastructure.Services.Email;
using Microsoft.AspNetCore.Mvc;

namespace IfsahApp.Web.Controllers;

[Route("api/email")]
public class EmailController(IEmailService email) : Controller
{
    private readonly IEmailService _email = email;

    // GET /api/email/send
    [HttpGet("send")]
    public async Task<IActionResult> SendTest()
    {
        await _email.SendAsync("ahmed.s.alwahaibi@mem.gov.om", "Hello from .NET", "<p>This is a test email.</p>", isHtml: true);
        return Ok(new { message = "Email sent!" });
    }
}

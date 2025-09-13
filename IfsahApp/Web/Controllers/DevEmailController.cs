using System.Threading.Tasks;
using IfsahApp.Infrastructure.Services.Email;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace IfsahApp.Web.Controllers
{
    [ApiController]
    [Route("dev/email")]
    public class DevEmailController : ControllerBase
    {
        private readonly IEmailService _email;
        private readonly IOptions<SmtpSettings> _smtp;

        public DevEmailController(IEmailService email, IOptions<SmtpSettings> smtp)
        {
            _email = email;
            _smtp = smtp;
        }

        [HttpGet("send")]
        public async Task<IActionResult> Send(string to)
        {
            await _email.SendAsync(to, "IfsahApp Test", "<h2>Hello from IfsahApp 🚀</h2>", isHtml: true);
            return Ok("✅ Email sent successfully");
        }

        [HttpGet("config")]
        public IActionResult Config()
        {
            var c = _smtp.Value;
            return Ok(new
            {
                c.Host,
                c.Port,
                c.EnableSsl,
                c.FromAddress,
                UserName = string.IsNullOrWhiteSpace(c.UserName) ? "<EMPTY>" : "<SET>",
                Password = string.IsNullOrWhiteSpace(c.Password) ? "<EMPTY>" : "<SET>"
            });
        }
    }
}

using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace IfsahApp.Infrastructure.Services.Email
{
    public class GmailEmailService : IEmailService
    {
        private readonly SmtpSettings _cfg;
        private readonly ILogger<GmailEmailService> _log;

        public GmailEmailService(IOptions<SmtpSettings> cfg, ILogger<GmailEmailService> log)
        {
            _cfg = cfg.Value;
            _log = log;
        }

        public async Task SendAsync(string to, string subject, string body, bool isHtml = false, CancellationToken ct = default)
        {
            var fromAddr = string.IsNullOrWhiteSpace(_cfg.FromAddress) ? _cfg.UserName : _cfg.FromAddress;

            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(_cfg.FromName ?? string.Empty, fromAddr));
            msg.To.Add(MailboxAddress.Parse(to));
            msg.Subject = subject;

            var builder = new BodyBuilder();
            if (isHtml) builder.HtmlBody = body;
            else builder.TextBody = body;
            msg.Body = builder.ToMessageBody();

            using var client = new SmtpClient
            {
                SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            };

#if DEBUG
            // DEV ONLY: بعض الشبكات تحجب CRL/OCSP، هذا يتجاوز فحص الإبطال في التطوير فقط
            client.CheckCertificateRevocation = false;
#endif

            // Gmail يستخدم STARTTLS على 587
            var secure = _cfg.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;

            await client.ConnectAsync(_cfg.Host, _cfg.Port, secure, ct);
            await client.AuthenticateAsync(_cfg.UserName, _cfg.Password, ct); // هنا بيستخدم القيم من appsettings.json
            await client.SendAsync(msg, ct);
            await client.DisconnectAsync(true, ct);
        }
    }
}

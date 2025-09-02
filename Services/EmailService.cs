using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IfsahApp.Services
{
    // Configuration POCO bound from appsettings.json
    public class SmtpSettings
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 25;
        public bool EnableSsl { get; set; } = false;
        public string? UserName { get; set; }
        public string? Password { get; set; }
        public string FromAddress { get; set; } = "no-reply@example.com";
        public string? FromName { get; set; }
    }

    public interface IEmailService
    {
        Task SendAsync(string to, string subject, string body, bool isHtml = false, CancellationToken ct = default);
    }

    public class SmtpEmailService : IEmailService
    {
        private readonly SmtpSettings _settings;
        private readonly ILogger<SmtpEmailService> _logger;

        public SmtpEmailService(IOptions<SmtpSettings> options, ILogger<SmtpEmailService> logger)
        {
            _settings = options.Value;
            _logger = logger;
        }

        public async Task SendAsync(string to, string subject, string body, bool isHtml = false, CancellationToken ct = default)
        {
            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                Credentials = string.IsNullOrWhiteSpace(_settings.UserName)
                    ? CredentialCache.DefaultNetworkCredentials
                    : new NetworkCredential(_settings.UserName, _settings.Password)
            };

            var fromName = string.IsNullOrWhiteSpace(_settings.FromName) ? _settings.FromAddress : _settings.FromName;
            using var message = new MailMessage
            {
                From = new MailAddress(_settings.FromAddress, fromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = isHtml
            };

            message.To.Add(to);

            try
            {
                // SmtpClient has no real async; wrap in Task.Run
                await Task.Run(() => client.Send(message), ct);
                _logger.LogInformation("Email sent to {To} with subject {Subject}", to, subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email to {To}", to);
                throw;
            }
        }
    }
}

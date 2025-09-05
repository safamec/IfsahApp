using System;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IfsahApp.Services.Email;

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
            // Wrap Send() in Task.Run because SmtpClient.Send is sync
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

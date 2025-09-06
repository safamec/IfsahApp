using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace IfsahApp.Services.Email;

/// <summary>
/// Fake email sender for Development - logs emails instead of sending.
/// </summary>
public class FakeEmailService : IEmailService
{
    private readonly ILogger<FakeEmailService> _logger;

    public FakeEmailService(ILogger<FakeEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string to, string subject, string body, bool isHtml = false, CancellationToken ct = default)
    {
        _logger.LogInformation("📧 [FAKE EMAIL] To={To}, Subject={Subject}, Body={Body}", to, subject, body);
        return Task.CompletedTask;
    }
}

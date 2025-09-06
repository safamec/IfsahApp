namespace IfsahApp.Infrastructure.Services.Email;

/// <summary>
/// Fake email sender for Development - logs emails instead of sending.
/// </summary>
public class FakeEmailService(ILogger<FakeEmailService> logger) : IEmailService
{
    private readonly ILogger<FakeEmailService> _logger = logger;

    public Task SendAsync(string to, string subject, string body, bool isHtml = false, CancellationToken ct = default)
    {
        _logger.LogInformation("📧 [FAKE EMAIL] To={To}, Subject={Subject}, Body={Body}", to, subject, body);
        return Task.CompletedTask;
    }
}

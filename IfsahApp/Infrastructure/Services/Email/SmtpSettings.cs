namespace IfsahApp.Infrastructure.Services.Email;

/// <summary>
/// SMTP configuration bound from appsettings.json
/// </summary>
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
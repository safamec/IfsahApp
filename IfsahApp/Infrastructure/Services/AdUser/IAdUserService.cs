namespace IfsahApp.Infrastructure.Services.AdUser;

public class AdUser
{
    public string SamAccountName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
}

public interface IAdUserService
{
    Task<AdUser?> FindByWindowsIdentityAsync(string windowsIdentityName, CancellationToken ct = default);
}


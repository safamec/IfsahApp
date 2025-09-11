using IfsahApp.Config;
using Microsoft.Extensions.Options;

namespace IfsahApp.Infrastructure.Services.AdUser;

public class FakeAdUserService(IOptions<DevUserOptions> devUserOptions) : IAdUserService
{
    private readonly DevUserOptions _devUserOptions = devUserOptions.Value;

    // Expose users as a public property so controllers can access them
    public List<AdUser> Users { get; } =
    [
        new AdUser
        {
            SamAccountName = "ahmed.wahaibi",
            DisplayName = "Ahmed Al Wahaibi",
            Email = "ahmed@example.com",
            Department = "Admin"
        },
        new AdUser
        {
            SamAccountName = "fatima.harthy",
            DisplayName = "Fatima Al Harthy",
            Email = "fatima@example.com",
            Department = "Examiner"
        },
        new AdUser
        {
            SamAccountName = "mohammed.said",
            DisplayName = "Mohammed Al Said",
            Email = "mohammed@example.com",
            Department = "Employee"
        },
           new AdUser
        {
            SamAccountName = "adam.ahmed",
            DisplayName = "Adam Al Wahaibi",
            Email = "adam@example.com",
            Department = "Guest"
        }
    ];

    public Task<AdUser?> FindByWindowsIdentityAsync(string windowsIdentityName, CancellationToken ct = default)
    {
        // Use Dev-selected user if present, otherwise fallback to identity
        string sam = _devUserOptions.SamAccountName ?? windowsIdentityName.Split('\\').Last();

        var user = Users.FirstOrDefault(u =>
            string.Equals(u.SamAccountName, sam, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(user);
    }
}

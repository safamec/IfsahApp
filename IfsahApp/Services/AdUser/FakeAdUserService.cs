namespace IfsahApp.Services;

public sealed class FakeAdUserService : IAdUserService
{
    private static readonly List<AdUser> _users = new()
        {
            new AdUser { SamAccountName = "ahmed", DisplayName = "Ahmed Al Wahaibi", Email = "ahmed@example.com", Department = "IT" },
            new AdUser { SamAccountName = "jdoe", DisplayName = "John Doe", Email = "jdoe@example.com", Department = "IT" },
            new AdUser { SamAccountName = "asmith", DisplayName = "Alice Smith", Email = "asmith@example.com", Department = "HR" },
            new AdUser { SamAccountName = "bking", DisplayName = "Bob King", Email = "bking@example.com", Department = "Finance" }
        };

    public Task<AdUser?> FindByWindowsIdentityAsync(string windowsIdentityName, CancellationToken ct = default)
    {
        var parts = windowsIdentityName.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        string sam = parts.Length == 2 ? parts[1] : parts[0];

        var user = _users.FirstOrDefault(u =>
            string.Equals(u.SamAccountName, sam, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(user);
    }
}

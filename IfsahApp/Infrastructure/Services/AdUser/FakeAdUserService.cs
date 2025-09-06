using IfsahApp.Options;
using Microsoft.Extensions.Options;

namespace IfsahApp.Services.AdUser
{
    public class FakeAdUserService : IAdUserService
    {
        private readonly DevUserOptions _devUserOptions;

        // Predefined fake users for dev/testing
        private readonly List<AdUser> _users = new()
        {
            new AdUser { SamAccountName = "ahmed", DisplayName = "Ahmed Al Wahaibi", Email = "ahmed@corp.com", Department = "IT" },
            new AdUser { SamAccountName = "jdoe", DisplayName = "John Doe", Email = "jdoe@corp.com", Department = "HR" }
        };

        public FakeAdUserService(IOptions<DevUserOptions> devUserOptions)
        {
            _devUserOptions = devUserOptions.Value;
        }

        public Task<AdUser?> FindByWindowsIdentityAsync(string windowsIdentityName, CancellationToken ct = default)
        {
            // Use CLI-selected user if present, otherwise fallback to identity
            string sam = _devUserOptions.SamAccountName ?? windowsIdentityName.Split('\\').Last();

            var user = _users.FirstOrDefault(u =>
                string.Equals(u.SamAccountName, sam, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(user);
        }
    }
}

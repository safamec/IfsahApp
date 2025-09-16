using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IfsahApp.Config;
using Microsoft.Extensions.Options;

namespace IfsahApp.Infrastructure.Services.AdUser
{
    public class FakeAdUserService(IOptions<DevUserOptions> devUserOptions) : IAdUserService
    {
        private readonly DevUserOptions _devUserOptions = devUserOptions.Value;

        // Expose users as a public property so controllers can access them
        public List<AdUser> Users { get; } =
        [
            new AdUser
            {
                SamAccountName = "Admin",
                DisplayName = "Ahmed Al Wahaibi",
                Email = "mgk390@gmail.com",
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

        // NEW: للبحث السريع (AJAX)
        public Task<IReadOnlyList<AdUser>> SearchAsync(string query, int take = 8, CancellationToken ct = default)
        {
            if (ct.IsCancellationRequested)
                return Task.FromCanceled<IReadOnlyList<AdUser>>(ct);

            query = (query ?? string.Empty).Trim();
            take = take <= 0 ? 8 : take;

            List<AdUser> results;

            if (string.IsNullOrEmpty(query))
            {
                results = Users.Take(take).ToList();
            }
            else
            {
                results = Users
                    .Where(u =>
                        (u.SamAccountName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (u.DisplayName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (u.Email?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (u.Department?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
                    .Take(take)
                    .ToList();
            }

            return Task.FromResult<IReadOnlyList<AdUser>>(results);
        }
    }
}

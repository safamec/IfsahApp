using System.DirectoryServices.Protocols;
using System.Net;

namespace IfsahApp.Infrastructure.Services.AdUser;

public sealed class LdapAdUserService : IAdUserService
{
    private readonly IConfiguration _config;
    private readonly ILogger<LdapAdUserService> _logger;

    public LdapAdUserService(IConfiguration config, ILogger<LdapAdUserService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<AdUser?> FindByWindowsIdentityAsync(string windowsIdentityName, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            // Strip domain prefix if present (CORP\ahmed -> ahmed)
            var parts = windowsIdentityName.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            string sam = parts.Length == 2 ? parts[1] : parts[0];

            // Load LDAP settings from config
            string ldapServer = _config["Ldap:Server"] ?? "ldap.company.com";
            int port = int.TryParse(_config["Ldap:Port"], out var p) ? p : 389;
            string bindUser = _config["Ldap:BindUser"] ?? "";
            string bindPassword = _config["Ldap:BindPassword"] ?? "";
            string searchBase = _config["Ldap:SearchBase"] ?? "DC=company,DC=com";

            try
            {
                using var connection = new LdapConnection(new LdapDirectoryIdentifier(ldapServer, port));
                connection.Credential = new NetworkCredential(bindUser, bindPassword);
                connection.AuthType = AuthType.Negotiate;
                connection.Bind();

                var request = new SearchRequest(
                    searchBase,
                    $"(&(objectClass=user)(sAMAccountName={sam}))",
                    SearchScope.Subtree,
                    "sAMAccountName", "displayName", "mail", "department"
                );

                var response = (SearchResponse)connection.SendRequest(request);

                var entry = response.Entries.Cast<SearchResultEntry>().FirstOrDefault();
                if (entry != null)
                {
                    return new AdUser
                    {
                        SamAccountName = entry.Attributes["sAMAccountName"]?[0]?.ToString() ?? sam,
                        DisplayName = entry.Attributes["displayName"]?[0]?.ToString() ?? sam,
                        Email = entry.Attributes["mail"]?[0]?.ToString() ?? string.Empty,
                        Department = entry.Attributes["department"]?[0]?.ToString() ?? string.Empty
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LDAP lookup failed for {SamAccountName}", sam);
            }

            return null;
        }, ct);
    }

    public Task<IReadOnlyList<AdUser>> SearchAsync(string query, int take = 8, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}
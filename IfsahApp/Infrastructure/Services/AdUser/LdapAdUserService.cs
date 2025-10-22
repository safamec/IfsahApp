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
            try
            {
                // Normalize username (e.g., MEM\ahmed → ahmed)
                var parts = windowsIdentityName.Split('\\', StringSplitOptions.RemoveEmptyEntries);
                string sam = parts.Length == 2 ? parts[1] : parts[0];

                // Load LDAP config
                string ldapServer = _config["Ldap:LdapServer"] ?? "10.193.8.65";
                string domain = _config["Ldap:Domain"] ?? "DC=mem,DC=local";
                string username = _config["Ldap:Username"] ?? "ldap-micro@mem.local";
                string password = _config["Ldap:Password"] ?? string.Empty;

                _logger.LogInformation("Connecting to LDAP server {Server} for user lookup {User}", ldapServer, sam);

                // Create LDAP connection
                using var connection = new LdapConnection(new LdapDirectoryIdentifier(ldapServer, 389));
                connection.AuthType = AuthType.Negotiate;
                connection.Credential = new NetworkCredential(username, password);
                connection.Bind();

                // Build search filter
                string filter = $"(&(objectClass=user)(sAMAccountName={sam}))";

                // Build search request
                var request = new SearchRequest(
                    domain,
                    filter,
                    SearchScope.Subtree,
                    "sAMAccountName",
                    "displayName",
                    "mail",
                    "department"
                );

                // Execute search
                var response = (SearchResponse)connection.SendRequest(request);

                var entry = response.Entries.Cast<SearchResultEntry>().FirstOrDefault();
                if (entry != null)
                {
                    _logger.LogInformation("LDAP user found: {User}", sam);

                    return new AdUser
                    {
                        SamAccountName = entry.Attributes["sAMAccountName"]?[0]?.ToString() ?? sam,
                        DisplayName = entry.Attributes["displayName"]?[0]?.ToString() ?? sam,
                        Email = entry.Attributes["mail"]?[0]?.ToString() ?? string.Empty,
                        Department = entry.Attributes["department"]?[0]?.ToString() ?? string.Empty
                    };
                }

                _logger.LogWarning("LDAP user {User} not found in directory.", sam);
            }
            catch (LdapException ex)
            {
                _logger.LogError(ex, "LDAP connection or query failed for {User}", windowsIdentityName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while querying LDAP for {User}", windowsIdentityName);
            }

            return null;
        }, ct);
    }

    public Task<IReadOnlyList<AdUser>> SearchAsync(string query, int take = 8, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}

using System.DirectoryServices.Protocols;
using System.Net;
using Microsoft.Extensions.Caching.Memory;

namespace IfsahApp.Infrastructure.Services.AdUser;

public sealed class LdapAdUserService : IAdUserService
{
    private readonly IConfiguration _config;
    private readonly ILogger<LdapAdUserService> _logger;
    private readonly IMemoryCache _cache;

    public LdapAdUserService(IConfiguration config, ILogger<LdapAdUserService> logger, IMemoryCache cache)
    {
        _config = config;
        _logger = logger;
        _cache = cache;
    }

    public async Task<AdUser?> FindByWindowsIdentityAsync(string windowsIdentityName, CancellationToken ct = default)
    {
        var sam = NormalizeUsername(windowsIdentityName);

        // 🔹 Check cache first
        if (_cache.TryGetValue(sam, out AdUser cached))
        {
            _logger.LogInformation("LDAP cache hit for {User}", sam);
            return cached;
        }

        return await Task.Run(() =>
        {
            try
            {
                _logger.LogInformation("LdapAdUserService hit");
                string ldapServer = _config["Ldap:LdapServer"] ?? "10.193.8.65";
                string domain = _config["Ldap:Domain"] ?? "DC=mem,DC=local";
                string username = _config["Ldap:Username"] ?? "ldap-micro@mem.local";
                string password = _config["Ldap:Password"] ?? string.Empty;

                _logger.LogInformation("Connecting to LDAP server {Server} for user lookup {User}", ldapServer, sam);

                using var connection = new LdapConnection(new LdapDirectoryIdentifier(ldapServer, 389));
                connection.AuthType = AuthType.Negotiate;
                connection.Credential = new NetworkCredential(username, password);
                connection.Bind();

                string filter = $"(&(objectClass=user)(sAMAccountName={sam}))";
                var request = new SearchRequest(domain, filter, SearchScope.Subtree,
                    "sAMAccountName", "displayName", "mail", "department");

                var response = (SearchResponse)connection.SendRequest(request);
                var entry = response.Entries.Cast<SearchResultEntry>().FirstOrDefault();

                if (entry != null)
                {
                    var adUser = new AdUser
                    {
                        SamAccountName = entry.Attributes["sAMAccountName"]?[0]?.ToString() ?? sam,
                        DisplayName = entry.Attributes["displayName"]?[0]?.ToString() ?? sam,
                        Email = entry.Attributes["mail"]?[0]?.ToString() ?? string.Empty,
                        Department = entry.Attributes["department"]?[0]?.ToString() ?? string.Empty
                    };

                    _logger.LogInformation("LDAP user found: {User}", sam);

                    // 🔹 Cache the result for 10 minutes
                    _cache.Set(sam, adUser, TimeSpan.FromMinutes(10));
                    return adUser;
                }

                _logger.LogWarning("LDAP user {User} not found in directory.", sam);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LDAP query failed for {User}", windowsIdentityName);
            }

            return null;
        }, ct);
    }

public async Task<AdUser?> FindByCredentialsAsync(string username, string password, CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        return null;

    var sam = NormalizeUsername(username);

    // 🔹 Check cache first (optional, you may skip cache for login)
    if (_cache.TryGetValue(sam, out AdUser cached))
        return cached;

    return await Task.Run(() =>
    {
        try
        {
            _logger.LogInformation("Attempting LDAP login for {User}", sam);

            string ldapServer = _config["Ldap:LdapServer"] ?? "10.193.8.65";
            string domain = _config["Ldap:Domain"] ?? "DC=mem,DC=local";

            using var connection = new LdapConnection(new LdapDirectoryIdentifier(ldapServer, 389));
            connection.AuthType = AuthType.Negotiate;
            connection.Credential = new NetworkCredential(sam, password); // use user-provided credentials
            connection.Bind(); // throws if invalid

            // 🔹 If bind succeeds, fetch user details
            string filter = $"(&(objectClass=user)(sAMAccountName={sam}))";
            var request = new SearchRequest(domain, filter, SearchScope.Subtree,
                "sAMAccountName", "displayName", "mail", "department");

            var response = (SearchResponse)connection.SendRequest(request);
            var entry = response.Entries.Cast<SearchResultEntry>().FirstOrDefault();

            if (entry != null)
            {
                var adUser = new AdUser
                {
                    SamAccountName = entry.Attributes["sAMAccountName"]?[0]?.ToString() ?? sam,
                    DisplayName = entry.Attributes["displayName"]?[0]?.ToString() ?? sam,
                    Email = entry.Attributes["mail"]?[0]?.ToString() ?? string.Empty,
                    Department = entry.Attributes["department"]?[0]?.ToString() ?? string.Empty
                };

                _logger.LogInformation("LDAP login successful for {User}", sam);

                // 🔹 Optional: cache user for a short period
                _cache.Set(sam, adUser, TimeSpan.FromMinutes(5));

                return adUser;
            }
        }
        catch (LdapException ex)
        {
            _logger.LogWarning(ex, "Invalid LDAP credentials for {User}", sam);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LDAP error for {User}", sam);
        }

        return null;
    }, ct);
}

    private static string NormalizeUsername(string identityName)
    {
        var parts = identityName.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 ? parts[1] : parts[0];
    }

    public Task<IReadOnlyList<AdUser>> SearchAsync(string query, int take = 8, CancellationToken ct = default)
        => throw new NotImplementedException();
}

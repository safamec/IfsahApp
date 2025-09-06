using IfsahApp.Config;
using System.Security.Claims;
using IfsahApp.Infrastructure.Services.AdUser;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication;

namespace IfsahApp.Infrastructure.Services.Authentication;

public class FakeAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IAdUserService _adUserService;
    private readonly DevUserOptions _devUserOptions;

    public FakeAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IAdUserService adUserService,
        IOptions<DevUserOptions> devUserOptions
    ) : base(options, logger, encoder)
    {
        _adUserService = adUserService;
        _devUserOptions = devUserOptions.Value;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string windowsIdentityName = _devUserOptions.SamAccountName ?? Environment.UserName;

        var adUser = await _adUserService.FindByWindowsIdentityAsync(windowsIdentityName);
        if (adUser == null)
            return AuthenticateResult.Fail($"User '{windowsIdentityName}' not found in AD.");

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, adUser.SamAccountName),
            new Claim(ClaimTypes.GivenName, adUser.DisplayName),
            new Claim(ClaimTypes.Email, adUser.Email),
            new Claim("Department", adUser.Department)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using IfsahApp.Infrastructure.Services.AdUser;
using IfsahApp.Infrastructure.Services.Authentication;
using IfsahApp.Config;

namespace IfsahApp.Utils;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppAuthentication(
        this IServiceCollection services,
        IWebHostEnvironment env)
    {
        bool isDev = env.IsDevelopment();
        bool isStaging = env.IsStaging();
        bool isProd = env.IsProduction();

        // ---------- (1) DevUserOptions for Dev UI login ----------
        if (isDev)
        {
            services.Configure<DevUserOptions>(opt => opt.SamAccountName = string.Empty);
        }

        // ---------- (2) Log which mode is active ----------
        services.AddSingleton(provider =>
        {
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("AuthSetup");

            if (isDev)
                logger.LogInformation("Authentication mode: Development (Fake login + Fake AD)");
            else if (isStaging)
                logger.LogInformation("Authentication mode: Staging (Cookies + Real AD via LDAP)");
            else if (isProd)
                logger.LogInformation("Authentication mode: Production (Cookies + Real AD via LDAP)");

            return new object(); // dummy service for logging
        });

        // ---------- (3) Unified Cookie Authentication ----------
        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.LoginPath = "/Account/Login";
                options.LogoutPath = "/Account/Logout";
                options.AccessDeniedPath = "/Account/AccessDenied";

                // ✅ Prevent redirect loops (HTTP 414)
                options.Events.OnRedirectToLogin = context =>
                {
                    // Allow the login page itself
                    var path = context.Request.Path;
                    if (path.StartsWithSegments("/Account/Login") ||
                        path.StartsWithSegments("/Account/AccessDenied") ||
                        path.StartsWithSegments("/Account/Logout"))
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    }

                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };
            });

        // ---------- (4) Choose AD service ----------
        if (isDev)
            services.AddSingleton<IAdUserService, FakeAdUserService>();
        else
            services.AddSingleton<IAdUserService, LdapAdUserService>();

        // ---------- (5) Authorization ----------
        services.AddAuthorization();

        return services;
    }
}

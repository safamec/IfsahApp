using Microsoft.AspNetCore.Authentication.Cookies;
using IfsahApp.Infrastructure.Services.AdUser;
using IfsahApp.Config;

namespace IfsahApp.Utils;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAppAuthentication(
        this IServiceCollection services,
        IWebHostEnvironment env)
    {
        // ----------------------------------------
        // 1) Cookie Authentication (all environments)
        // ----------------------------------------
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Account/Login";          // Redirect if not authenticated
                options.AccessDeniedPath = "/Account/AccessDenied";
                options.SlidingExpiration = true;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
            });

        // ----------------------------------------
        // 2) Environment-specific AD service
        // ----------------------------------------
        if (env.IsDevelopment())
        {
            // Fake AD service for DEV
            services.Configure<DevUserOptions>(opt => opt.SamAccountName = string.Empty);
            services.AddSingleton<IAdUserService, FakeAdUserService>();
            Console.WriteLine("👨‍💻 DEV MODE: Fake AD + Cookie auth.");
        }
        else
        {
            // Real AD service for STAGE/PROD
            services.AddSingleton<IAdUserService, LdapAdUserService>();
            Console.WriteLine("🪟 PROD/STAGE: Silent AD attempt + Cookie fallback.");
        }

        // ----------------------------------------
        // 3) Authorization
        // ----------------------------------------
        services.AddAuthorization();

        return services;
    }
}

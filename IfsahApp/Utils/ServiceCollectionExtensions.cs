using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
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

        // 1️⃣ Register DevUserOptions for UI login
        if (isDev)
        {
            services.Configure<DevUserOptions>(opt => opt.SamAccountName = string.Empty); // initially empty
        }

        // 2️⃣ Logging mode
        services.AddSingleton(provider =>
        {
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("AuthSetup");

            if (isDev)
            {
                logger.LogInformation("Authentication mode: Development (Fake login + Fake AD). Username will be set via UI.");
            }
            else if (isStaging)
            {
                logger.LogInformation("Authentication mode: Staging (Real login + Fake AD)");
            }
            else if (isProd)
            {
                logger.LogInformation("Authentication mode: Production (Real login + Real AD)");
            }

            return new object(); // dummy singleton just for logging
        });

        // 3️⃣ Register authentication & AD service
        if (isDev)
        {
            services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = "Cookies";           // cookies for persistence
                    options.DefaultChallengeScheme = "Fake";     // challenge with Fake handler
                })
                .AddCookie("Cookies") // supports SignInAsync/SignOutAsync
                .AddScheme<AuthenticationSchemeOptions, FakeAuthHandler>("Fake", options =>
                {
                    options.TimeProvider = TimeProvider.System;
                });

            services.AddSingleton<IAdUserService, FakeAdUserService>();
        }
        else if (isStaging)
        {
            services
                .AddAuthentication(NegotiateDefaults.AuthenticationScheme)
                .AddNegotiate(options =>
                {
                    options.TimeProvider = TimeProvider.System;
                });

            services.AddSingleton<IAdUserService, FakeAdUserService>();
        }
        else
        {
            services
                .AddAuthentication(NegotiateDefaults.AuthenticationScheme)
                .AddNegotiate(options =>
                {
                    options.TimeProvider = TimeProvider.System;
                });

            services.AddSingleton<IAdUserService, LdapAdUserService>();
        }

        return services;
    }
}

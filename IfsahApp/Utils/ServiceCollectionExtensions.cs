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
        IWebHostEnvironment env,
        string[] args)
    {
        bool isDev = env.IsDevelopment();
        bool isStaging = env.IsStaging();
        bool isProd = env.IsProduction();

        // 1️⃣ Handle CLI user for development
        if (isDev && args.Length > 0)
        {
            string devUser = args[0].TrimStart('-', '/');
            services.Configure<DevUserOptions>(opt => opt.SamAccountName = devUser);
        }

        // 2️⃣ Logging mode
        services.AddSingleton(provider =>
        {
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("AuthSetup");

            if (isDev)
            {
                var options = provider.GetService<IOptions<DevUserOptions>>()?.Value;
                string username = options?.SamAccountName ?? "(not set)";
                logger.LogInformation("Authentication mode: Development (Fake login + Fake AD). Using username: {User}", username);
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
                .AddAuthentication("Fake")
                .AddScheme<AuthenticationSchemeOptions, FakeAuthHandler>("Fake", options =>
                {
                    options.TimeProvider = TimeProvider.System; // ✅ correct place to set TimeProvider
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

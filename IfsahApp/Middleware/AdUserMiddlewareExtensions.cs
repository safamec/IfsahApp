using IfsahApp.Services;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

namespace IfsahApp.Middleware;

public static class AdUserMiddlewareExtensions
{
    public static IApplicationBuilder UseEnsureAdUser(this IApplicationBuilder app, bool isDev)
    {
        return app.Use(async (ctx, next) =>
        {
            if (ctx.User?.Identity?.IsAuthenticated == true)
            {
                var logger = ctx.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("EnsureAdUser");
                var cache = ctx.RequestServices.GetRequiredService<IMemoryCache>();
                var adService = ctx.RequestServices.GetRequiredService<IAdUserService>();

                string? winName = ctx.User.Identity?.Name;
                string sid = ctx.User.FindFirstValue(ClaimTypes.Sid) ?? winName ?? string.Empty;

                if (!string.IsNullOrWhiteSpace(sid))
                {
                    var cacheKey = $"aduser:{sid}";

                    if (!cache.TryGetValue(cacheKey, out AdUser? profile))
                    {
                        if (isDev)
                        {
                            profile = new AdUser
                            {
                                SamAccountName = "ahmed",
                                DisplayName = "Ahmed Al Wahaibi",
                                Email = "ahmed@example.com",
                                Department = "IT"
                            };
                        }
                        else
                        {
                            try
                            {
                                profile = await adService.FindByWindowsIdentityAsync(winName!, ctx.RequestAborted);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "AD lookup failed for {Principal}", winName);
                                ctx.Response.Redirect("/Account/AccessDenied");
                                return;
                            }
                        }

                        if (profile == null)
                        {
                            logger.LogWarning("Windows principal {Principal} not found in AD. Denying access.", winName);
                            ctx.Response.Redirect("/Account/AccessDenied");
                            return;
                        }

                        cache.Set(cacheKey, profile, TimeSpan.FromMinutes(10));
                    }

                    ctx.Items["AdUser"] = profile;
                }
            }

            await next();
        });
    }
}

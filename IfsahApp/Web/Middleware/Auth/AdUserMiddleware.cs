using IfsahApp.Infrastructure.Services.AdUser;

namespace IfsahApp.Web.Middleware.Auth;

public class AdUserMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context, IAdUserService adService)
    {
        // 1) Check if IIS sent Windows Identity
        string? identityName =
            context.User.Identity?.IsAuthenticated == true
                ? context.User.Identity?.Name
                : null;

        // 2) If Windows identity not provided → do nothing (cookie auth will take over)
        if (string.IsNullOrWhiteSpace(identityName))
        {
            await _next(context);
            return;
        }

        // 3) Lookup AD user
        AdUser? adUser = await adService.FindByWindowsIdentityAsync(identityName);

        // 4) Add to HttpContext if found
        if (adUser is not null)
        {
            context.Items["AdUser"] = adUser;
        }

        await _next(context);
    }
}

using IfsahApp.Infrastructure.Services.AdUser;

namespace IfsahApp.Web.Middleware.Auth;

public class AdUserMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context, IAdUserService adService)
    {
        // Get the Windows identity name; fallback to environment user
        string identityName = context.User.Identity?.Name ?? Environment.UserName;

        // Lookup AD user
        AdUser? adUser = await adService.FindByWindowsIdentityAsync(identityName);

        // Only inject into HttpContext if user is found
        if (adUser is not null)
        {
            context.Items["AdUser"] = adUser;
        }

        // Continue the middleware pipeline
        await _next(context);
    }
}

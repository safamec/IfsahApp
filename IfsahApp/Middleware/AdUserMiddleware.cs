using IfsahApp.Services.AdUser;

namespace IfsahApp.Middleware;

public class AdUserMiddleware
{
    private readonly RequestDelegate _next;

    public AdUserMiddleware(RequestDelegate next)
    {
        _next = next;
    }

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

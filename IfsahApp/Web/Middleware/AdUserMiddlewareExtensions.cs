using Microsoft.AspNetCore.Builder;

namespace IfsahApp.Middleware;

public static class AdUserMiddlewareExtensions
{
    public static IApplicationBuilder UseAdUser(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AdUserMiddleware>();
    }
}

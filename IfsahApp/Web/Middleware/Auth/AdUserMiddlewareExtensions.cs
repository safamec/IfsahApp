namespace IfsahApp.Web.Middleware.Auth;

public static class AdUserMiddlewareExtensions
{
    public static IApplicationBuilder UseAdUser(this IApplicationBuilder app)
    {
        return app.UseMiddleware<AdUserMiddleware>();
    }
}

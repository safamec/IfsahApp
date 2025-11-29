using System.Security.Claims;
using System.Reflection;
using IfsahApp.Core.Mapping;
using IfsahApp.Hubs;
using IfsahApp.Infrastructure.Data;
using IfsahApp.Infrastructure.Services;
using IfsahApp.Infrastructure.Services.AdUser;
using IfsahApp.Infrastructure.Services.Email;
using IfsahApp.Infrastructure.Settings;
using IfsahApp.Utils;
using IfsahApp.Web.Middleware.Auth;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;

var options = new WebApplicationOptions
{
    ContentRootPath = Directory.GetCurrentDirectory(),
    WebRootPath     = Path.Combine(Directory.GetCurrentDirectory(), "Web", "wwwroot")
};

var builder = WebApplication.CreateBuilder(options);

// ---------- Configuration Loading ----------
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);
}

// ---------- 1) Database ----------
builder.Services.AddDbContext<ApplicationDbContext>(dbOptions =>
    dbOptions.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---------- 2) AutoMapper ----------
builder.Services.AddAutoMapper(typeof(DisclosureMappingProfile));

// ---------- 3) Email ----------
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddTransient<IEmailService, GmailEmailService>();
}
else
{
    builder.Services.AddTransient<IEmailService, SmtpEmailService>();
}

// ---------- 4) MVC + Localization + Antiforgery ----------
builder.Services.AddLocalization(opts => opts.ResourcesPath = "Resources");

builder.Services
    .AddControllersWithViews(o =>
    {
        o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    })
    .AddViewLocalization()
    .AddDataAnnotationsLocalization()
    .AddRazorOptions(opts =>
    {
        opts.ViewLocationFormats.Clear();
        opts.ViewLocationFormats.Add("/Web/Views/{1}/{0}.cshtml");
        opts.ViewLocationFormats.Add("/Web/Views/Shared/{0}.cshtml");
    })
    .AddSessionStateTempDataProvider();

builder.Services.AddSession();

// ---------- 5) Custom Services ----------
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IAdUserService, FakeAdUserService>();
}
else
{
    builder.Services.AddSingleton<IAdUserService, LdapAdUserService>();
}

builder.Services.AddScoped<ViewRenderService>();
builder.Services.AddScoped<IEnumLocalizer, EnumLocalizer>();
builder.Services.AddScoped<IEnumERLocalizer, EnumERLocalizer>();

// ---------- 6) Authentication & Authorization ----------
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddMemoryCache();
builder.Services.AddAntiforgery(o => o.HeaderName = "RequestVerificationToken");

// ---------- 7) SignalR ----------
builder.Services.AddSignalR();

// ---------- Build Application ----------
var app = builder.Build();

// ---------- 8) Seed Database ----------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    if (app.Environment.IsDevelopment())
    {
        DevDbSeeder.Seed(db);
    }
    else
    {
        DbSeeder.Seed(db, logger);
    }
}

// ---------- 9) Localization ----------
var supportedCultures = new[] { "en", "ar" };
var locOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("ar")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);
app.UseRequestLocalization(locOptions);

// ---------- 10) Middleware Pipeline ----------
app.UseStaticFiles();
app.UseSession();
app.UseRouting();

// ---------- Silent AD Login Middleware ----------
app.Use(async (context, next) =>
{
    var adService = context.RequestServices.GetRequiredService<IAdUserService>();

    if (!context.User.Identity?.IsAuthenticated ?? true)
    {
        try
        {
            // Attempt silent AD login
            var remoteUser = context.Request.Headers["REMOTE_USER"].ToString();
            if (!string.IsNullOrEmpty(remoteUser))
            {
                var user = await adService.FindByWindowsIdentityAsync(remoteUser);
                if (user != null)
                {
                    var claims = new List<Claim> { new Claim(ClaimTypes.Name, user.SamAccountName) };
                    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
                }
            }
        }
        catch
        {
            // Fail silently, no popup
        }
    }

    await next();
});

app.UseAdUser(); // your custom middleware
app.UseAuthentication();
app.UseAuthorization();

// ---------- Debug Middleware (show current Windows user) ----------
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
        Console.WriteLine($"✅ Authenticated as: {context.User.Identity.Name}");
    else
        Console.WriteLine("❌ No authenticated Windows user detected.");

    await next();
});

// ---------- 12) Routes ----------
if (app.Environment.IsDevelopment())
{
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=DevLogin}/{action=Index}/{id?}");
}
else
{
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Account}/{action=Login}/{id?}");
}

// ---------- 13) SignalR Hub ----------
app.MapHub<NotificationHub>("/hubs/notifications");

// ---------- 14) Startup Logs ----------
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("🚀 Environment: {Env}", app.Environment.EnvironmentName);
    logger.LogInformation("🔐 Authentication Scheme: Cookies");
    logger.LogInformation("📧 SMTP Host: {Smtp}", builder.Configuration["Smtp:Host"]);
    logger.LogInformation("📦 DB Path: {Db}", builder.Configuration.GetConnectionString("DefaultConnection"));
    logger.LogInformation("📂 Attachments Path: {Path}", builder.Configuration["FileUploadSettings:BasePath"]);
}

app.Run();

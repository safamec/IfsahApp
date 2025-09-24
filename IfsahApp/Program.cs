using System.IO;
using System.Reflection;
using IfsahApp.Core.Mapping;
using IfsahApp.Hubs;
using IfsahApp.Infrastructure.Data;
using IfsahApp.Infrastructure.Services;
using IfsahApp.Infrastructure.Services.AdUser;
using IfsahApp.Infrastructure.Services.Email;
using IfsahApp.Utils;
using IfsahApp.Web.Middleware.Auth;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var options = new WebApplicationOptions
{
    ContentRootPath = Directory.GetCurrentDirectory(),
    WebRootPath     = Path.Combine(Directory.GetCurrentDirectory(), "Web", "wwwroot")
};

var builder = WebApplication.CreateBuilder(options);

// Load user-secrets in Development (Smtp:UserName / Smtp:Password)
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);
}

// 1) Database
builder.Services.AddDbContext<ApplicationDbContext>(dbOptions =>
    dbOptions.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2) AutoMapper
builder.Services.AddAutoMapper(typeof(DisclosureMappingProfile));

// 3) Email settings + service
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddTransient<IEmailService, GmailEmailService>();

// 4) Localization + MVC + Antiforgery (single, consolidated chain) + TempData in Session
builder.Services.AddLocalization(opts => opts.ResourcesPath = "Resources");
builder.Services
    .AddControllersWithViews(o =>
    {
        // auto-validate antiforgery on POST/PUT/DELETE etc.
        o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    })
    .AddViewLocalization()
    .AddDataAnnotationsLocalization()
    .AddRazorOptions(opts =>
    {
        // custom view locations
        opts.ViewLocationFormats.Clear();
        opts.ViewLocationFormats.Add("/Web/Views/{1}/{0}.cshtml");
        opts.ViewLocationFormats.Add("/Web/Views/Shared/{0}.cshtml");
    })
    .AddSessionStateTempDataProvider();

builder.Services.AddSession();

// 5) Custom Services (AD user: fake in Dev, LDAP in Prod)
if (builder.Environment.IsDevelopment())
    builder.Services.AddSingleton<IAdUserService, FakeAdUserService>();
else
    builder.Services.AddSingleton<IAdUserService, LdapAdUserService>();

builder.Services.AddScoped<IEnumLocalizer, EnumLocalizer>();
builder.Services.AddScoped<ViewRenderService>();

// 6) Authentication & Authorization
builder.Services.AddAppAuthentication(builder.Environment); // must set Cookie LoginPath/AccessDeniedPath

builder.Services.AddAuthorization(options =>
{
    // require auth by default unless [AllowAnonymous]
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// 7) Antiforgery header name (matches your JS)
builder.Services.AddAntiforgery(o => o.HeaderName = "RequestVerificationToken");

// 8) SignalR
builder.Services.AddSignalR();

// ---------- Build ----------
var app = builder.Build();

// (Optional) seed (idempotent)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    DbSeeder.Seed(db);
}

// 10) Localization middleware (early)
var supportedCultures = new[] { "en", "ar" };
var locOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("ar")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);
app.UseRequestLocalization(locOptions);

// 11) Pipeline
app.UseStaticFiles();
app.UseSession();
app.UseRouting();

app.UseAdUser();        // BEFORE auth; your middleware populates HttpContext.Items
app.UseAuthentication(); // BEFORE UseAuthorization
app.UseAuthorization();

// 12) Routes
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

// SignalR hub
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();

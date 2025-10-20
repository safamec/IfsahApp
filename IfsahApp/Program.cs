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
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var options = new WebApplicationOptions
{
    ContentRootPath = Directory.GetCurrentDirectory(),
    WebRootPath     = Path.Combine(Directory.GetCurrentDirectory(), "Web", "wwwroot")
};

var builder = WebApplication.CreateBuilder(options);

// ---------- Configuration Loading ----------
// ASP.NET Core automatically loads:
// - appsettings.json
// - appsettings.{Environment}.json (e.g., Development / Staging / Production)
//
// Example:
//   Development → appsettings.Development.json
//   Staging     → appsettings.Staging.json
//   Production  → appsettings.Production.json
//
// So you don't need to manually handle them here.

// Load user-secrets in Development (Smtp:UserName / Smtp:Password)
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);
}

// ---------- 1) Database ----------
builder.Services.AddDbContext<ApplicationDbContext>(dbOptions =>
    dbOptions.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---------- 2) AutoMapper ----------
builder.Services.AddAutoMapper(typeof(DisclosureMappingProfile));

// ---------- 3) Email settings + service ----------
// Read Smtp settings from the environment-specific appsettings file
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));

// Use Gmail in Development; use company SMTP in Staging/Production
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddTransient<IEmailService, GmailEmailService>();
}
else
{
    builder.Services.AddTransient<IEmailService, SmtpEmailService>(); // Generic SMTP
}

// ---------- 4) Localization + MVC + Antiforgery + TempData in Session ----------
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

// ---------- 5) Custom Services (AD user: fake in Dev, LDAP in Staging/Prod) ----------
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
builder.Services.AddAppAuthentication(builder.Environment); // must set Cookie LoginPath/AccessDeniedPath

builder.Services.AddAuthorization(options =>
{
    // require auth by default unless [AllowAnonymous]
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// ---------- 7) Antiforgery header name ----------
builder.Services.AddAntiforgery(o => o.HeaderName = "RequestVerificationToken");

// ---------- 8) SignalR ----------
builder.Services.AddSignalR();

// ---------- Build Application ----------
var app = builder.Build();

// ---------- 9) Optional: Seed database (idempotent) ----------
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    DbSeeder.Seed(db);
}

// ---------- 10) Localization middleware (early in pipeline) ----------
var supportedCultures = new[] { "en", "ar" };
var locOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("ar")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);
app.UseRequestLocalization(locOptions);

// ---------- 11) Middleware Pipeline ----------
app.UseStaticFiles();
app.UseSession();
app.UseRouting();

app.UseAdUser();        // BEFORE auth; your middleware populates HttpContext.Items
app.UseAuthentication(); // BEFORE UseAuthorization
app.UseAuthorization();

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

// ---------- 14) Debug: Log Current Environment ----------
var envName = app.Environment.EnvironmentName;
var smtp = builder.Configuration.GetSection("Smtp").Get<SmtpSettings>();
var attachSettings = builder.Configuration.GetSection("AttachmentSettings").Get<AttachmentSettings>();
var dbPath = builder.Configuration.GetConnectionString("DefaultConnection");

Console.WriteLine($"🚀 Environment: {envName}");
Console.WriteLine($"📧 SMTP Host: {smtp?.Host}");
Console.WriteLine($"📦 DB Path: {dbPath}");
Console.WriteLine($"📂 Attachment Path: {attachSettings?.BasePath}");

app.Run();

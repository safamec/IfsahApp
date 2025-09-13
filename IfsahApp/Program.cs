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
using Microsoft.EntityFrameworkCore;

var options = new WebApplicationOptions
{
    ContentRootPath = Directory.GetCurrentDirectory(),
    WebRootPath = Path.Combine(Directory.GetCurrentDirectory(), "Web", "wwwroot")
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

// 4) Localization + Views
builder.Services.AddLocalization(opts => opts.ResourcesPath = "Resources");
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization()
    .AddRazorOptions(opts =>
    {
        opts.ViewLocationFormats.Clear();
        opts.ViewLocationFormats.Add("/Web/Views/{1}/{0}.cshtml");
        opts.ViewLocationFormats.Add("/Web/Views/Shared/{0}.cshtml");
    });

// 5) Custom Services (AD user)
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IAdUserService, FakeAdUserService>();
}
else
{
    builder.Services.AddSingleton<IAdUserService, LdapAdUserService>();
}

builder.Services.AddScoped<IEnumLocalizer, EnumLocalizer>();

// 6) Authentication & Authorization
builder.Services.AddAppAuthentication(builder.Environment);

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddSignalR();

// 7) Build app
var app = builder.Build();

// 8) DB Seeding
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    DbSeeder.Seed(db);
}

// 9) Localization middleware
var supportedCultures = new[] { "en", "ar" };
var locOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("ar")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(locOptions);

// 10) Middleware pipeline
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAdUser();

// 11) Routing
if (app.Environment.IsDevelopment())
{
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=DevLogin}/{action=Index}/{id?}"
    );
}
else
{
    app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Account}/{action=Login}/{id?}"
    );
}

app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();

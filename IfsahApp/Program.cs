using IfsahApp.Data;
using IfsahApp.Services;
using IfsahApp.Middleware;
using IfsahApp.Extensions;
using IfsahApp.Services.Email;
using IfsahApp.Services.AdUser;
using Microsoft.EntityFrameworkCore;

var options = new WebApplicationOptions
{
    ContentRootPath = Directory.GetCurrentDirectory(),
    WebRootPath = Path.Combine(Directory.GetCurrentDirectory(), "Web", "wwwroot")
};

var builder = WebApplication.CreateBuilder(options);

// =============================
// 1. Database
// =============================
builder.Services.AddDbContext<ApplicationDbContext>(dbOptions =>
    dbOptions.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// =============================
// 2. Localization + Views
// =============================
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

// =============================
// 3. Custom Services
// =============================
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<IAdUserService, FakeAdUserService>();
    builder.Services.AddTransient<IEmailService, FakeEmailService>();
}
else
{
    builder.Services.AddSingleton<IAdUserService, LdapAdUserService>();
    builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));
    builder.Services.AddTransient<IEmailService, SmtpEmailService>();
}

builder.Services.AddScoped<IEnumLocalizer, EnumLocalizer>();

// =============================
// 4. Authentication & AD Service
// =============================
builder.Services.AddAppAuthentication(builder.Environment, args);

// =============================
// 5. Authorization
// =============================
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// =============================
// 6. Build app
// =============================
var app = builder.Build();

// =============================
// 7. DB Seeding
// =============================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    DbSeeder.Seed(db);
}

// =============================
// 8. Localization options
// =============================
var supportedCultures = new[] { "en", "ar" };
var locOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("ar")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(locOptions);

// =============================
// 9. Middleware pipeline
// =============================
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAdUser();

// =============================
// 10. Routing
// =============================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// =============================
// 11. Run
// =============================
app.Run();

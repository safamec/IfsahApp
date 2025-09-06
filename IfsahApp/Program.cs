using IfsahApp.Data;
using IfsahApp.Services;
using IfsahApp.Services.AdUser;
using IfsahApp.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.EntityFrameworkCore;
using IfsahApp.Services.Email;
using IfsahApp.Options;
using IfsahApp.Middleware;
using IfsahApp.Extensions;

var builder = WebApplication.CreateBuilder(args);

// =============================
// 1. Database
// =============================
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// =============================
// 2. Localization
// =============================
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddControllersWithViews()
       .AddViewLocalization()
       .AddDataAnnotationsLocalization();

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
// 7. DB seeding
// =============================
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    DbSeeder.Seed(dbContext);
}

// =============================
// 8. Localization options
// =============================
var supportedCultures = new[] { "en", "ar" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("ar")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);

// =============================
// 9. Middleware pipeline
// =============================
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Middleware: ensure AD profile exists
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

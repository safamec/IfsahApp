using IfsahApp.Data;        // Import your EF Core database context (ApplicationDbContext)
using IfsahApp.Services;    // Import your custom services (AD service, Email service, etc.)
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.EntityFrameworkCore; // Import EF Core functionality for DbContext and SQLite

var builder = WebApplication.CreateBuilder(args);
// Creates a new WebApplicationBuilder to configure services and the HTTP pipeline

// =============================
// 1. Register Database Context
// =============================
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
// Registers ApplicationDbContext with dependency injection (DI) using SQLite
// Connection string is fetched from appsettings.json under "DefaultConnection"

// =============================
// 2. Localization Services
// =============================
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
// Enables localization (multi-language support) and tells ASP.NET to look for resource files in /Resources

builder.Services.AddControllersWithViews()
    .AddViewLocalization()              // Enables localized views (e.g., Index.en.cshtml, Index.ar.cshtml)
    .AddDataAnnotationsLocalization();  // Enables localized validation messages (from resource files)

// =============================
// 3. Custom Services
// =============================
builder.Services.AddSingleton<IAdUserService, AdUserService>();
// Registers your fake Active Directory service as a Singleton
// (in-memory, so one instance is enough for the whole app lifetime)

builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("SmtpSettings"));
// Reads SMTP settings (host, port, username, password, etc.) from appsettings.json into SmtpSettings class

builder.Services.AddTransient<IEmailService, SmtpEmailService>();
// Registers Email service (creates a new instance each time it's requested)

builder.Services.AddScoped<IEnumLocalizer, EnumLocalizer>();
// Register Enum localizer

builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme).AddNegotiate();
// Enable Windows Authentication

// =============================
// 4. Build Application
// =============================
var app = builder.Build();
// Builds the WebApplication with all the registered services and configuration

// =============================
// 5. Database Seeding
// =============================
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    DbSeeder.Seed(dbContext);
    // Seeds the database with initial data (users, disclosure types, sample disclosures, etc.)
}

// =============================
// 6. Localization Configuration
// =============================
var supportedCultures = new[] { "en", "ar" };
// Define which cultures your app supports (English + Arabic)

var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("ar")            // Default culture is Arabic
    .AddSupportedCultures(supportedCultures)   // Supports both English and Arabic
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);
// Applies the localization settings to the request pipeline

// =============================
// 7. Middleware Pipeline
// =============================
app.UseStaticFiles();  // Serves static files (CSS, JS, images, etc.) from wwwroot folder
app.UseRouting();      // Enables endpoint routing
app.UseAuthentication(); // Enables authentication
app.UseAuthorization();// Enables authorization checks (e.g., [Authorize] attributes)

// =============================
// 8. Routing
// =============================
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");
// Sets up default route pattern: {controller=Home}/{action=Index}/{id?}

// =============================
// 9. Run Application
// =============================
app.Run();
// Starts the web server and listens for incoming HTTP requests

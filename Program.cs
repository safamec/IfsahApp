using IfsahApp.Data;
using IfsahApp.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Register ApplicationDbContext with SQLite provider
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add localization services (optional, you can remove if not needed)
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Add MVC with localization support
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

// Register the fake AD service (Singleton is fine since it's in-memory)
builder.Services.AddSingleton<IAdUserService, AdUserService>();

var app = builder.Build();

// Seed the database (creates users, disclosure types, and initial disclosures)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    DbSeeder.Seed(dbContext);
}

// Localization options (optional)
var supportedCultures = new[] { "en-US", "ar" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("ar")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);

// Middleware
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// Map default controller route
app.MapDefaultControllerRoute();

app.Run();

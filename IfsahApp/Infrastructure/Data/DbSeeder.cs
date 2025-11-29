using IfsahApp.Core.Enums;
using IfsahApp.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IfsahApp.Infrastructure.Data;

public static class DbSeeder
{
    public static void Seed(ApplicationDbContext context, ILogger logger)
    {
        try
        {
            logger.LogInformation("Starting database migration...");

            // Try applying migrations
            context.Database.Migrate();
            logger.LogInformation("Database migration complete.");

            // --- Seed Admin user only ---
            if (!context.Users.Any(u => u.ADUserName == "Ahmed.sm"))
            {
                logger.LogInformation("Seeding default admin user...");

                context.Users.Add(new User
                {
                    ADUserName = "Ahmed.sm",
                    FullName = "Ahmed Al Wahaibi",
                    Email = "ahmed.s.alwahaibi@mem.gov.om",
                    Department = "Management",
                    Role = Role.Admin,
                    IsActive = true,
                    IsEmailConfirmed = true
                });

                context.SaveChanges();
                logger.LogInformation("Admin user seeded successfully.");
            }
            else
            {
                logger.LogInformation("Admin user already exists. Skipping seed.");
            }
        }
        catch (Exception ex)
        {
            // This prevents IIS from crashing if something goes wrong
            logger.LogError(ex, "❌ Database migration or seeding failed.");
        }
    }
}

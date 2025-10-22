using IfsahApp.Core.Enums;
using IfsahApp.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IfsahApp.Infrastructure.Data;

public static class DbSeeder
{
    public static void Seed(ApplicationDbContext context)
    {
        // Apply any pending migrations automatically
        context.Database.Migrate();

        // --- Seed Admin user only ---
        if (!context.Users.Any(u => u.ADUserName == "Ahmed.sm"))
        {
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
        }
    }
}

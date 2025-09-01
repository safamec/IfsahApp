

using IfsahApp.Models;
using Microsoft.EntityFrameworkCore;

namespace IfsahApp.Data;

public static class DbSeeder
{
    public static void Seed(ApplicationDbContext context)
    {
        // Apply any pending migrations
        context.Database.Migrate();

        // Seed Users
        if (!context.Users.Any())
        {
            context.Users.AddRange(
                new User
                {
                    Id = 1,
                    ADUserName = "ahmed.wahaibi",
                    FullName = "Ahmed Al Wahaibi",
                    Email = "ahmed@example.com",
                    Department = "Development",
                    Role = "Employee",
                    IsActive = true
                },
                new User
                {
                    Id = 2,
                    ADUserName = "fatima.harthy",
                    FullName = "Fatima Al Harthy",
                    Email = "fatima@example.com",
                    Department = "Audit",
                    Role = "AuditManager",
                    IsActive = true
                }
            );
            context.SaveChanges();
        }

        // Seed Disclosure Types
        if (!context.DisclosureTypes.Any())
        {
            context.DisclosureTypes.AddRange(
                new DisclosureType { Id = 1, Name = "Safety" },
                new DisclosureType { Id = 2, Name = "Compliance" }
            );
            context.SaveChanges();
        }

        // Seed Disclosures
        if (!context.Disclosures.Any())
        {
            context.Disclosures.AddRange(
                new Disclosure
                {
                    Id = 1,
                    DisclosureNumber = "DISC-2025-0001",
                    Description = "Unauthorized access to system",
                    IncidentStartDate = new DateTime(2025, 8, 30),
                    IncidentEndDate = new DateTime(2025, 8, 31),
                    Location = "Main Office",
                    SubmittedAt = DateTime.UtcNow,
                    Status = "New",
                    DisclosureTypeId = 1, // Safety
                    SubmittedById = 1,   // Ahmed
                    IsAccuracyConfirmed = false
                },
                new Disclosure
                {
                    Id = 2,
                    DisclosureNumber = "DISC-2025-0002",
                    Description = "Data breach reported",
                    IncidentStartDate = new DateTime(2025, 9, 1),
                    Location = "Headquarters",
                    SubmittedAt = DateTime.UtcNow,
                    Status = "InReview",
                    DisclosureTypeId = 2, // Compliance
                    SubmittedById = 2,   // Fatima
                    IsAccuracyConfirmed = true
                }
            );
            context.SaveChanges();
        }
    }
}

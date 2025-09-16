using IfsahApp.Core.Enums;
using IfsahApp.Utils;
using IfsahApp.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IfsahApp.Infrastructure.Data;

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
            ADUserName = "Admin",                // SamAccountName أو أي اسم AD عندك
            FullName = "Main Administrator",
            Email = "mgk390@gmail.com",
            Department = "Management",
            Role = Role.Admin,
            IsActive = true,
            IsEmailConfirmed = false
                },
                new User
                {
                    Id = 2,
                    ADUserName = "fatima.harthy",
                    FullName = "Fatima Al Harthy",
                    Email = "safaa3568@gmail.com",
                    Department = "Audit",
                    Role = Role.Examiner,
                    IsActive = true
                },
                new User
                {
                    Id = 3,
                    ADUserName = "mohammed.said",
                    FullName = "Mohammed Al Said",
                    Email = "safaa3568@gmail.com",
                    Department = "Finance",
                    Role = Role.User,
                    IsActive = true
                }
            );
            context.SaveChanges();
        }

      // Seed Disclosure Types
if (!context.DisclosureTypes.Any())
{
    context.DisclosureTypes.AddRange(
        new DisclosureType { EnglishName = "Safety", ArabicName = "السلامة" },
        new DisclosureType { EnglishName = "Compliance", ArabicName = "الامتثال" }
    );
    context.SaveChanges();
}
else
{
    var safety = context.DisclosureTypes.FirstOrDefault(x => x.EnglishName == "Safety");
    if (safety != null && string.IsNullOrWhiteSpace(safety.ArabicName))
        safety.ArabicName = "السلامة";

    var compliance = context.DisclosureTypes.FirstOrDefault(x => x.EnglishName == "Compliance");
    if (compliance != null && string.IsNullOrWhiteSpace(compliance.ArabicName))
        compliance.ArabicName = "الامتثال";

    context.SaveChanges();
}

        // Seed Disclosures
        if (!context.Disclosures.Any())
        {
            context.Disclosures.AddRange(
                new Disclosure
                {
                    Id = 1,
                    DisclosureNumber = DisclosureNumberGeneratorHelper.Generate(),
                    Description = "Unauthorized access to system",
                    IncidentStartDate = new DateTime(2025, 8, 30),
                    IncidentEndDate = new DateTime(2025, 8, 31),
                    Location = "Main Office",
                    SubmittedAt = DateTime.UtcNow,
                    Status = DisclosureStatus.New,
                    DisclosureTypeId = 1,
                    SubmittedById = 1,
                    IsAccuracyConfirmed = false
                },
                new Disclosure
                {
                    Id = 2,
                    DisclosureNumber = DisclosureNumberGeneratorHelper.Generate(),
                    Description = "Data breach reported",
                    IncidentStartDate = new DateTime(2025, 9, 1),
                    Location = "Headquarters",
                    SubmittedAt = DateTime.UtcNow,
                    Status = DisclosureStatus.InReview,
                    DisclosureTypeId = 2,
                    SubmittedById = 2,
                    IsAccuracyConfirmed = true
                },
                new Disclosure
                {
                    Id = 3,
                    DisclosureNumber = DisclosureNumberGeneratorHelper.Generate(),
                    Description = "Unreported financial transaction",
                    IncidentStartDate = new DateTime(2025, 7, 15),
                    Location = "Finance Dept.",
                    SubmittedAt = DateTime.UtcNow,
                    Status = DisclosureStatus.Assigned,
                    DisclosureTypeId = 2,
                    SubmittedById = 1,
                    IsAccuracyConfirmed = false
                },
                new Disclosure
                {
                    Id = 4,
                    DisclosureNumber = DisclosureNumberGeneratorHelper.Generate(),
                    Description = "Unauthorized software installation",
                    IncidentStartDate = new DateTime(2025, 8, 5),
                    Location = "IT Dept.",
                    SubmittedAt = DateTime.UtcNow,
                    Status = DisclosureStatus.Completed,
                    DisclosureTypeId = 1,
                    SubmittedById = 2,
                    IsAccuracyConfirmed = true
                },
                new Disclosure
                {
                    Id = 5,
                    DisclosureNumber = DisclosureNumberGeneratorHelper.Generate(),
                    Description = "Misuse of company resources",
                    IncidentStartDate = new DateTime(2025, 6, 20),
                    Location = "Branch Office",
                    SubmittedAt = DateTime.UtcNow,
                    Status = DisclosureStatus.New,
                    DisclosureTypeId = 2,
                    SubmittedById = 1,
                    IsAccuracyConfirmed = false
                },
                new Disclosure
                {
                    Id = 6,
                    DisclosureNumber = DisclosureNumberGeneratorHelper.Generate(),
                    Description = "Physical security breach",
                    IncidentStartDate = new DateTime(2025, 8, 1),
                    Location = "Warehouse",
                    SubmittedAt = DateTime.UtcNow,
                    Status = DisclosureStatus.InReview,
                    DisclosureTypeId = 1,
                    SubmittedById = 2,
                    IsAccuracyConfirmed = true
                },
                new Disclosure
                {
                    Id = 7,
                    DisclosureNumber = DisclosureNumberGeneratorHelper.Generate(),
                    Description = "Delayed compliance reporting",
                    IncidentStartDate = new DateTime(2025, 7, 30),
                    Location = "Audit Dept.",
                    SubmittedAt = DateTime.UtcNow,
                    Status = DisclosureStatus.Assigned,
                    DisclosureTypeId = 2,
                    SubmittedById = 1,
                    IsAccuracyConfirmed = false
                },
                new Disclosure
                {
                    Id = 8,
                    DisclosureNumber = DisclosureNumberGeneratorHelper.Generate(),
                    Description = "Unauthorized disclosure of confidential data",
                    IncidentStartDate = new DateTime(2025, 8, 12),
                    Location = "Main Office",
                    SubmittedAt = DateTime.UtcNow,
                    Status = DisclosureStatus.Completed,
                    DisclosureTypeId = 2,
                    SubmittedById = 2,
                    IsAccuracyConfirmed = true
                },
                new Disclosure
                {
                    Id = 9,
                    DisclosureNumber = DisclosureNumberGeneratorHelper.Generate(),
                    Description = "Safety hazard in office premises",
                    IncidentStartDate = new DateTime(2025, 8, 20),
                    Location = "HQ Building",
                    SubmittedAt = DateTime.UtcNow,
                    Status = DisclosureStatus.New,
                    DisclosureTypeId = 1,
                    SubmittedById = 1,
                    IsAccuracyConfirmed = false
                },
                new Disclosure
                {
                    Id = 10,
                    DisclosureNumber = DisclosureNumberGeneratorHelper.Generate(),
                    Description = "Non-compliance with internal audit",
                    IncidentStartDate = new DateTime(2025, 9, 1),
                    Location = "Audit Dept.",
                    SubmittedAt = DateTime.UtcNow,
                    Status = DisclosureStatus.InReview,
                    DisclosureTypeId = 2,
                    SubmittedById = 2,
                    IsAccuracyConfirmed = true
                }
            );

            context.SaveChanges();
        }
    }
}
using IfsahApp.Models;
using Microsoft.EntityFrameworkCore;

namespace IfsahApp.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    // System Users
    public DbSet<User> Users { get; set; }

    // Lookup
    public DbSet<DisclosureType> DisclosureTypes { get; set; }

    // Core Disclosure Workflow
    public DbSet<Disclosure> Disclosures { get; set; }
    public DbSet<DisclosurePerson> DisclosurePeople { get; set; }
    public DbSet<DisclosureAttachment> DisclosureAttachments { get; set; }
    public DbSet<DisclosureNote> DisclosureNotes { get; set; }
    public DbSet<DisclosureAssignment> DisclosureAssignments { get; set; }
    public DbSet<DisclosureReview> DisclosureReviews { get; set; }

    // Role Delegation
    public DbSet<RoleDelegation> RoleDelegations { get; set; }

    // Notification System
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<UserNotificationPreference> UserNotificationPreferences { get; set; }

    // Logs
    public DbSet<AuditLog> AuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Unique indexes
        modelBuilder.Entity<DisclosureType>()
            .HasIndex(t => t.Name)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.ADUserName)
            .IsUnique();

        // Notification
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.Recipient)
            .WithMany()
            .HasForeignKey(n => n.RecipientId)
            .OnDelete(DeleteBehavior.Restrict);

        // One-to-one: DisclosureReview
        modelBuilder.Entity<DisclosureReview>()
    .HasOne(dr => dr.Disclosure)
    .WithOne(d => d.FinalReview)
    .HasForeignKey<DisclosureReview>(dr => dr.DisclosureId)
    .OnDelete(DeleteBehavior.Cascade); // ✅ this is okay

        modelBuilder.Entity<DisclosureReview>()
            .HasOne(dr => dr.Reviewer)
            .WithMany()
            .HasForeignKey(dr => dr.ReviewerId)
            .OnDelete(DeleteBehavior.Restrict); // ✅ FIX: prevent cascade path error

        // RoleDelegation with restricted deletes
        modelBuilder.Entity<RoleDelegation>()
            .HasOne(d => d.FromUser)
            .WithMany()
            .HasForeignKey(d => d.FromUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<RoleDelegation>()
            .HasOne(d => d.ToUser)
            .WithMany()
            .HasForeignKey(d => d.ToUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure DisclosurePerson base + TPH
        modelBuilder.Entity<DisclosurePerson>().HasKey(p => p.Id);

        modelBuilder.Entity<DisclosurePerson>()
            .HasOne(p => p.Disclosure)
            .WithMany()
            .HasForeignKey(p => p.DisclosureId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DisclosurePerson>()
            .ToTable("DisclosurePersons")
            .HasDiscriminator<string>("PersonType")
            .HasValue<SuspectedPerson>("Suspected")
            .HasValue<RelatedPerson>("Related");

        modelBuilder.Entity<Disclosure>()
            .HasMany(d => d.SuspectedPeople)
            .WithOne(p => p.Disclosure)
            .HasForeignKey(p => p.DisclosureId);

        modelBuilder.Entity<Disclosure>()
            .HasMany(d => d.RelatedPeople)
            .WithOne(p => p.Disclosure)
            .HasForeignKey(p => p.DisclosureId);

        // ✅ DisclosureAssignments: prevent multiple cascade paths
        modelBuilder.Entity<DisclosureAssignment>()
            .HasOne(a => a.Disclosure)
            .WithMany(d => d.Assignments)
            .HasForeignKey(a => a.DisclosureId)
            .OnDelete(DeleteBehavior.Cascade); // keep cascade here

        modelBuilder.Entity<DisclosureAssignment>()
            .HasOne(a => a.Examiner)
            .WithMany()
            .HasForeignKey(a => a.ExaminerId)
            .OnDelete(DeleteBehavior.Restrict); // prevent cascade path

        // ✅ DisclosureNotes: prevent multiple cascade paths
        modelBuilder.Entity<DisclosureNote>()
            .HasOne(n => n.Disclosure)
            .WithMany(d => d.Notes)
            .HasForeignKey(n => n.DisclosureId)
            .OnDelete(DeleteBehavior.Cascade); // this is okay

        modelBuilder.Entity<DisclosureNote>()
            .HasOne(n => n.Author)
            .WithMany()
            .HasForeignKey(n => n.AuthorId)
            .OnDelete(DeleteBehavior.Restrict); // ✅ avoid multiple cascade paths
    }
}
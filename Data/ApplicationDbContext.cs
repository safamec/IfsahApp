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

    // Comments for admin
    public DbSet<Comment> Comments { get; set; }

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
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DisclosureReview>()
            .HasOne(dr => dr.Reviewer)
            .WithMany()
            .HasForeignKey(dr => dr.ReviewerId)
            .OnDelete(DeleteBehavior.Restrict);

        // RoleDelegation
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

        // DisclosureAssignments
        modelBuilder.Entity<DisclosureAssignment>()
            .HasOne(a => a.Disclosure)
            .WithMany(d => d.Assignments)
            .HasForeignKey(a => a.DisclosureId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DisclosureAssignment>()
            .HasOne(a => a.Examiner)
            .WithMany()
            .HasForeignKey(a => a.ExaminerId)
            .OnDelete(DeleteBehavior.Restrict);

        // DisclosureNotes
        modelBuilder.Entity<DisclosureNote>()
            .HasOne(n => n.Disclosure)
            .WithMany(d => d.Notes)
            .HasForeignKey(n => n.DisclosureId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DisclosureNote>()
            .HasOne(n => n.Author)
            .WithMany()
            .HasForeignKey(n => n.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Comments
        modelBuilder.Entity<Comment>()
            .HasOne(c => c.Disclosure)
            .WithMany(d => d.Comments)
            .HasForeignKey(c => c.DisclosureId)
            .OnDelete(DeleteBehavior.Cascade);

        // ✅ Explicitly configure multiple User relationships
        modelBuilder.Entity<Disclosure>()
            .HasOne(d => d.SubmittedBy)
            .WithMany(u => u.SubmittedDisclosures)
            .HasForeignKey(d => d.SubmittedById)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Disclosure>()
            .HasOne(d => d.AssignedToUser)
            .WithMany(u => u.AssignedDisclosures)
            .HasForeignKey(d => d.AssignedToUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

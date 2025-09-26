using IfsahApp.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace IfsahApp.Infrastructure.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        // System Users
        public DbSet<User> Users { get; set; } = null!;

        // Lookup
        public DbSet<DisclosureType> DisclosureTypes { get; set; } = null!;

        // Core Disclosure Workflow
        public DbSet<Disclosure> Disclosures { get; set; } = null!;
        public DbSet<DisclosurePerson> DisclosurePeople { get; set; } = null!;
        public DbSet<DisclosureAttachment> DisclosureAttachments { get; set; } = null!;
        public DbSet<DisclosureNote> DisclosureNotes { get; set; } = null!;
        public DbSet<DisclosureAssignment> DisclosureAssignments { get; set; } = null!;
        public DbSet<DisclosureReview> DisclosureReviews { get; set; } = null!;

        // Comments for admin
        public DbSet<Comment> Comments { get; set; } = null!;

        // Role Delegation
        public DbSet<RoleDelegation> RoleDelegations { get; set; } = null!;

        // Notification System
        public DbSet<Notification> Notifications { get; set; } = null!;
        public DbSet<UserNotificationPreference> UserNotificationPreferences { get; set; } = null!;
        public DbSet<EmailVerification> EmailVerifications { get; set; } = null!;

        // Logs
        public DbSet<AuditLog> AuditLogs { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ---------- Unique indexes ----------
            modelBuilder.Entity<DisclosureType>()
                .HasIndex(t => t.EnglishName)
                .IsUnique();

            modelBuilder.Entity<DisclosureType>()
                .HasIndex(t => t.ArabicName)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.ADUserName)
                .IsUnique();

            // ---------- Notifications ----------
            modelBuilder.Entity<Notification>()
                .HasOne(n => n.Recipient)
                .WithMany()
                .HasForeignKey(n => n.RecipientId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---------- Final Review (1:1) ----------
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

            // ---------- Role Delegation ----------
            modelBuilder.Entity<RoleDelegation>(e =>
            {
                e.HasOne(d => d.FromUser)
                 .WithMany()
                 .HasForeignKey(d => d.FromUserId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(d => d.ToUser)
                 .WithMany()
                 .HasForeignKey(d => d.ToUserId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.Property(d => d.Role).HasMaxLength(32);
            });

            // ---------- People (TPH) ----------
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

            // ---------- Attachments (explicit mapping) ----------
            modelBuilder.Entity<DisclosureAttachment>(a =>
            {
                a.HasKey(x => x.Id);
                a.Property(x => x.FileName).IsRequired();

                a.HasOne(x => x.Disclosure)
                 .WithMany(d => d.Attachments)
                 .HasForeignKey(x => x.DisclosureId)
                 .OnDelete(DeleteBehavior.Cascade);

                a.HasIndex(x => x.DisclosureId);
                a.HasIndex(x => new { x.DisclosureId, x.UploadedAt });
            });

            // ---------- Assignments ----------
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

            // ---------- Notes ----------
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

            // ---------- Comments ----------
            modelBuilder.Entity<Comment>()
                .HasOne(c => c.Disclosure)
                .WithMany(d => d.Comments)
                .HasForeignKey(c => c.DisclosureId)
                .OnDelete(DeleteBehavior.Cascade);

            // ---------- User relationships on Disclosure ----------
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
}

using CTSHIPDashboard.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CTSHIPDashboard.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<MedicalHistory> MedicalHistories { get; set; }
        public DbSet<Enrollee> Enrollees { get; set; }
        public DbSet<Encounter> Encounters { get; set; }
        public DbSet<Organization> Organizations { get; set; }
        public DbSet<Claim> Claims { get; set; }
        public DbSet<Provider> Providers { get; set; }
        public DbSet<Feedback> Feedbacks { get; set; }
        public DbSet<NewsUpdate> NewsUpdates { get; set; }
        public DbSet<ApplicationUser> ApplicationUsers { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<UserActivity> UserActivities { get; set; }
        public DbSet<Hmo> Hmos { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // GLOBAL FIX — ALL decimal → decimal(18,2)
            foreach (var property in modelBuilder.Model.GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(decimal)))
            {
                property.SetColumnType("decimal(18,2)");
            }

       
            base.OnModelCreating(modelBuilder);

            // FIX CASCADE DELETE ISSUE — THIS IS THE SOLUTION
            foreach (var relationship in modelBuilder.Model.GetEntityTypes()
                .SelectMany(e => e.GetForeignKeys()))
            {
                relationship.DeleteBehavior = DeleteBehavior.Restrict;
            }

            // OR — ONLY FIX SPECIFIC ONES (RECOMMENDED FOR PRODUCTION)
            modelBuilder.Entity<Claim>()
                .HasOne(c => c.Provider)
                .WithMany(p => p.Claims)
                .HasForeignKey(c => c.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);  // NOT CASCADE!

            modelBuilder.Entity<Claim>()
                .HasOne(c => c.Enrollee)
                .WithMany(e => e.Claims)
                .HasForeignKey(c => c.EnrolleeId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Encounter>()
                .HasOne(e => e.Provider)
                .WithMany(p => p.Encounters)
                .HasForeignKey(e => e.ProviderId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Encounter>()
                .HasOne(e => e.Enrollee)
                .WithMany(e => e.Encounters)
                .HasForeignKey(e => e.EnrolleeId)
                .OnDelete(DeleteBehavior.Restrict);
      
            modelBuilder.Entity<Enrollee>()
                .HasOne(e => e.Hmo)
                .WithMany(h => h.Enrollees)
                .HasForeignKey(e => e.HmoId)
                .OnDelete(DeleteBehavior.SetNull)    // Safe because HmoId is nullable
                .IsRequired(false);

            base.OnModelCreating(modelBuilder);
        

        // Optional: Keep cascade only for non-problematic ones
        // modelBuilder.Entity<Enrollee>().HasOne(e => e.Hmo).WithMany().OnDelete(DeleteBehavior.SetNull);
        }
    }
}
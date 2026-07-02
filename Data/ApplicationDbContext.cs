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
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<Complaint> Complaints { get; set; }
        public DbSet<Feedback> Feedbacks { get; set; }
        public DbSet<NewsUpdate> NewsUpdates { get; set; }
        public DbSet<ApplicationUser> ApplicationUsers { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<UserActivity> UserActivities { get; set; }
        public DbSet<Hmo> Hmos { get; set; }
        public DbSet<Referral> Referrals { get; set; }

        public DbSet<ReferredHospital> ReferralHospitals { get; set; }

        public DbSet<ReferralAuditLog> ReferralAuditLogs { get; set; }
        public DbSet<DeathRegister> DeathRegisters { get; set; }

        public DbSet<DeathRegisterAuditLog> DeathRegisterAuditLogs { get; set; }
        public DbSet<EnrolleeWallet> EnrolleeWallets { get; set; }
        public DbSet<WalletTransaction> WalletTransactions { get; set; }
        public DbSet<StateOfficeMonthlyReport> StateOfficeMonthlyReports { get; set; }
        public DbSet<ProgramMonitoringTarget> ProgramMonitoringTargets { get; set; }
        public DbSet<EncounterService> EncounterServices { get; set; }

        private static void ConfigureDeathRegisterEntities(ModelBuilder builder)
        {
            builder.Entity<DeathRegister>(entity =>
            {
                entity.ToTable("DeathRegisters");
                entity.HasKey(x => x.Id);

                entity.Property(x => x.EnrolleeNumber).IsRequired().HasMaxLength(100);
                entity.Property(x => x.EnrolleeFullName).IsRequired().HasMaxLength(200);
                entity.Property(x => x.Gender).HasMaxLength(50);
                entity.Property(x => x.PhoneNumber).HasMaxLength(50);
                entity.Property(x => x.Address).HasMaxLength(500);
                entity.Property(x => x.HmoCode).HasMaxLength(100);
                entity.Property(x => x.HmoName).HasMaxLength(200);
                entity.Property(x => x.ProviderId).HasMaxLength(100);
                entity.Property(x => x.ProviderName).IsRequired().HasMaxLength(200);
                entity.Property(x => x.PlaceOfDeath).IsRequired().HasMaxLength(300);
                entity.Property(x => x.CauseOfDeath).IsRequired().HasMaxLength(1000);
                entity.Property(x => x.DeathConfirmedBy).IsRequired().HasMaxLength(200);
                entity.Property(x => x.DeathConfirmedByDesignation).HasMaxLength(100);
                entity.Property(x => x.DeathConfirmedByPhone).HasMaxLength(50);
                entity.Property(x => x.DeathCertificateNumber).HasMaxLength(100);
                entity.Property(x => x.DeathCertificateFilePath).HasMaxLength(500);
                entity.Property(x => x.ProviderRemarks).HasMaxLength(1000);
                entity.Property(x => x.CreatedByUserId).HasMaxLength(450);
                entity.Property(x => x.CreatedByName).HasMaxLength(200);
                entity.Property(x => x.SubmittedByUserId).HasMaxLength(450);
                entity.Property(x => x.SubmittedByName).HasMaxLength(200);
                entity.Property(x => x.VerifiedByUserId).HasMaxLength(450);
                entity.Property(x => x.VerifiedByName).HasMaxLength(200);
                entity.Property(x => x.HmoVerificationNote).HasMaxLength(1000);
                entity.Property(x => x.AuditedByUserId).HasMaxLength(450);
                entity.Property(x => x.AuditedByName).HasMaxLength(200);
                entity.Property(x => x.AuditNote).HasMaxLength(1000);

                entity.HasIndex(x => x.EnrolleeId);
                entity.HasIndex(x => x.EnrolleeNumber);
                entity.HasIndex(x => x.ProviderId);
                entity.HasIndex(x => x.HmoCode);
                entity.HasIndex(x => x.Status);
                entity.HasIndex(x => x.DateOfDeath);
                entity.HasIndex(x => new { x.EnrolleeNumber, x.Status });

                entity.HasMany(x => x.AuditLogs)
                    .WithOne(x => x.DeathRegister)
                    .HasForeignKey(x => x.DeathRegisterId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Wallet entities
            builder.Entity<EnrolleeWallet>(entity =>
            {
                entity.ToTable("EnrolleeWallets");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Balance).HasColumnType("decimal(18,2)");
                entity.Property(x => x.MonthlyAllocation).HasColumnType("decimal(18,2)");
                entity.HasIndex(x => x.EnrolleeId);
                entity.HasMany(x => x.Transactions).WithOne(t => t.EnrolleeWallet).HasForeignKey(t => t.EnrolleeWalletId).OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<WalletTransaction>(entity =>
            {
                entity.ToTable("WalletTransactions");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Amount).HasColumnType("decimal(18,2)");
                entity.Property(x => x.Type).HasMaxLength(100);
                entity.HasIndex(x => x.EnrolleeWalletId);
            });

            builder.Entity<DeathRegisterAuditLog>(entity =>
            {
                entity.ToTable("DeathRegisterAuditLogs");
                entity.HasKey(x => x.Id);

                entity.Property(x => x.ActionByUserId).HasMaxLength(450);
                entity.Property(x => x.ActionByName).HasMaxLength(200);
                entity.Property(x => x.Note).HasMaxLength(1000);

                entity.HasIndex(x => x.DeathRegisterId);
                entity.HasIndex(x => x.ActionAt);
            });
        }


        public void ConfigureReferralEntities(ModelBuilder builder)
        {
            builder.Entity<ReferredHospital>(entity =>
            {
                entity.ToTable("ReferralHospitals");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Name).IsRequired().HasMaxLength(200);
                entity.Property(x => x.State).HasMaxLength(100);
                entity.Property(x => x.Lga).HasMaxLength(100);
                entity.Property(x => x.Address).HasMaxLength(250);
                entity.Property(x => x.ContactPerson).HasMaxLength(100);
                entity.Property(x => x.PhoneNumber).HasMaxLength(50);
                entity.Property(x => x.Email).HasMaxLength(150);
                entity.HasIndex(x => x.Name);
                entity.HasIndex(x => x.State);
            });

            builder.Entity<Referral>(entity =>
            {
                entity.ToTable("Referrals");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.EncounterReference).HasMaxLength(100);
                entity.Property(x => x.EnrolleeNumber).IsRequired().HasMaxLength(100);
                entity.Property(x => x.EnrolleeFullName).IsRequired().HasMaxLength(200);
                entity.Property(x => x.HmoCode).HasMaxLength(100);
                entity.Property(x => x.HmoName).HasMaxLength(200);
                entity.Property(x => x.FromProviderId).HasMaxLength(100);
                entity.Property(x => x.FromProviderName).IsRequired().HasMaxLength(200);
                entity.Property(x => x.Diagnosis).IsRequired().HasMaxLength(200);
                entity.Property(x => x.ReasonForReferral).IsRequired().HasMaxLength(1000);
                entity.Property(x => x.ClinicalSummary).HasMaxLength(1000);
                entity.Property(x => x.TreatmentGiven).HasMaxLength(1000);
                entity.Property(x => x.InvestigationSummary).HasMaxLength(1000);
                entity.Property(x => x.CreatedByUserId).HasMaxLength(450);
                entity.Property(x => x.CreatedByName).HasMaxLength(200);
                entity.Property(x => x.SubmittedByUserId).HasMaxLength(450);
                entity.Property(x => x.VerifiedByUserId).HasMaxLength(450);
                entity.Property(x => x.VerifiedByName).HasMaxLength(200);
                entity.Property(x => x.HmoVerificationNote).HasMaxLength(1000);
                entity.Property(x => x.AuditedByUserId).HasMaxLength(450);
                entity.Property(x => x.AuditedByName).HasMaxLength(200);
                entity.Property(x => x.AuditNote).HasMaxLength(1000);
                entity.HasOne(x => x.ReferredHospital)
                    .WithMany(x => x.Referrals)
                    .HasForeignKey(x => x.ReferredHospitalId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasIndex(x => x.EnrolleeNumber);
                entity.HasIndex(x => x.HmoCode);
                entity.HasIndex(x => x.FromProviderId);
                entity.HasIndex(x => x.Status);
                entity.HasIndex(x => x.CreatedAt);
            });

            builder.Entity<ReferralAuditLog>(entity =>
            {
                entity.ToTable("ReferralAuditLogs");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.PerformedByUserId).HasMaxLength(450);
                entity.Property(x => x.PerformedByName).HasMaxLength(200);
                entity.Property(x => x.Note).HasMaxLength(1000);
                entity.HasOne(x => x.Referral)
                    .WithMany(x => x.AuditLogs)
                    .HasForeignKey(x => x.ReferralId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasIndex(x => x.ReferralId);
                entity.HasIndex(x => x.CreatedAt);
            });
        }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            ConfigureDeathRegisterEntities(modelBuilder);

            modelBuilder.Entity<StateOfficeMonthlyReport>(entity =>
            {
                entity.ToTable("StateOfficeMonthlyReports");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.ReportingMonth).HasColumnType("date");
                entity.Property(x => x.State).IsRequired().HasMaxLength(100);
                entity.Property(x => x.Lga).IsRequired().HasMaxLength(100);
                entity.Property(x => x.Ward).IsRequired().HasMaxLength(100);
                entity.Property(x => x.FacilityName).IsRequired().HasMaxLength(200);
                entity.Property(x => x.FacilityCode).IsRequired().HasMaxLength(100);
                entity.Property(x => x.ReportingOfficerName).IsRequired().HasMaxLength(200);
                entity.Property(x => x.Designation).IsRequired().HasMaxLength(150);
                entity.Property(x => x.PhoneNumber).IsRequired().HasMaxLength(30);
                entity.Property(x => x.SubmittedByUserId).HasMaxLength(450);
                entity.Property(x => x.SubmittedByName).HasMaxLength(200);
                entity.Property(x => x.ReferralCompletionRate).HasColumnType("decimal(18,2)");
                entity.Property(x => x.AmountCapitationPaid).HasColumnType("decimal(18,2)");
                entity.Property(x => x.CapitationToUtilizationRatio).HasColumnType("decimal(18,2)");
                entity.Property(x => x.TotalClaimsAmount).HasColumnType("decimal(18,2)");
                entity.Property(x => x.PaidClaimsAmount).HasColumnType("decimal(18,2)");
                entity.Property(x => x.AuditStatus).IsRequired().HasMaxLength(50).HasDefaultValue("Pending");
                entity.Property(x => x.AuditedByUserId).HasMaxLength(450);
                entity.Property(x => x.AuditedByName).HasMaxLength(200);
                entity.Property(x => x.AuditNote).HasMaxLength(1000);
                entity.HasIndex(x => x.ReportingMonth);
                entity.HasIndex(x => x.State);
                entity.HasIndex(x => x.ProviderId);
                entity.HasIndex(x => x.AuditStatus);
            });

            modelBuilder.Entity<ProgramMonitoringTarget>(entity =>
            {
                entity.ToTable("ProgramMonitoringTargets");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Scope).IsRequired().HasMaxLength(100);
                entity.Property(x => x.UpdatedByUserId).HasMaxLength(450);
                entity.Property(x => x.UpdatedByName).HasMaxLength(200);
                entity.HasIndex(x => x.Scope).IsUnique();
            });

            modelBuilder.Entity<EncounterService>(entity =>
            {
                entity.ToTable("EncounterServices");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.ServiceSetting).IsRequired().HasMaxLength(50);
                entity.Property(x => x.ServiceName).IsRequired().HasMaxLength(200);
                entity.HasIndex(x => x.EncounterId);
                entity.HasIndex(x => x.ServiceName);
                entity.HasIndex(x => new { x.EncounterId, x.ServiceName }).IsUnique();
                entity.HasOne(x => x.Encounter)
                    .WithMany(x => x.Services)
                    .HasForeignKey(x => x.EncounterId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Encounter>()
                .Property(x => x.ServiceSetting)
                .IsRequired()
                .HasMaxLength(50)
                .HasDefaultValue(EncounterServiceCatalog.Outpatient);

            modelBuilder.Entity<Doctor>(entity =>
            {
                entity.ToTable("Doctors");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.FullName).IsRequired().HasMaxLength(200);
                entity.Property(x => x.MedicalLicenseNumber).IsRequired().HasMaxLength(100);
                entity.Property(x => x.Specialty).IsRequired().HasMaxLength(150);
                entity.Property(x => x.Designation).HasMaxLength(150);
                entity.Property(x => x.Phone).HasMaxLength(30);
                entity.Property(x => x.Email).HasMaxLength(150);
                entity.HasIndex(x => x.ProviderId);
                entity.HasIndex(x => new { x.ProviderId, x.MedicalLicenseNumber }).IsUnique();
                entity.HasOne(x => x.Provider)
                    .WithMany(x => x.Doctors)
                    .HasForeignKey(x => x.ProviderId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Complaint>(entity =>
            {
                entity.ToTable("Complaints");
                entity.HasKey(x => x.Id);
                entity.Property(x => x.ReferenceNumber).IsRequired().HasMaxLength(40);
                entity.Property(x => x.Subject).IsRequired().HasMaxLength(200);
                entity.Property(x => x.Description).IsRequired().HasMaxLength(3000);
                entity.Property(x => x.State).IsRequired().HasMaxLength(100);
                entity.Property(x => x.SubmittedByUserId).HasMaxLength(450);
                entity.Property(x => x.SubmittedByName).HasMaxLength(200);
                entity.Property(x => x.SubmittedByRole).HasMaxLength(100);
                entity.Property(x => x.AssignedToUserId).HasMaxLength(450);
                entity.Property(x => x.AssignedToName).HasMaxLength(200);
                entity.Property(x => x.ResolutionNote).HasMaxLength(2000);
                entity.HasIndex(x => x.ReferenceNumber).IsUnique();
                entity.HasIndex(x => x.Status);
                entity.HasIndex(x => x.Priority);
                entity.HasIndex(x => x.State);
                entity.HasIndex(x => x.HmoId);
                entity.HasIndex(x => x.ProviderId);
                entity.HasOne(x => x.Hmo)
                    .WithMany()
                    .HasForeignKey(x => x.HmoId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.Provider)
                    .WithMany()
                    .HasForeignKey(x => x.ProviderId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.Enrollee)
                    .WithMany()
                    .HasForeignKey(x => x.EnrolleeId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

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

            modelBuilder.Entity<Encounter>()
                .HasOne(e => e.Doctor)
                .WithMany(d => d.Encounters)
                .HasForeignKey(e => e.DoctorId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);
      
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

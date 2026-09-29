using GoBeyond.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GoBeyond.Infrastructure.Database;

public class GoBeyondDbContext(DbContextOptions<GoBeyondDbContext> options) : DbContext(options)
{
    // Šifarnici
    public DbSet<TrainingType> TrainingTypes => Set<TrainingType>();
    public DbSet<FitnessGoal> FitnessGoals => Set<FitnessGoal>();
    public DbSet<FitnessLevel> FitnessLevels => Set<FitnessLevel>();
    public DbSet<Gender> Genders => Set<Gender>();

    // Domenske tabele
    public DbSet<User> Users => Set<User>();
    public DbSet<MentorProfile> MentorProfiles => Set<MentorProfile>();
    public DbSet<MentorSpecialization> MentorSpecializations => Set<MentorSpecialization>();
    public DbSet<MentorCertificate> MentorCertificates => Set<MentorCertificate>();
    public DbSet<ClientProfile> ClientProfiles => Set<ClientProfile>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Questionnaire> Questionnaires => Set<Questionnaire>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<TrainingPlan> TrainingPlans => Set<TrainingPlan>();
    public DbSet<DayPlan> DayPlans => Set<DayPlan>();
    public DbSet<TrainingSession> TrainingSessions => Set<TrainingSession>();
    public DbSet<ProgressEntry> ProgressEntries => Set<ProgressEntry>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserActivity> UserActivities => Set<UserActivity>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>
    /// Svi DateTime podaci se čuvaju u UTC. SQL Server ne čuva "Kind", pa se pri čitanju označavaju kao UTC
    /// kako bi JSON imao sufiks "Z" (ISO-8601 UTC, prema API ugovoru).
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        value => value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : value,
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
        value => value.HasValue && value.Value.Kind == DateTimeKind.Local ? value.Value.ToUniversalTime() : value,
        value => value.HasValue ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc) : value);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureReferenceTables(modelBuilder);
        ConfigureUsers(modelBuilder);
        ConfigureSubscriptions(modelBuilder);
        ConfigurePlans(modelBuilder);
        ConfigureCommunication(modelBuilder);
    }

    private static void ConfigureReferenceTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TrainingType>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(500).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<FitnessGoal>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(60).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(300);
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<FitnessLevel>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(300);
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<Gender>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(30).IsRequired();
            entity.HasIndex(x => x.Name).IsUnique();
        });
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(x => x.FirstName).HasMaxLength(50).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(50).IsRequired();
            entity.Property(x => x.Username).HasMaxLength(30).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(100).IsRequired();
            entity.Property(x => x.PhoneNumber).HasMaxLength(20);
            entity.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ProfileImageUrl).HasMaxLength(300);
            // Postojeći korisnici pri migraciji dobijaju nasumičnu vrijednost.
            entity.Property(x => x.SecurityStamp).HasDefaultValueSql("NEWID()");
            entity.HasIndex(x => x.Username).IsUnique();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.HasOne(x => x.Gender).WithMany().HasForeignKey(x => x.GenderId).OnDelete(DeleteBehavior.Restrict);
            entity.Ignore(x => x.FullName);
        });

        modelBuilder.Entity<MentorProfile>(entity =>
        {
            entity.Property(x => x.Nickname).HasMaxLength(50);
            entity.Property(x => x.Bio).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.MonthlyPrice).HasPrecision(10, 2);
            entity.Property(x => x.RejectionReason).HasMaxLength(500);
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasOne(x => x.User).WithOne(x => x.MentorProfile)
                .HasForeignKey<MentorProfile>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.TrainingType).WithMany()
                .HasForeignKey(x => x.TrainingTypeId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_MentorProfiles_YearsOfExperience", "[YearsOfExperience] BETWEEN 0 AND 60");
                t.HasCheckConstraint("CK_MentorProfiles_MonthlyPrice", "[MonthlyPrice] BETWEEN 1 AND 1000");
            });
        });

        modelBuilder.Entity<MentorSpecialization>(entity =>
        {
            entity.HasKey(x => new { x.MentorProfileId, x.FitnessGoalId });
            entity.HasOne(x => x.MentorProfile).WithMany(x => x.Specializations)
                .HasForeignKey(x => x.MentorProfileId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.FitnessGoal).WithMany()
                .HasForeignKey(x => x.FitnessGoalId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<MentorCertificate>(entity =>
        {
            entity.Property(x => x.FileName).HasMaxLength(255).IsRequired();
            entity.Property(x => x.FileUrl).HasMaxLength(300).IsRequired();
            entity.HasOne(x => x.MentorProfile).WithMany(x => x.Certificates)
                .HasForeignKey(x => x.MentorProfileId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ClientProfile>(entity =>
        {
            entity.Property(x => x.WeightKg).HasPrecision(6, 2);
            entity.Property(x => x.HeightCm).HasPrecision(6, 2);
            entity.Property(x => x.GoalDescription).HasMaxLength(500);
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasOne(x => x.User).WithOne(x => x.ClientProfile)
                .HasForeignKey<ClientProfile>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.FitnessLevel).WithMany()
                .HasForeignKey(x => x.FitnessLevelId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.FitnessGoal).WithMany()
                .HasForeignKey(x => x.FitnessGoalId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.PreferredTrainingType).WithMany()
                .HasForeignKey(x => x.PreferredTrainingTypeId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_ClientProfiles_WeightKg", "[WeightKg] BETWEEN 30 AND 300");
                t.HasCheckConstraint("CK_ClientProfiles_HeightCm", "[HeightCm] BETWEEN 100 AND 250");
                t.HasCheckConstraint("CK_ClientProfiles_TrainingExperienceYears", "[TrainingExperienceYears] BETWEEN 0 AND 60");
            });
        });

        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasOne(x => x.User).WithMany(x => x.RefreshTokens)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserActivity>(entity =>
        {
            entity.HasIndex(x => new { x.UserId, x.Day }).IsUnique();
            entity.HasOne(x => x.User).WithMany(x => x.Activities)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureSubscriptions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.Property(x => x.Price).HasPrecision(10, 2);
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.StatusReason).HasMaxLength(500);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.ClientProfileId);
            // Klijent ima najviše jednu otvorenu (PendingPayment/AwaitingMentor/Active) pretplatu, i kod istovremenih zahtjeva.
            entity.HasIndex(x => x.ClientProfileId, "UX_Subscriptions_ClientProfileId_Open").IsUnique()
                .HasFilter($"[Status] IN ({(int)Core.Enums.SubscriptionStatus.PendingPayment}, {(int)Core.Enums.SubscriptionStatus.AwaitingMentor}, " +
                           $"{(int)Core.Enums.SubscriptionStatus.Active})");
            // Svaki UPDATE pretplate je uslovljen pročitanim statusom (WHERE Id = @id AND Status = @status): upis nad
            // zastarjelim stanjem (npr. prihvatanje nakon istovremenog odbijanja) ne uspijeva, umjesto da tiho pregazi prelaz.
            entity.Property(x => x.Status).IsConcurrencyToken();
            entity.HasOne(x => x.ClientProfile).WithMany(x => x.Subscriptions)
                .HasForeignKey(x => x.ClientProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.MentorProfile).WithMany(x => x.Subscriptions)
                .HasForeignKey(x => x.MentorProfileId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Questionnaire>(entity =>
        {
            entity.Property(x => x.PrimaryGoal).HasMaxLength(500).IsRequired();
            entity.Property(x => x.TimeCommitment).HasMaxLength(500).IsRequired();
            entity.Property(x => x.HealthIssues).HasMaxLength(500).IsRequired();
            entity.Property(x => x.Medications).HasMaxLength(500).IsRequired();
            entity.Property(x => x.WeeklySessions).HasMaxLength(500).IsRequired();
            entity.Property(x => x.OutsideActivity).HasMaxLength(500).IsRequired();
            entity.HasIndex(x => x.SubscriptionId).IsUnique();
            entity.HasOne(x => x.Subscription).WithOne(x => x.Questionnaire)
                .HasForeignKey<Questionnaire>(x => x.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.Property(x => x.Amount).HasPrecision(10, 2);
            entity.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            entity.Property(x => x.StripePaymentIntentId).HasMaxLength(100).IsRequired();
            entity.HasIndex(x => x.StripePaymentIntentId).IsUnique();
            entity.HasOne(x => x.Subscription).WithMany(x => x.Payments)
                .HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.Property(x => x.Comment).HasMaxLength(1000).IsRequired();
            entity.HasIndex(x => x.SubscriptionId).IsUnique();
            entity.HasOne(x => x.Subscription).WithOne(x => x.Review)
                .HasForeignKey<Review>(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ClientProfile).WithMany(x => x.Reviews)
                .HasForeignKey(x => x.ClientProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.MentorProfile).WithMany(x => x.Reviews)
                .HasForeignKey(x => x.MentorProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t => t.HasCheckConstraint("CK_Reviews_Rating", "[Rating] BETWEEN 1 AND 5"));
        });
    }

    private static void ConfigurePlans(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TrainingPlan>(entity =>
        {
            entity.Property(x => x.MotivationalQuote).HasMaxLength(300);
            entity.HasIndex(x => x.SubscriptionId).IsUnique();
            entity.HasOne(x => x.Subscription).WithOne(x => x.TrainingPlan)
                .HasForeignKey<TrainingPlan>(x => x.SubscriptionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.MentorProfile).WithMany(x => x.TrainingPlans)
                .HasForeignKey(x => x.MentorProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ClientProfile).WithMany()
                .HasForeignKey(x => x.ClientProfileId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DayPlan>(entity =>
        {
            entity.Property(x => x.TrainingDescription).HasMaxLength(8000).IsRequired();
            entity.Property(x => x.NutritionDescription).HasMaxLength(8000).IsRequired();
            entity.HasIndex(x => new { x.TrainingPlanId, x.DayOfWeek }).IsUnique();
            entity.HasOne(x => x.TrainingPlan).WithMany(x => x.Days)
                .HasForeignKey(x => x.TrainingPlanId).OnDelete(DeleteBehavior.Cascade);
            entity.ToTable(t =>
            {
                t.HasCheckConstraint("CK_DayPlans_DayOfWeek", "[DayOfWeek] BETWEEN 1 AND 7");
                t.HasCheckConstraint("CK_DayPlans_TrainingDuration", "[TrainingDurationMinutes] BETWEEN 1 AND 600");
                t.HasCheckConstraint("CK_DayPlans_NutritionDuration",
                    "[NutritionDurationMinutes] IS NULL OR [NutritionDurationMinutes] BETWEEN 1 AND 1440");
            });
        });

        modelBuilder.Entity<TrainingSession>(entity =>
        {
            entity.Property(x => x.Note).HasMaxLength(500);
            entity.HasOne(x => x.TrainingPlan).WithMany(x => x.Sessions)
                .HasForeignKey(x => x.TrainingPlanId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.DayPlan).WithMany()
                .HasForeignKey(x => x.DayPlanId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.ClientProfile).WithMany(x => x.TrainingSessions)
                .HasForeignKey(x => x.ClientProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t => t.HasCheckConstraint("CK_TrainingSessions_Repetitions", "[Repetitions] BETWEEN 1 AND 10000"));
        });

        modelBuilder.Entity<ProgressEntry>(entity =>
        {
            entity.Property(x => x.WeightKg).HasPrecision(6, 2);
            entity.Property(x => x.PhotoUrl).HasMaxLength(300);
            entity.Property(x => x.Measurements).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Strength).HasMaxLength(300).IsRequired();
            entity.Property(x => x.Conditioning).HasMaxLength(300).IsRequired();
            entity.HasIndex(x => new { x.ClientProfileId, x.Year, x.Month }).IsUnique();
            entity.HasOne(x => x.ClientProfile).WithMany(x => x.ProgressEntries)
                .HasForeignKey(x => x.ClientProfileId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.TrainingPlan).WithMany()
                .HasForeignKey(x => x.TrainingPlanId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(t => t.HasCheckConstraint("CK_ProgressEntries_Month", "[Month] BETWEEN 1 AND 12"));
        });
    }

    private static void ConfigureCommunication(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(150).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(2000).IsRequired();
            entity.HasIndex(x => new { x.UserId, x.IsRead });
            entity.HasOne(x => x.User).WithMany(x => x.Notifications)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Announcement).WithMany(x => x.Notifications)
                .HasForeignKey(x => x.AnnouncementId).OnDelete(DeleteBehavior.ClientSetNull);
            entity.HasOne<User>().WithMany()
                .HasForeignKey(x => x.SenderUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Announcement>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Content).HasMaxLength(2000).IsRequired();
            entity.HasOne(x => x.CreatedByUser).WithMany()
                .HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Message>(entity =>
        {
            entity.Property(x => x.Content).HasMaxLength(2000).IsRequired();
            entity.HasIndex(x => new { x.SubscriptionId, x.SentAt });
            entity.HasOne(x => x.Subscription).WithMany(x => x.Messages)
                .HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.SenderUser).WithMany()
                .HasForeignKey(x => x.SenderUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.Property(x => x.EventType).HasMaxLength(60).IsRequired();
            entity.Property(x => x.RecipientEmail).HasMaxLength(100).IsRequired();
            entity.Property(x => x.Subject).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Body).HasMaxLength(4000).IsRequired();
            entity.Property(x => x.LastError).HasMaxLength(500);
            entity.HasIndex(x => new { x.SentAt, x.FailedAt });
            entity.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}

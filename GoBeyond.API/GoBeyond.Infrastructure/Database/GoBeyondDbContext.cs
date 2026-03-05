using GoBeyond.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Database;

public class GoBeyondDbContext(DbContextOptions<GoBeyondDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<MentorProfile> MentorProfiles => Set<MentorProfile>();
    public DbSet<MentorCertificate> MentorCertificates => Set<MentorCertificate>();
    public DbSet<ClientProfile> ClientProfiles => Set<ClientProfile>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Questionnaire> Questionnaires => Set<Questionnaire>();
    public DbSet<TrainingPlan> TrainingPlans => Set<TrainingPlan>();
    public DbSet<DayPlan> DayPlans => Set<DayPlan>();
    public DbSet<ProgressEntry> ProgressEntries => Set<ProgressEntry>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.Email).HasMaxLength(255);
            entity.Property(x => x.FirstName).HasMaxLength(100);
            entity.Property(x => x.LastName).HasMaxLength(100);
        });

        modelBuilder.Entity<MentorProfile>()
            .HasOne(x => x.User)
            .WithOne(x => x.MentorProfile)
            .HasForeignKey<MentorProfile>(x => x.UserId);

        modelBuilder.Entity<ClientProfile>()
            .HasOne(x => x.User)
            .WithOne(x => x.ClientProfile)
            .HasForeignKey<ClientProfile>(x => x.UserId);

        modelBuilder.Entity<MentorCertificate>()
            .HasOne(x => x.MentorProfile)
            .WithMany(x => x.Certificates)
            .HasForeignKey(x => x.MentorProfileId);

        modelBuilder.Entity<Subscription>()
            .HasOne(x => x.ClientProfile)
            .WithMany(x => x.Subscriptions)
            .HasForeignKey(x => x.ClientProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Subscription>()
            .HasOne(x => x.MentorProfile)
            .WithMany(x => x.Subscriptions)
            .HasForeignKey(x => x.MentorProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Questionnaire>()
            .HasOne(x => x.Subscription)
            .WithOne(x => x.Questionnaire)
            .HasForeignKey<Questionnaire>(x => x.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TrainingPlan>()
            .HasOne(x => x.Subscription)
            .WithMany(x => x.TrainingPlans)
            .HasForeignKey(x => x.SubscriptionId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TrainingPlan>()
            .HasOne(x => x.MentorProfile)
            .WithMany(x => x.TrainingPlans)
            .HasForeignKey(x => x.MentorProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TrainingPlan>()
            .HasOne(x => x.ClientProfile)
            .WithMany(x => x.TrainingPlans)
            .HasForeignKey(x => x.ClientProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DayPlan>()
            .HasOne(x => x.TrainingPlan)
            .WithMany(x => x.DayPlans)
            .HasForeignKey(x => x.TrainingPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProgressEntry>()
            .HasOne(x => x.ClientProfile)
            .WithMany(x => x.ProgressEntries)
            .HasForeignKey(x => x.ClientProfileId);

        modelBuilder.Entity<Review>()
            .HasOne(x => x.ClientProfile)
            .WithMany(x => x.Reviews)
            .HasForeignKey(x => x.ClientProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Review>()
            .HasOne(x => x.MentorProfile)
            .WithMany(x => x.Reviews)
            .HasForeignKey(x => x.MentorProfileId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Notification>()
            .HasOne(x => x.User)
            .WithMany(x => x.Notifications)
            .HasForeignKey(x => x.UserId);

        modelBuilder.Entity<Payment>()
            .HasOne(x => x.Subscription)
            .WithMany(x => x.Payments)
            .HasForeignKey(x => x.SubscriptionId);

        modelBuilder.Entity<RefreshToken>()
            .HasOne(x => x.User)
            .WithMany(x => x.RefreshTokens)
            .HasForeignKey(x => x.UserId);
    }
}

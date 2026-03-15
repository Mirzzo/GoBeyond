using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Infrastructure.Utilities;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Database;

public class DatabaseSeeder(
    GoBeyondDbContext dbContext,
    IPasswordHasherService passwordHasherService) : IDatabaseSeeder
{
    private const string AdminEmail = "admin@gobeyond.local";
    private const string AdminPassword = "Admin123!";
    private const string MentorEmail = "mentor@gobeyond.local";
    private const string MentorPassword = "Mentor123!";
    private const string ClientEmail = "client@gobeyond.local";
    private const string ClientPassword = "Client123!";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var admin = await EnsureUserAsync(
            email: AdminEmail,
            password: AdminPassword,
            firstName: "System",
            lastName: "Admin",
            role: UserRole.Admin,
            cancellationToken);

        var mila = await EnsureUserAsync(
            email: MentorEmail,
            password: MentorPassword,
            firstName: "Mila",
            lastName: "Mentor",
            role: UserRole.Mentor,
            cancellationToken);

        var lejla = await EnsureUserAsync(
            email: "lejla@gobeyond.local",
            password: MentorPassword,
            firstName: "Lejla",
            lastName: "Kovac",
            role: UserRole.Mentor,
            cancellationToken);

        var amir = await EnsureUserAsync(
            email: "amir@gobeyond.local",
            password: MentorPassword,
            firstName: "Amir",
            lastName: "Hadzic",
            role: UserRole.Mentor,
            cancellationToken);

        var mia = await EnsureUserAsync(
            email: "mia@gobeyond.local",
            password: MentorPassword,
            firstName: "Mia",
            lastName: "Novak",
            role: UserRole.Mentor,
            cancellationToken);

        var luka = await EnsureUserAsync(
            email: ClientEmail,
            password: ClientPassword,
            firstName: "Luka",
            lastName: "Client",
            role: UserRole.Client,
            cancellationToken);

        var ana = await EnsureUserAsync(
            email: "ana@gobeyond.local",
            password: ClientPassword,
            firstName: "Ana",
            lastName: "Hodzic",
            role: UserRole.Client,
            cancellationToken);

        var ivan = await EnsureUserAsync(
            email: "ivan@gobeyond.local",
            password: ClientPassword,
            firstName: "Ivan",
            lastName: "Klaric",
            role: UserRole.Client,
            cancellationToken);

        ConfigureMentorProfile(
            mila,
            bio: "Certified strength coach focused on structured weekly progress and recovery-aware programming.",
            age: 29,
            category: MentorCategory.Hybrid,
            price: 29.99m,
            status: MentorApprovalStatus.Approved);
        ConfigureMentorProfile(
            lejla,
            bio: "Structured coaching for people with busy schedules, habit resets and long-term strength gains.",
            age: 31,
            category: MentorCategory.Hybrid,
            price: 49m,
            status: MentorApprovalStatus.Approved);
        ConfigureMentorProfile(
            amir,
            bio: "Bodyweight progressions, clean technique and mobility-first calisthenics planning.",
            age: 28,
            category: MentorCategory.Calisthenics,
            price: 39m,
            status: MentorApprovalStatus.Approved);
        ConfigureMentorProfile(
            mia,
            bio: "Technique-first olympic lifting cycles with measured volume and video review.",
            age: 30,
            category: MentorCategory.Weightlifting,
            price: 59m,
            status: MentorApprovalStatus.Pending);

        ConfigureClientProfile(luka, weight: 78.5m, height: 182m, age: 25, fitnessLevel: "Intermediate");
        ConfigureClientProfile(ana, weight: 68m, height: 170m, age: 27, fitnessLevel: "Beginner");
        ConfigureClientProfile(ivan, weight: 90m, height: 188m, age: 32, fitnessLevel: "Advanced");

        EnsureCertificate(mila.MentorProfile!, "strength-cert.pdf", "https://files.gobeyond.local/certificates/strength-cert.pdf");
        EnsureCertificate(lejla.MentorProfile!, "hybrid-reset-cert.pdf", "https://files.gobeyond.local/certificates/hybrid-reset-cert.pdf");
        EnsureCertificate(amir.MentorProfile!, "calisthenics-level-2.pdf", "https://files.gobeyond.local/certificates/calisthenics-level-2.pdf");
        EnsureCertificate(mia.MentorProfile!, "weightlifting-coach.pdf", "https://files.gobeyond.local/certificates/weightlifting-coach.pdf");

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var today = DateTime.UtcNow.Date;

        var lukaSubscription = await EnsureSubscriptionAsync(
            luka.ClientProfile!,
            mila.MentorProfile!,
            SubscriptionStatus.Active,
            today.AddDays(-21),
            today.AddDays(7),
            mila.MentorProfile.Price,
            new QuestionnaireSeed(
                "Build strength while dropping body fat",
                "4 training sessions weekly",
                "Occasional lower back tightness",
                "None",
                "Mon, Wed, Fri + Sat",
                "Intermediate"),
            cancellationToken);

        var anaSubscription = await EnsureSubscriptionAsync(
            ana.ClientProfile!,
            mila.MentorProfile!,
            SubscriptionStatus.Active,
            today.AddDays(-3),
            today.AddDays(25),
            mila.MentorProfile.Price,
            new QuestionnaireSeed(
                "Create consistency and basic training habits",
                "3 short sessions weekly",
                "No major health issues",
                "None",
                "Tue, Thu, Sat",
                "Beginner"),
            cancellationToken);

        var ivanSubscription = await EnsureSubscriptionAsync(
            ivan.ClientProfile!,
            mila.MentorProfile!,
            SubscriptionStatus.Active,
            today.AddDays(-12),
            today.AddDays(16),
            mila.MentorProfile.Price,
            new QuestionnaireSeed(
                "Explosive strength and cleaner lifting mechanics",
                "5 sessions weekly",
                "Old shoulder irritation during high volume",
                "Occasional anti-inflammatory",
                "Weekday evenings",
                "Advanced"),
            cancellationToken);

        var lukaLejlaSubscription = await EnsureSubscriptionAsync(
            luka.ClientProfile!,
            lejla.MentorProfile!,
            SubscriptionStatus.Expired,
            today.AddDays(-90),
            today.AddDays(-60),
            lejla.MentorProfile.Price,
            new QuestionnaireSeed(
                "Reset habits after a long break",
                "3 sessions weekly",
                "None",
                "None",
                "Flexible",
                "Intermediate"),
            cancellationToken);

        await EnsurePaymentAsync(lukaSubscription, PaymentStatus.Succeeded, "seed_pi_luka_mila", cancellationToken);
        await EnsurePaymentAsync(anaSubscription, PaymentStatus.Succeeded, "seed_pi_ana_mila", cancellationToken);
        await EnsurePaymentAsync(ivanSubscription, PaymentStatus.Succeeded, "seed_pi_ivan_mila", cancellationToken);
        await EnsurePaymentAsync(lukaLejlaSubscription, PaymentStatus.Succeeded, "seed_pi_luka_lejla", cancellationToken);

        await EnsureTrainingPlanAsync(
            lukaSubscription,
            mila.MentorProfile!,
            luka.ClientProfile!,
            4,
            "Consistency beats intensity when the calendar gets loud.",
            TrainingPlanStatus.Published,
            [
                new DayPlanSeed(DayOfWeek.Monday, 58, "Tempo squat 5x4, RDL 4x8 and carries.", 15, "Protein-focused meal prep and hydration targets."),
                new DayPlanSeed(DayOfWeek.Tuesday, 32, "Zone 2 bike and mobility reset.", 10, "Lower-calorie recovery day with high vegetables."),
                new DayPlanSeed(DayOfWeek.Wednesday, 51, "Upper push and pull hypertrophy circuit.", 12, "Balanced plate template with post-workout carbs."),
                new DayPlanSeed(DayOfWeek.Thursday, 25, "Check-in walk and breathing reset.", 10, "Sleep-first nutrition reminders."),
                new DayPlanSeed(DayOfWeek.Friday, 46, "Power circuit with carries and sled work.", 14, "Higher-carb day for the heaviest session.")
            ],
            cancellationToken);

        await EnsureTrainingPlanAsync(
            ivanSubscription,
            mila.MentorProfile!,
            ivan.ClientProfile!,
            2,
            "Sharp technique under calm effort beats rushed heavy reps.",
            TrainingPlanStatus.Published,
            [
                new DayPlanSeed(DayOfWeek.Monday, 75, "Snatch complexes and front squat volume.", 15, "Pre-lift carb timing and recovery meal."),
                new DayPlanSeed(DayOfWeek.Wednesday, 68, "Clean pulls, jerk balance and accessory rows.", 12, "Protein target with low-fat dinner."),
                new DayPlanSeed(DayOfWeek.Friday, 70, "Clean and jerk waves plus posterior chain work.", 15, "Carb reload around the main session."),
                new DayPlanSeed(DayOfWeek.Saturday, 35, "Mobility flush and shoulder stability work.", 10, "Hydration, sodium and easy digestion focus.")
            ],
            cancellationToken);

        await EnsureProgressEntryAsync(
            luka.ClientProfile!,
            today.Year,
            Math.Max(1, today.Month - 2),
            78.5m,
            "Waist -1 cm",
            "Tempo squat moving better.",
            "Zone 2 feels easier.",
            "https://files.gobeyond.local/progress/luka-1.jpg",
            cancellationToken);
        await EnsureProgressEntryAsync(
            luka.ClientProfile!,
            today.Year,
            Math.Max(1, today.Month - 1),
            77.2m,
            "Waist -2 cm",
            "Added 5 kg on squat work sets.",
            "Recovery between rounds improved.",
            "https://files.gobeyond.local/progress/luka-2.jpg",
            cancellationToken);
        await EnsureProgressEntryAsync(
            luka.ClientProfile!,
            today.Year,
            today.Month,
            76.6m,
            "Waist -3 cm",
            "Posterior chain volume now stable.",
            "Power circuit completed without extra rest.",
            "https://files.gobeyond.local/progress/luka-3.jpg",
            cancellationToken);

        await EnsureProgressEntryAsync(
            ivan.ClientProfile!,
            today.Year,
            Math.Max(1, today.Month - 1),
            90.4m,
            "Shoulder pain lower on overhead days.",
            "Snatch timing improving.",
            "Short conditioning flush tolerated well.",
            null,
            cancellationToken);
        await EnsureProgressEntryAsync(
            ivan.ClientProfile!,
            today.Year,
            today.Month,
            89.9m,
            "Bar path cleaner in video review.",
            "Clean pull power improved.",
            "Sleep score steadier.",
            null,
            cancellationToken);

        await EnsureReviewAsync(luka.ClientProfile!, mila.MentorProfile!, 5, "The first coach who adjusted my plan when my week fell apart.", cancellationToken);
        await EnsureReviewAsync(ivan.ClientProfile!, mila.MentorProfile!, 4, "Clear cues and realistic weekly structure.", cancellationToken);
        await EnsureReviewAsync(luka.ClientProfile!, lejla.MentorProfile!, 5, "Structured habit reset without unrealistic volume.", cancellationToken);
        await EnsureReviewAsync(ana.ClientProfile!, amir.MentorProfile!, 4, "Mobility progressions were easy to follow.", cancellationToken);

        EnsureNotification(
            luka.Id,
            "Training plan ready",
            "Week 4 is available in your client app.",
            NotificationType.PlanReady);
        EnsureNotification(
            luka.Id,
            "Payment confirmed",
            "Your subscription payment has been recorded successfully.",
            NotificationType.PlanReady);
        EnsureNotification(
            mila.Id,
            "New collaboration request",
            "Ana Hodzic submitted onboarding details and is waiting for a plan.",
            NotificationType.NewSubscriber);
        EnsureNotification(
            admin.Id,
            "Mentor approval waiting",
            "Mia Novak still needs an admin approval decision.",
            NotificationType.MentorApproved);
        EnsureNotification(
            mia.Id,
            "Mentor account pending",
            "Your mentor account is waiting for admin approval.",
            NotificationType.MentorApproved);

        if (dbContext.ChangeTracker.HasChanges())
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static void ConfigureMentorProfile(
        User user,
        string bio,
        int age,
        MentorCategory category,
        decimal price,
        MentorApprovalStatus status)
    {
        user.MentorProfile ??= new MentorProfile();
        user.MentorProfile.Bio = bio;
        user.MentorProfile.Age = age;
        user.MentorProfile.Category = category;
        user.MentorProfile.Price = price;
        user.MentorProfile.Status = status;
    }

    private static void ConfigureClientProfile(
        User user,
        decimal weight,
        decimal height,
        int age,
        string fitnessLevel)
    {
        user.ClientProfile ??= new ClientProfile();
        user.ClientProfile.Weight = weight;
        user.ClientProfile.Height = height;
        user.ClientProfile.Age = age;
        user.ClientProfile.FitnessLevel = fitnessLevel;
    }

    private static void EnsureCertificate(MentorProfile mentorProfile, string fileName, string fileUrl)
    {
        var existing = mentorProfile.Certificates.FirstOrDefault(x => x.FileName == fileName);
        if (existing is null)
        {
            mentorProfile.Certificates.Add(new MentorCertificate
            {
                FileName = fileName,
                FileUrl = fileUrl
            });
            return;
        }

        existing.FileUrl = fileUrl;
    }

    private async Task<User> EnsureUserAsync(
        string email,
        string password,
        string firstName,
        string lastName,
        UserRole role,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var user = await dbContext.Users
            .Include(x => x.MentorProfile)
                .ThenInclude(x => x!.Certificates)
            .Include(x => x.ClientProfile)
            .FirstOrDefaultAsync(x => x.Email == normalizedEmail, cancellationToken);

        if (user is null)
        {
            user = new User
            {
                FirstName = firstName,
                LastName = lastName,
                Email = normalizedEmail,
                PasswordHash = passwordHasherService.Hash(password),
                Role = role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            dbContext.Users.Add(user);
            return user;
        }

        user.FirstName = firstName;
        user.LastName = lastName;
        user.Role = role;
        user.IsActive = true;

        if (!passwordHasherService.Verify(password, user.PasswordHash))
        {
            user.PasswordHash = passwordHasherService.Hash(password);
        }

        return user;
    }

    private async Task<Subscription> EnsureSubscriptionAsync(
        ClientProfile clientProfile,
        MentorProfile mentorProfile,
        SubscriptionStatus status,
        DateTime startDate,
        DateTime endDate,
        decimal amountPaid,
        QuestionnaireSeed questionnaireSeed,
        CancellationToken cancellationToken)
    {
        var subscription = await dbContext.Subscriptions
            .Include(x => x.Questionnaire)
            .Include(x => x.Payments)
            .Include(x => x.TrainingPlans)
                .ThenInclude(x => x.DayPlans)
            .FirstOrDefaultAsync(
                x => x.ClientProfileId == clientProfile.Id && x.MentorProfileId == mentorProfile.Id,
                cancellationToken);

        if (subscription is null)
        {
            subscription = new Subscription
            {
                ClientProfileId = clientProfile.Id,
                MentorProfileId = mentorProfile.Id
            };
            dbContext.Subscriptions.Add(subscription);
        }

        subscription.Status = status;
        subscription.StartDate = startDate;
        subscription.EndDate = endDate;
        subscription.AmountPaid = amountPaid;

        subscription.Questionnaire ??= new Questionnaire
        {
            ClientProfileId = clientProfile.Id
        };
        subscription.Questionnaire.PrimaryGoal = questionnaireSeed.PrimaryGoal;
        subscription.Questionnaire.TimeCommitment = questionnaireSeed.TimeCommitment;
        subscription.Questionnaire.HealthIssues = questionnaireSeed.HealthIssues;
        subscription.Questionnaire.Medications = questionnaireSeed.Medications;
        subscription.Questionnaire.WeeklyAvailability = questionnaireSeed.WeeklyAvailability;
        subscription.Questionnaire.PhysicalActivityLevel = questionnaireSeed.PhysicalActivityLevel;

        await dbContext.SaveChangesAsync(cancellationToken);
        return subscription;
    }

    private async Task EnsurePaymentAsync(
        Subscription subscription,
        PaymentStatus status,
        string paymentIntentId,
        CancellationToken cancellationToken)
    {
        var payment = await dbContext.Payments
            .FirstOrDefaultAsync(x => x.SubscriptionId == subscription.Id && x.StripePaymentIntentId == paymentIntentId, cancellationToken);

        if (payment is null)
        {
            payment = new Payment
            {
                SubscriptionId = subscription.Id,
                StripePaymentIntentId = paymentIntentId
            };
            dbContext.Payments.Add(payment);
        }

        payment.Amount = subscription.AmountPaid;
        payment.Currency = "bam";
        payment.Status = status;
    }

    private async Task EnsureTrainingPlanAsync(
        Subscription subscription,
        MentorProfile mentorProfile,
        ClientProfile clientProfile,
        int weekNumber,
        string motivationalQuote,
        TrainingPlanStatus status,
        IReadOnlyList<DayPlanSeed> days,
        CancellationToken cancellationToken)
    {
        var trainingPlan = await dbContext.TrainingPlans
            .Include(x => x.DayPlans)
            .FirstOrDefaultAsync(
                x => x.SubscriptionId == subscription.Id && x.WeekNumber == weekNumber,
                cancellationToken);

        if (trainingPlan is null)
        {
            trainingPlan = new TrainingPlan
            {
                SubscriptionId = subscription.Id,
                MentorProfileId = mentorProfile.Id,
                ClientProfileId = clientProfile.Id
            };
            dbContext.TrainingPlans.Add(trainingPlan);
        }

        trainingPlan.MotivationalQuote = motivationalQuote;
        trainingPlan.WeekNumber = weekNumber;
        trainingPlan.Status = status;

        if (trainingPlan.DayPlans.Count > 0)
        {
            dbContext.DayPlans.RemoveRange(trainingPlan.DayPlans);
            trainingPlan.DayPlans.Clear();
        }

        foreach (var day in days)
        {
            trainingPlan.DayPlans.Add(new DayPlan
            {
                DayOfWeek = day.DayOfWeek,
                TrainingDuration = TimeSpan.FromMinutes(day.TrainingDurationMinutes),
                TrainingDescription = day.TrainingDescription,
                NutritionDuration = TimeSpan.FromMinutes(day.NutritionDurationMinutes),
                NutritionDescription = day.NutritionDescription
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureProgressEntryAsync(
        ClientProfile clientProfile,
        int year,
        int month,
        decimal? weight,
        string? measurements,
        string? strength,
        string? conditioning,
        string? photoUrl,
        CancellationToken cancellationToken)
    {
        var entry = await dbContext.ProgressEntries
            .FirstOrDefaultAsync(
                x => x.ClientProfileId == clientProfile.Id &&
                    x.Year == year &&
                    x.Month == month,
                cancellationToken);

        if (entry is null)
        {
            entry = new ProgressEntry
            {
                ClientProfileId = clientProfile.Id,
                Year = year,
                Month = month
            };
            dbContext.ProgressEntries.Add(entry);
        }

        entry.Weight = weight;
        entry.Measurements = measurements;
        entry.Strength = strength;
        entry.Conditioning = conditioning;
        entry.PhotoUrl = photoUrl;
    }

    private async Task EnsureReviewAsync(
        ClientProfile clientProfile,
        MentorProfile mentorProfile,
        int rating,
        string comment,
        CancellationToken cancellationToken)
    {
        var review = await dbContext.Reviews
            .FirstOrDefaultAsync(
                x => x.ClientProfileId == clientProfile.Id && x.MentorProfileId == mentorProfile.Id,
                cancellationToken);

        if (review is null)
        {
            review = new Review
            {
                ClientProfileId = clientProfile.Id,
                MentorProfileId = mentorProfile.Id
            };
            dbContext.Reviews.Add(review);
        }

        review.Rating = rating;
        review.Comment = comment;
    }

    private void EnsureNotification(
        int userId,
        string title,
        string body,
        NotificationType type)
    {
        var notification = dbContext.Notifications
            .Local
            .FirstOrDefault(x => x.UserId == userId && x.Title == title)
            ?? dbContext.Notifications.FirstOrDefault(x => x.UserId == userId && x.Title == title);

        if (notification is null)
        {
            dbContext.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = title,
                Body = body,
                Type = type,
                IsRead = false
            });
            return;
        }

        notification.Body = body;
        notification.Type = type;
        notification.IsRead = false;
    }

    private sealed record QuestionnaireSeed(
        string PrimaryGoal,
        string TimeCommitment,
        string HealthIssues,
        string Medications,
        string WeeklyAvailability,
        string PhysicalActivityLevel);

    private sealed record DayPlanSeed(
        DayOfWeek DayOfWeek,
        int TrainingDurationMinutes,
        string TrainingDescription,
        int NutritionDurationMinutes,
        string NutritionDescription);
}

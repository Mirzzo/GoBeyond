using GoBeyond.Core.DTOs.Plans;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Plans;
using GoBeyond.Infrastructure.Services.Subscriptions;
using GoBeyond.Infrastructure.StateMachineServices.TrainingPlans;
using GoBeyond.Tests.Payments;
using GoBeyond.Tests.TestInfrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GoBeyond.Tests.Plans;

/// <summary>
/// B3: obavijesti o objavi/ažuriranju plana moraju biti rodno neutralne (bez "Mentor ... je objavio/ažurirao"),
/// a treninzi se ne mogu evidentirati kad saradnja više nije aktivna (otkazana pretplata ili obrisan mentor),
/// iako je plan i dalje formalno u statusu Published.
/// </summary>
public sealed class TrainingPlanServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly GoBeyondDbContext _db;
    private readonly RecordingNotificationSender _notifications = new();
    private readonly TrainingPlanService _service;

    public TrainingPlanServiceTests()
    {
        _connection.Open();
        _db = SqliteTestDbContext.Create(_connection);
        _db.Database.EnsureCreated();

        var stateFactory = new TrainingPlanStateFactory(
            [new DraftTrainingPlanState(), new PublishedTrainingPlanState(), new ArchivedTrainingPlanState()]);
        var workflow = new SubscriptionWorkflow(_notifications, new FakePaymentGateway(),
            Options.Create(new LifecycleOptions { SubscriptionPeriodDays = 30 }), NullLogger<SubscriptionWorkflow>.Instance);
        var lifecycle = Options.Create(new LifecycleOptions { PlanUpdateNotificationThrottleMinutes = 0 });
        _service = new TrainingPlanService(_db, stateFactory, workflow, _notifications, lifecycle);
    }

    public void Dispose() => _connection.Dispose();

    private (MentorProfile Mentor, ClientProfile Client, Subscription Subscription) SeedActiveCollaboration(string mentorFirst, string mentorLast)
    {
        var gender = new Gender { Name = "Žensko" };
        var type = new TrainingType { Name = "Weightlifting", Description = "Utezi" };
        var level = new FitnessLevel { Name = "Početnik", SortOrder = 1 };
        var goal = new FitnessGoal { Name = "Snaga" };

        var mentorUser = new User
        {
            FirstName = mentorFirst, LastName = mentorLast, Username = "mentor." + mentorLast.ToLowerInvariant(),
            Email = "mentor@test.ba", DateOfBirth = new DateOnly(1985, 1, 1), Gender = gender, PasswordHash = "x", Role = UserRole.Mentor
        };
        var mentor = new MentorProfile
        {
            User = mentorUser, TrainingType = type, Bio = new string('b', 60), YearsOfExperience = 5,
            MonthlyPrice = 20, Status = MentorApprovalStatus.Approved
        };

        var clientUser = new User
        {
            FirstName = "Klijent", LastName = "Test", Username = "client.test", Email = "client@test.ba",
            DateOfBirth = new DateOnly(1995, 1, 1), Gender = gender, PasswordHash = "x", Role = UserRole.Client
        };
        var client = new ClientProfile
        {
            User = clientUser, WeightKg = 80, HeightCm = 180, FitnessLevel = level, FitnessGoal = goal, TrainingExperienceYears = 1
        };

        var subscription = new Subscription
        {
            ClientProfile = client, MentorProfile = mentor, Status = SubscriptionStatus.Active,
            Price = 20, Currency = "usd", PaidAt = DateTime.UtcNow, AcceptedAt = DateTime.UtcNow,
            StartDate = DateTime.UtcNow, EndDate = DateTime.UtcNow.AddDays(30)
        };

        _db.AddRange(mentor, client, subscription);
        _db.SaveChanges();
        return (mentor, client, subscription);
    }

    private TrainingPlan SeedDraftPlanWithAllDaysFilled(MentorProfile mentor, ClientProfile client, Subscription subscription)
    {
        var plan = new TrainingPlan
        {
            Subscription = subscription, SubscriptionId = subscription.Id, MentorProfile = mentor, MentorProfileId = mentor.Id,
            ClientProfile = client, ClientProfileId = client.Id, Status = TrainingPlanStatus.Draft, Version = 1,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        for (var day = 1; day <= BaseTrainingPlanState.DaysInWeek; day++)
            plan.Days.Add(new DayPlan
            {
                DayOfWeek = day, TrainingDurationMinutes = 30,
                TrainingDescription = new string('t', 15), NutritionDescription = new string('n', 15)
            });

        _db.TrainingPlans.Add(plan);
        _db.SaveChanges();
        return plan;
    }

    [Fact]
    public async Task PublishAsync_ForFemaleMentor_UsesGenderNeutralWording()
    {
        var (mentor, client, subscription) = SeedActiveCollaboration("Selma", "Delić");
        var plan = SeedDraftPlanWithAllDaysFilled(mentor, client, subscription);

        await _service.PublishAsync(mentor.User.Id, plan.Id);

        var notification = Assert.Single(_notifications.Sent);
        Assert.Equal(client.User.Id, notification.UserId);
        Assert.DoesNotContain("je objavio", notification.Body);
        Assert.DoesNotContain("je objavila", notification.Body);
        Assert.Contains("je objavljen", notification.Body);
        Assert.Contains("Selma Delić", notification.Body);
    }

    [Fact]
    public async Task UpdateAsync_OnPublishedPlan_ForFemaleMentor_UsesGenderNeutralWording()
    {
        var (mentor, client, subscription) = SeedActiveCollaboration("Selma", "Delić");
        var plan = SeedDraftPlanWithAllDaysFilled(mentor, client, subscription);
        await _service.PublishAsync(mentor.User.Id, plan.Id);
        _notifications.Sent.Clear();

        await _service.UpdateAsync(mentor.User.Id, plan.Id, new UpdatePlanRequest { MotivationalQuote = "Nastavi jako!" });

        var notification = Assert.Single(_notifications.Sent);
        Assert.DoesNotContain("je ažurirao", notification.Body);
        Assert.DoesNotContain("je ažurirala", notification.Body);
        Assert.Contains("je ažuriran", notification.Body);
    }

    [Fact]
    public async Task LogSessionAsync_WhenSubscriptionCancelled_ThrowsInsteadOfLoggingSession()
    {
        var (mentor, client, subscription) = SeedActiveCollaboration("Amra", "Hadžić");
        var plan = SeedDraftPlanWithAllDaysFilled(mentor, client, subscription);
        await _service.PublishAsync(mentor.User.Id, plan.Id);

        subscription.Status = SubscriptionStatus.Cancelled;
        subscription.StatusReason = "Klijent je otkazao pretplatu.";
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.LogSessionAsync(client.User.Id, plan.Id, 1, new LogTrainingSessionRequest { Repetitions = 5 }));
        Assert.Empty(await _db.TrainingSessions.ToListAsync());
    }

    [Fact]
    public async Task LogSessionAsync_WhenMentorDeleted_ThrowsInsteadOfLoggingSession()
    {
        var (mentor, client, subscription) = SeedActiveCollaboration("Amra", "Hadžić");
        var plan = SeedDraftPlanWithAllDaysFilled(mentor, client, subscription);
        await _service.PublishAsync(mentor.User.Id, plan.Id);

        subscription.Status = SubscriptionStatus.Cancelled;
        subscription.StatusReason = "Mentor je uklonjen sa platforme.";
        mentor.User.IsDeleted = true;
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.LogSessionAsync(client.User.Id, plan.Id, 1, new LogTrainingSessionRequest { Repetitions = 5 }));
    }

    [Fact]
    public async Task LogSessionAsync_WhenSubscriptionStillActive_Succeeds()
    {
        var (mentor, client, subscription) = SeedActiveCollaboration("Amra", "Hadžić");
        var plan = SeedDraftPlanWithAllDaysFilled(mentor, client, subscription);
        await _service.PublishAsync(mentor.User.Id, plan.Id);

        var result = await _service.LogSessionAsync(client.User.Id, plan.Id, 1, new LogTrainingSessionRequest { Repetitions = 5 });

        Assert.Equal(5, result.Repetitions);
        Assert.Single(await _db.TrainingSessions.ToListAsync());
    }
}

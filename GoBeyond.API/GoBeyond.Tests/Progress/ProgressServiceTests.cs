using GoBeyond.Core.DTOs.Progress;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Files;
using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Files;
using GoBeyond.Infrastructure.Services.Plans;
using GoBeyond.Infrastructure.Services.Progress;
using GoBeyond.Infrastructure.Services.Subscriptions;
using GoBeyond.Infrastructure.StateMachineServices.TrainingPlans;
using GoBeyond.Tests.Payments;
using GoBeyond.Tests.TestInfrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GoBeyond.Tests.Progress;

/// <summary>
/// B3 (PRG-04): "HISTORIJA PLANA" snapshot se prilaže samo unosu napretka za TEKUĆI mjesec - za prošle
/// mjesece trenutni plan možda tada nije ni postojao, pa se ne smije lažno prikazati kao historija.
/// </summary>
public sealed class ProgressServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly GoBeyondDbContext _db;
    private readonly ProgressService _service;

    public ProgressServiceTests()
    {
        _connection.Open();
        _db = SqliteTestDbContext.Create(_connection);
        _db.Database.EnsureCreated();

        var stateFactory = new TrainingPlanStateFactory(
            [new DraftTrainingPlanState(), new PublishedTrainingPlanState(), new ArchivedTrainingPlanState()]);
        var workflow = new SubscriptionWorkflow(new RecordingNotificationSender(), new FakePaymentGateway(),
            Options.Create(new LifecycleOptions { SubscriptionPeriodDays = 30 }), NullLogger<SubscriptionWorkflow>.Instance);
        var plans = new TrainingPlanService(_db, stateFactory, workflow, new RecordingNotificationSender(),
            Options.Create(new LifecycleOptions()));
        _service = new ProgressService(_db, plans, new UnusedFileStorageService());
    }

    public void Dispose() => _connection.Dispose();

    private ClientProfile SeedActiveClientWithPublishedPlan()
    {
        var gender = new Gender { Name = "Muško" };
        var type = new TrainingType { Name = "Weightlifting", Description = "Utezi" };
        var level = new FitnessLevel { Name = "Početnik", SortOrder = 1 };
        var goal = new FitnessGoal { Name = "Snaga" };

        var mentorUser = new User
        {
            FirstName = "Mia", LastName = "Mentor", Username = "mentor.test", Email = "mentor@test.ba",
            DateOfBirth = new DateOnly(1985, 1, 1), Gender = gender, PasswordHash = "x", Role = UserRole.Mentor
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

        var plan = new TrainingPlan
        {
            Subscription = subscription, MentorProfile = mentor, ClientProfile = client,
            Status = TrainingPlanStatus.Published, Version = 1, PublishedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        _db.AddRange(mentor, client, subscription, plan);
        _db.SaveChanges();
        return client;
    }

    private static UpsertProgressRequest ValidRequest() => new()
    {
        WeightKg = 80, Measurements = "grudi 100, struk 80", Strength = "bench 80kg", Conditioning = "5km trčanje"
    };

    [Fact]
    public async Task UpsertAsync_ForCurrentMonth_AttachesPlanSnapshot()
    {
        var client = SeedActiveClientWithPublishedPlan();
        var now = DateTime.UtcNow;

        var result = await _service.UpsertAsync(client.User.Id, now.Year, now.Month, ValidRequest());

        Assert.True(result.HasPlanSnapshot);
        var stored = await _db.ProgressEntries.SingleAsync();
        Assert.NotNull(stored.TrainingPlanId);
        Assert.False(string.IsNullOrEmpty(stored.PlanSnapshotJson));
    }

    [Fact]
    public async Task UpsertAsync_ForPastMonth_DoesNotAttachPlanThatDidNotExistThen()
    {
        var client = SeedActiveClientWithPublishedPlan();
        var pastMonth = DateTime.UtcNow.AddMonths(-6);

        var result = await _service.UpsertAsync(client.User.Id, pastMonth.Year, pastMonth.Month, ValidRequest());

        Assert.False(result.HasPlanSnapshot);
        var stored = await _db.ProgressEntries.SingleAsync();
        Assert.Null(stored.TrainingPlanId);
        Assert.Null(stored.PlanSnapshotJson);
    }

    /// <summary>ProgressService.UpsertAsync ne koristi IFileStorageService; svaki poziv na njega bi značio grešku u testu.</summary>
    private sealed class UnusedFileStorageService : IFileStorageService
    {
        public Task<string> SavePublicAsync(FileUpload file, string category, UploadKind kind, string fieldName,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<string> SavePrivateAsync(FileUpload file, string category, UploadKind kind, string fieldName,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public void Validate(FileUpload file, UploadKind kind, string fieldName) => throw new NotSupportedException();

        public string? ResolvePrivatePath(string location) => throw new NotSupportedException();

        public void Delete(string? location) => throw new NotSupportedException();
    }
}

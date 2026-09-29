using GoBeyond.Core.DTOs.Progress;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
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
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GoBeyond.Tests.Progress;

/// <summary>
/// Tekući mjesec za napredak se računa u vremenskoj zoni platforme (Europe/Sarajevo), kao u aplikaciji: prvog u mjesecu
/// poslije lokalne ponoći novi mjesec nije "budući", iako je po UTC-u još prethodni dan.
/// </summary>
public sealed class ProgressTimeZoneTests : IDisposable
{
    /// <summary>30.09.2026. 22:30 UTC = 01.10.2026. 00:30 u Sarajevu (ljetno vrijeme, UTC+2).</summary>
    private static readonly DateTimeOffset FirstOfOctoberAfterLocalMidnight = new(2026, 9, 30, 22, 30, 0, TimeSpan.Zero);

    /// <summary>31.12.2026. 23:30 UTC = 01.01.2027. 00:30 u Sarajevu (zimsko vrijeme, UTC+1).</summary>
    private static readonly DateTimeOffset NewYearAfterLocalMidnight = new(2026, 12, 31, 23, 30, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly GoBeyondDbContext _db;
    private readonly TrainingPlanService _plans;
    private readonly User _client;

    public ProgressTimeZoneTests()
    {
        _connection.Open();
        _db = SqliteTestDbContext.Create(_connection);
        _db.Database.EnsureCreated();

        var stateFactory = new TrainingPlanStateFactory(
            [new DraftTrainingPlanState(), new PublishedTrainingPlanState(), new ArchivedTrainingPlanState()]);
        var workflow = new SubscriptionWorkflow(new RecordingNotificationSender(), new FakePaymentGateway(),
            Options.Create(new LifecycleOptions { SubscriptionPeriodDays = 30 }), NullLogger<SubscriptionWorkflow>.Instance);
        _plans = new TrainingPlanService(_db, stateFactory, workflow, new RecordingNotificationSender(), Options.Create(new LifecycleOptions()));
        _client = SeedClientWithPublishedPlan();
    }

    public void Dispose() => _connection.Dispose();

    private ProgressService Service(DateTimeOffset utcNow) =>
        new(_db, _plans, new UnusedFileStorageService(), Options.Create(new LifecycleOptions()), new FixedClock(utcNow));

    private User SeedClientWithPublishedPlan()
    {
        var gender = new Gender { Name = "Žensko" };
        var mentor = new MentorProfile
        {
            User = new User
            {
                FirstName = "Selma", LastName = "Delić", Username = "selma", Email = "selma@test.ba",
                DateOfBirth = new DateOnly(1988, 1, 1), Gender = gender, PasswordHash = "x", Role = UserRole.Mentor
            },
            TrainingType = new TrainingType { Name = "Weightlifting", Description = "Utezi" }, Bio = new string('b', 60),
            YearsOfExperience = 5, MonthlyPrice = 20, Status = MentorApprovalStatus.Approved
        };
        var client = new ClientProfile
        {
            User = new User
            {
                FirstName = "Nađa", LastName = "Škrijelj", Username = "nadja", Email = "nadja@test.ba",
                DateOfBirth = new DateOnly(1995, 1, 1), Gender = gender, PasswordHash = "x", Role = UserRole.Client
            },
            WeightKg = 60, HeightCm = 170, FitnessLevel = new FitnessLevel { Name = "Početnik", SortOrder = 1 },
            FitnessGoal = new FitnessGoal { Name = "Snaga" }, TrainingExperienceYears = 1
        };
        var subscription = new Subscription
        {
            ClientProfile = client, MentorProfile = mentor, Status = SubscriptionStatus.Active, Price = 20, Currency = "usd",
            PaidAt = new DateTime(2026, 9, 10, 8, 0, 0, DateTimeKind.Utc), AcceptedAt = new DateTime(2026, 9, 10, 9, 0, 0, DateTimeKind.Utc),
            StartDate = new DateTime(2026, 9, 10, 9, 0, 0, DateTimeKind.Utc), EndDate = new DateTime(2027, 1, 20, 9, 0, 0, DateTimeKind.Utc)
        };
        var plan = new TrainingPlan
        {
            Subscription = subscription, MentorProfile = mentor, ClientProfile = client, Status = TrainingPlanStatus.Published,
            Version = 1, PublishedAt = new DateTime(2026, 9, 11, 9, 0, 0, DateTimeKind.Utc),
            CreatedAt = new DateTime(2026, 9, 10, 9, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2026, 9, 11, 9, 0, 0, DateTimeKind.Utc)
        };
        _db.AddRange(mentor, client, subscription, plan);
        _db.SaveChanges();
        return client.User;
    }

    private static UpsertProgressRequest Request() => new()
    {
        WeightKg = 60, Measurements = "struk 70", Strength = "čučanj 60kg", Conditioning = "5km trčanje"
    };

    [Fact]
    public async Task UpsertAsync_OnTheFirstOfTheMonthAfterLocalMidnight_AcceptsTheNewMonthWithThePlanSnapshot()
    {
        var entry = await Service(FirstOfOctoberAfterLocalMidnight).UpsertAsync(_client.Id, 2026, 10, Request());

        Assert.Equal(10, entry.Month);
        Assert.True(entry.HasPlanSnapshot);
    }

    [Fact]
    public async Task UpsertAsync_AfterLocalMidnight_StillRejectsTheFollowingMonth()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            Service(FirstOfOctoberAfterLocalMidnight).UpsertAsync(_client.Id, 2026, 11, Request()));

        Assert.Equal("Nije moguće unijeti napredak za budući mjesec.", error.Message);
    }

    [Fact]
    public async Task UpsertAsync_BeforeLocalMidnight_RejectsTheNextMonth()
    {
        // 30.09.2026. 23:30 u Sarajevu: oktobar još nije počeo.
        var beforeMidnight = FirstOfOctoberAfterLocalMidnight.AddHours(-1);

        await Assert.ThrowsAsync<ValidationException>(() => Service(beforeMidnight).UpsertAsync(_client.Id, 2026, 10, Request()));
    }

    [Fact]
    public async Task OnNewYearsDayAfterLocalMidnight_TheNewYearIsCurrent()
    {
        var service = Service(NewYearAfterLocalMidnight);

        var entry = await service.UpsertAsync(_client.Id, 2027, 1, Request());
        var years = await service.GetYearsAsync(_client.Id);
        var defaultYear = await service.GetByYearAsync(_client.Id, year: null);

        Assert.True(entry.HasPlanSnapshot);
        Assert.Equal(2027, years[0]);
        Assert.Equal(2027, Assert.Single(defaultYear).Year);
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    /// <summary>Unos napretka ne koristi IFileStorageService; svaki poziv na njega bi značio grešku u testu.</summary>
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

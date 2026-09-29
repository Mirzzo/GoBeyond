using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Messages;
using GoBeyond.Tests.Payments;
using GoBeyond.Tests.TestInfrastructure;
using Microsoft.Data.Sqlite;

namespace GoBeyond.Tests.Messages;

/// <summary>
/// B3 (NOT-06): mentor ne smije vidjeti zahtjev koji klijent nikad nije platio, ni kad je u međuvremenu
/// otkazan (PendingPayment → Cancelled bez PaidAt), ni u listi razgovora ni pri direktnom pristupu nitima.
/// </summary>
public sealed class MessageServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly GoBeyondDbContext _db;
    private readonly MessageService _service;

    public MessageServiceTests()
    {
        _connection.Open();
        _db = SqliteTestDbContext.Create(_connection);
        _db.Database.EnsureCreated();
        _service = new MessageService(_db, new RecordingNotificationSender());
    }

    public void Dispose() => _connection.Dispose();

    private (MentorProfile Mentor, ClientProfile Client) SeedMentorAndClient(string clientFirst, string clientLast)
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
            FirstName = clientFirst, LastName = clientLast, Username = "client." + clientLast.ToLowerInvariant(),
            Email = clientLast.ToLowerInvariant() + "@test.ba", DateOfBirth = new DateOnly(1995, 1, 1),
            Gender = gender, PasswordHash = "x", Role = UserRole.Client
        };
        var client = new ClientProfile
        {
            User = clientUser, WeightKg = 80, HeightCm = 180, FitnessLevel = level, FitnessGoal = goal, TrainingExperienceYears = 1
        };

        _db.AddRange(mentor, client);
        _db.SaveChanges();
        return (mentor, client);
    }

    private Subscription AddSubscription(MentorProfile mentor, ClientProfile client, SubscriptionStatus status, DateTime? paidAt)
    {
        var subscription = new Subscription
        {
            ClientProfile = client, MentorProfile = mentor, Status = status, Price = 20, Currency = "usd",
            PaidAt = paidAt, CreatedAt = DateTime.UtcNow
        };
        _db.Subscriptions.Add(subscription);
        _db.SaveChanges();
        return subscription;
    }

    [Fact]
    public async Task GetThreadsAsync_ForMentor_ExcludesNeverPaidCancelledSubscription()
    {
        var (mentor, client) = SeedMentorAndClient("Žana", "Ćosić");
        AddSubscription(mentor, client, SubscriptionStatus.Cancelled, paidAt: null);

        var threads = await _service.GetThreadsAsync(mentor.User.Id, UserRole.Mentor, null);

        Assert.Empty(threads);
    }

    [Fact]
    public async Task GetThreadAsync_ForMentor_OnNeverPaidCancelledSubscription_ThrowsNotFound()
    {
        var (mentor, client) = SeedMentorAndClient("Tarik", "Džeko");
        var subscription = AddSubscription(mentor, client, SubscriptionStatus.Cancelled, paidAt: null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetThreadAsync(mentor.User.Id, UserRole.Mentor, subscription.Id));
    }

    [Fact]
    public async Task GetThreadsAsync_ForMentor_StillIncludesPaidCancelledSubscription()
    {
        var (mentor, client) = SeedMentorAndClient("Nedim", "Ćesko");
        AddSubscription(mentor, client, SubscriptionStatus.Cancelled, paidAt: DateTime.UtcNow.AddDays(-10));

        var threads = await _service.GetThreadsAsync(mentor.User.Id, UserRole.Mentor, null);

        Assert.Single(threads);
    }
}

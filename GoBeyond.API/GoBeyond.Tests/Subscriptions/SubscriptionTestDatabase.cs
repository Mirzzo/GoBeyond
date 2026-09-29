using System.Data.Common;
using GoBeyond.Core.DTOs.Subscriptions;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Mentors;
using GoBeyond.Infrastructure.Services.Notifications;
using GoBeyond.Infrastructure.Services.Payments;
using GoBeyond.Infrastructure.Services.Plans;
using GoBeyond.Infrastructure.Services.Subscriptions;
using GoBeyond.Infrastructure.StateMachineServices.TrainingPlans;
using GoBeyond.Tests.Payments;
using GoBeyond.Tests.TestInfrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GoBeyond.Tests.Subscriptions;

/// <summary>
/// SQLite baza u privremenom fajlu: svaki DbContext dobija svoju konekciju, pa se istovremene operacije ponašaju kao na
/// pravoj bazi. In-memory baza sa jednom dijeljenom konekcijom to ne može. Ponašanje je kao SQL Server sa
/// READ_COMMITTED_SNAPSHOT (runtime): WAL mod i odložene transakcije (BEGIN), pa čitanje ne čeka i vidi zadnje potvrđeno
/// stanje, a upis čeka drugi upis. Podrazumijevani BEGIN IMMEDIATE bi zaključao cijelu bazu na početku svake transakcije
/// i sakrio nedostajuće zaključavanje pretplate (SubscriptionLocks). Sadrži mentora, drugog mentora, klijenta i drugog
/// klijenta, te fabrike servisa.
/// </summary>
internal sealed class SubscriptionTestDatabase : IDisposable
{
    public const decimal Price = 29.99m;

    private readonly string _path = Path.Combine(Path.GetTempPath(), $"gobeyond-tests-{Guid.NewGuid():N}.db");

    public SubscriptionTestDatabase()
    {
        using var db = CreateContext();
        db.Database.EnsureCreated();
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
        Seed(db);
    }

    public FakePaymentGateway Gateway { get; } = new();

    public LifecycleOptions Lifecycle { get; } = new()
    {
        SubscriptionPeriodDays = 30, ExpiringReminderDays = 3, PlanMissingAfterHours = 48, PlanMissingRepeatHours = 24,
        InactivityDays = 7, InactivityRepeatDays = 7, PaymentReconcileAfterMinutes = 5, PaymentReconcileWindowHours = 48
    };

    public User MentorUser { get; private set; } = null!;
    public User SecondMentorUser { get; private set; } = null!;
    public User ClientUser { get; private set; } = null!;
    public User SecondClientUser { get; private set; } = null!;
    public int MentorProfileId { get; private set; }
    public int SecondMentorProfileId { get; private set; }
    public int ClientProfileId { get; private set; }
    public int SecondClientProfileId { get; private set; }

    public GoBeyondDbContext CreateContext(params IInterceptor[] interceptors) =>
        new SqliteTestDbContext(new DbContextOptionsBuilder<GoBeyondDbContext>()
            .UseSqlite($"Data Source={_path};Pooling=False;Default Timeout=30")
            .AddInterceptors([DeferredTransactions.Instance, .. interceptors])
            .Options);

    public SubscriptionWorkflow Workflow(GoBeyondDbContext db) =>
        new(new NotificationSender(db), Gateway, Options.Create(Lifecycle), NullLogger<SubscriptionWorkflow>.Instance);

    public SubscriptionService Subscriptions(GoBeyondDbContext db) => new(db, Workflow(db), Gateway);

    public PaymentService Payments(GoBeyondDbContext db) =>
        new(db, Gateway, Workflow(db), Subscriptions(db), Options.Create(Lifecycle), NullLogger<PaymentService>.Instance);

    public CollaborationService Collaboration(GoBeyondDbContext db) => new(db, Workflow(db));

    public TrainingPlanService Plans(GoBeyondDbContext db) => new(db,
        new TrainingPlanStateFactory([new DraftTrainingPlanState(), new PublishedTrainingPlanState(), new ArchivedTrainingPlanState()]),
        Workflow(db), new NotificationSender(db), Options.Create(Lifecycle));

    public SubscriptionLifecycleProcessor LifecycleProcessor(GoBeyondDbContext db) =>
        new(db, Workflow(db), new NotificationSender(db), Payments(db), Options.Create(Lifecycle),
            NullLogger<SubscriptionLifecycleProcessor>.Instance);

    /// <summary>Pokreće operaciju nad novim DbContext-om (svaki servis ima svoju konekciju, kao zaseban HTTP zahtjev).</summary>
    public async Task<T> RunAsync<T>(Func<GoBeyondDbContext, Task<T>> action)
    {
        await using var db = CreateContext();
        return await action(db);
    }

    public async Task RunAsync(Func<GoBeyondDbContext, Task> action)
    {
        await using var db = CreateContext();
        await action(db);
    }

    /// <summary>Dodaje pretplatu klijenta kod mentora (podrazumijevano prvi klijent i prvi mentor) i vraća njen Id.</summary>
    public async Task<int> AddSubscriptionAsync(SubscriptionStatus status, Action<Subscription>? configure = null,
        int? clientProfileId = null, int? mentorProfileId = null)
    {
        await using var db = CreateContext();
        var now = DateTime.UtcNow;
        var subscription = new Subscription
        {
            ClientProfileId = clientProfileId ?? ClientProfileId,
            MentorProfileId = mentorProfileId ?? MentorProfileId,
            Status = status,
            Price = Price,
            Currency = "usd",
            CreatedAt = now.AddHours(-1),
            Questionnaire = NewQuestionnaire()
        };
        if (status != SubscriptionStatus.PendingPayment) subscription.PaidAt = now.AddMinutes(-50);
        if (status is SubscriptionStatus.Active or SubscriptionStatus.Expired)
        {
            subscription.AcceptedAt = subscription.StartDate = now.AddDays(-20);
            subscription.EndDate = now.AddDays(10);
        }
        configure?.Invoke(subscription);
        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync();
        return subscription.Id;
    }

    /// <summary>Dodaje uplatu (i odgovarajući PaymentIntent u lažni Stripe) i vraća Id uplate.</summary>
    public async Task<int> AddPaymentAsync(int subscriptionId, string intentId, PaymentStatus status, string stripeStatus,
        PaymentPurpose purpose = PaymentPurpose.Initial, decimal amount = Price, DateTime? createdAt = null)
    {
        await using var db = CreateContext();
        var payment = new Payment
        {
            SubscriptionId = subscriptionId, Amount = amount, Currency = "usd", StripePaymentIntentId = intentId, Purpose = purpose,
            Status = status, CreatedAt = createdAt ?? DateTime.UtcNow.AddMinutes(-2),
            PaidAt = status is PaymentStatus.Succeeded or PaymentStatus.Refunded or PaymentStatus.RefundPending ? DateTime.UtcNow.AddMinutes(-50) : null
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        Gateway.Intents[intentId] = FakePaymentGateway.Intent(intentId, stripeStatus, subscriptionId, purpose, amount);
        return payment.Id;
    }

    public async Task<Subscription> SubscriptionAsync(int id)
    {
        await using var db = CreateContext();
        return await db.Subscriptions.AsNoTracking().Include(x => x.Payments).SingleAsync(x => x.Id == id);
    }

    public async Task<List<Notification>> NotificationsAsync(int userId)
    {
        await using var db = CreateContext();
        return await db.Notifications.AsNoTracking().Where(x => x.UserId == userId).OrderBy(x => x.Id).ToListAsync();
    }

    public static QuestionnaireRequest QuestionnaireRequest(string primaryGoal = "Snaga i kondicija") => new()
    {
        PrimaryGoal = primaryGoal, TimeCommitment = "3 sata sedmično", HealthIssues = "Nema", Medications = "Nema",
        WeeklySessions = "3", OutsideActivity = "Šetnja"
    };

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var file in new[] { _path, _path + "-wal", _path + "-shm" })
        {
            try
            {
                File.Delete(file);
            }
            catch (IOException)
            {
                // privremeni fajl - ako je još zaključan, ostaje u temp folderu
            }
        }
    }

    /// <summary>Transakcija počinje sa BEGIN (odloženo), kao na SQL Server-u: ništa se ne zaključava dok se ne upisuje.</summary>
    private sealed class DeferredTransactions : DbTransactionInterceptor
    {
        public static readonly DeferredTransactions Instance = new();

        public override InterceptionResult<DbTransaction> TransactionStarting(DbConnection connection,
            TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result) =>
            InterceptionResult<DbTransaction>.SuppressWithResult(((SqliteConnection)connection).BeginTransaction(deferred: true));

        public override ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(DbConnection connection,
            TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(TransactionStarting(connection, eventData, result));
    }

    private static Questionnaire NewQuestionnaire() => new()
    {
        PrimaryGoal = "Snaga", TimeCommitment = "3 sata", HealthIssues = "Nema", Medications = "Nema", WeeklySessions = "3",
        OutsideActivity = "Šetnja"
    };

    private void Seed(GoBeyondDbContext db)
    {
        var gender = new Gender { Name = "Žensko" };
        var type = new TrainingType { Name = "Weightlifting", Description = "Utezi" };
        var level = new FitnessLevel { Name = "Početnik", SortOrder = 1 };
        var goal = new FitnessGoal { Name = "Snaga" };

        User NewUser(string first, string last, string username, UserRole role) => new()
        {
            FirstName = first, LastName = last, Username = username, Email = $"{username}@test.ba",
            DateOfBirth = new DateOnly(1990, 1, 1), Gender = gender, PasswordHash = "x", Role = role
        };

        MentorProfile NewMentor(User user) => new()
        {
            User = user, TrainingType = type, Bio = new string('b', 60), YearsOfExperience = 5, MonthlyPrice = Price,
            Status = MentorApprovalStatus.Approved
        };

        ClientProfile NewClient(User user) => new()
        {
            User = user, WeightKg = 60, HeightCm = 170, FitnessLevel = level, FitnessGoal = goal, TrainingExperienceYears = 1
        };

        var mentor = NewMentor(NewUser("Selma", "Delić", "selma", UserRole.Mentor));
        var secondMentor = NewMentor(NewUser("Lejla", "Mujić", "lejla", UserRole.Mentor));
        var client = NewClient(NewUser("Nađa", "Škrijelj", "nadja", UserRole.Client));
        var secondClient = NewClient(NewUser("Hana", "Kurić", "hana", UserRole.Client));
        db.AddRange(mentor, secondMentor, client, secondClient);
        db.SaveChanges();

        (MentorUser, SecondMentorUser, ClientUser, SecondClientUser) = (mentor.User, secondMentor.User, client.User, secondClient.User);
        (MentorProfileId, SecondMentorProfileId, ClientProfileId, SecondClientProfileId) = (mentor.Id, secondMentor.Id, client.Id, secondClient.Id);
    }
}

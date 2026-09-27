using GoBeyond.Core.DTOs.Subscriptions;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Payments;
using GoBeyond.Infrastructure.Services.Subscriptions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using GoBeyond.Tests.TestInfrastructure;

namespace GoBeyond.Tests.Payments;

/// <summary>#2 (plaćanje u obradi) i #4 (provjera PaymentIntent metadata/iznosa/valute) nad pravom (SQLite) bazom.</summary>
public sealed class PaymentServiceTests : IDisposable
{
    private const decimal Price = 29.99m;
    private const int ClientUserId = 2;

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly FakePaymentGateway _gateway = new();
    private readonly int _subscriptionId;
    private readonly int _paymentId;

    public PaymentServiceTests()
    {
        _connection.Open();
        using var db = CreateContext();
        db.Database.EnsureCreated();
        (_subscriptionId, _paymentId) = Seed(db);
        _gateway.Intents["pi_test_1"] = FakePaymentGateway.Intent("pi_test_1", "requires_payment_method", _subscriptionId, PaymentPurpose.Initial, Price);
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task CreateIntent_WhilePreviousIntentIsProcessing_Returns409AndDoesNotCreateAnotherIntent()
    {
        SetIntentStatus("pi_test_1", "processing");

        var error = await Assert.ThrowsAsync<ConflictException>(() => CreateIntentAsync());

        Assert.Equal(PaymentService.StillProcessing, error.Message);
        Assert.Empty(_gateway.CreateCalls);
        await using var db = CreateContext();
        var payment = Assert.Single(db.Payments);
        Assert.Equal(PaymentStatus.Pending, payment.Status);
    }

    [Fact]
    public async Task CreateIntent_ReusesIntentThatStillAwaitsPaymentMethod()
    {
        var result = await CreateIntentAsync();

        Assert.Equal(_paymentId, result.PaymentId);
        Assert.Equal("pi_test_1_secret", result.ClientSecret);
        Assert.Empty(_gateway.CreateCalls);
    }

    [Fact]
    public async Task CreateIntent_AfterCanceledIntent_FailsOldPaymentAndCreatesNewOneWithIdempotencyKeyAndMetadata()
    {
        SetIntentStatus("pi_test_1", "canceled");

        var result = await CreateIntentAsync();

        Assert.NotEqual(_paymentId, result.PaymentId);
        var call = Assert.Single(_gateway.CreateCalls);
        Assert.Equal($"create-intent:{_subscriptionId}:Initial:2:2999", call.IdempotencyKey);
        Assert.Equal(_subscriptionId.ToString(), call.Metadata[PaymentIntentInfo.MetadataSubscriptionId]);
        Assert.Equal("Initial", call.Metadata[PaymentIntentInfo.MetadataPurpose]);
        await using var db = CreateContext();
        Assert.Equal(PaymentStatus.Failed, (await db.Payments.SingleAsync(x => x.Id == _paymentId)).Status);
    }

    [Fact]
    public async Task CreateIntent_WhenPreviousIntentAlreadySucceeded_AppliesItAndReturns409()
    {
        SetIntentStatus("pi_test_1", "succeeded");

        var error = await Assert.ThrowsAsync<ConflictException>(() => CreateIntentAsync());

        Assert.Equal(PaymentService.AlreadyPaid, error.Message);
        Assert.Empty(_gateway.CreateCalls);
        await using var db = CreateContext();
        Assert.Equal(SubscriptionStatus.AwaitingMentor, (await db.Subscriptions.SingleAsync()).Status);
    }

    [Fact]
    public async Task Confirm_WithMatchingIntent_AppliesPaymentOnce()
    {
        SetIntentStatus("pi_test_1", "succeeded");

        var first = await ConfirmAsync();
        var second = await ConfirmAsync();

        Assert.Equal(SubscriptionStatus.AwaitingMentor, first.Status);
        Assert.Equal(SubscriptionStatus.AwaitingMentor, second.Status);
        await using var db = CreateContext();
        Assert.Equal(PaymentStatus.Succeeded, (await db.Payments.SingleAsync()).Status);
    }

    public static TheoryData<string, PaymentPurpose, decimal, string> Mismatches => new()
    {
        { "999", PaymentPurpose.Initial, Price, "usd" },    // druga pretplata u metadata
        { "SELF", PaymentPurpose.Renewal, Price, "usd" },   // pogrešna namjena
        { "SELF", PaymentPurpose.Initial, 1.00m, "usd" },   // pogrešan iznos
        { "SELF", PaymentPurpose.Initial, Price, "eur" }    // pogrešna valuta
    };

    [Theory]
    [MemberData(nameof(Mismatches))]
    public async Task Confirm_WithMismatchedIntent_IsRejectedAndNotApplied(string subscriptionId, PaymentPurpose purpose, decimal amount, string currency)
    {
        var metadataSubscription = subscriptionId == "SELF" ? _subscriptionId : int.Parse(subscriptionId);
        _gateway.Intents["pi_test_1"] = FakePaymentGateway.Intent("pi_test_1", "succeeded", metadataSubscription, purpose, amount, currency);

        var error = await Assert.ThrowsAsync<ValidationException>(ConfirmAsync);

        Assert.Equal(PaymentService.IntentMismatch, error.Message);
        await using var db = CreateContext();
        Assert.Equal(PaymentStatus.Pending, (await db.Payments.SingleAsync()).Status);
        Assert.Equal(SubscriptionStatus.PendingPayment, (await db.Subscriptions.SingleAsync()).Status);
    }

    [Fact]
    public async Task Webhook_WithMismatchedMetadata_IsRejected()
    {
        var payload = WebhookPayload("payment_intent.succeeded", "pi_test_1", subscriptionId: 12345, "Initial", 2999, "usd");

        await Assert.ThrowsAsync<ValidationException>(() => WithService(s => s.HandleWebhookAsync(payload, "t=1,v1=x")));

        await using var db = CreateContext();
        Assert.Equal(PaymentStatus.Pending, (await db.Payments.SingleAsync()).Status);
    }

    [Fact]
    public async Task Webhook_WithMatchingIntent_AppliesPayment()
    {
        var payload = WebhookPayload("payment_intent.succeeded", "pi_test_1", _subscriptionId, "Initial", 2999, "usd");

        await WithService(s => s.HandleWebhookAsync(payload, "t=1,v1=x"));

        await using var db = CreateContext();
        Assert.Equal(PaymentStatus.Succeeded, (await db.Payments.SingleAsync()).Status);
        Assert.Equal(SubscriptionStatus.AwaitingMentor, (await db.Subscriptions.SingleAsync()).Status);
    }

    [Fact]
    public async Task Webhook_WithInvalidSignature_IsRejected()
    {
        _gateway.WebhookSignatureValid = false;
        var payload = WebhookPayload("payment_intent.succeeded", "pi_test_1", _subscriptionId, "Initial", 2999, "usd");

        await Assert.ThrowsAsync<ValidationException>(() => WithService(s => s.HandleWebhookAsync(payload, "")));
    }

    [Fact]
    public async Task Confirm_ForSubscriptionCancelledMeanwhile_RefundsPayment()
    {
        await using (var db = CreateContext())
        {
            var subscription = await db.Subscriptions.SingleAsync();
            subscription.Status = SubscriptionStatus.Cancelled;
            await db.SaveChangesAsync();
        }
        SetIntentStatus("pi_test_1", "succeeded");

        var result = await ConfirmAsync();

        Assert.Equal(SubscriptionStatus.Cancelled, result.Status);
        Assert.Equal([("pi_test_1", $"refund:{_paymentId}")], _gateway.Refunds);
        await using var check = CreateContext();
        Assert.Equal(PaymentStatus.Refunded, (await check.Payments.SingleAsync()).Status);
        Assert.Contains(check.Notifications, x => x.Type == NotificationType.PaymentRefunded);
    }

    private Task<PaymentIntentDto> CreateIntentAsync() =>
        WithService(s => s.CreateIntentAsync(ClientUserId, new CreatePaymentIntentRequest { SubscriptionId = _subscriptionId }));

    private Task<SubscriptionDetailDto> ConfirmAsync() => WithService(s => s.ConfirmAsync(ClientUserId, _paymentId));

    private async Task<T> WithService<T>(Func<PaymentService, Task<T>> action)
    {
        await using var db = CreateContext();
        return await action(CreateService(db));
    }

    private async Task WithService(Func<PaymentService, Task> action)
    {
        await using var db = CreateContext();
        await action(CreateService(db));
    }

    private PaymentService CreateService(GoBeyondDbContext db)
    {
        var workflow = new SubscriptionWorkflow(new Infrastructure.Services.Notifications.NotificationSender(db), _gateway,
            Options.Create(new LifecycleOptions { SubscriptionPeriodDays = 30 }), NullLogger<SubscriptionWorkflow>.Instance);
        var subscriptions = new SubscriptionService(db, workflow, _gateway);
        return new PaymentService(db, _gateway, workflow, subscriptions, NullLogger<PaymentService>.Instance);
    }

    private void SetIntentStatus(string id, string status) => _gateway.Intents[id] = _gateway.Intents[id] with { Status = status };

    private static string WebhookPayload(string type, string intentId, int subscriptionId, string purpose, long amount, string currency) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            type,
            data = new
            {
                @object = new
                {
                    id = intentId, @object = "payment_intent", status = "succeeded", amount, currency,
                    metadata = new Dictionary<string, string> { ["subscriptionId"] = subscriptionId.ToString(), ["purpose"] = purpose }
                }
            }
        });

    private GoBeyondDbContext CreateContext() => SqliteTestDbContext.Create(_connection);

    private static (int SubscriptionId, int PaymentId) Seed(GoBeyondDbContext db)
    {
        var gender = new Gender { Name = "Muško" };
        var type = new TrainingType { Name = "Weightlifting", Description = "Utezi" };
        var level = new FitnessLevel { Name = "Početnik", SortOrder = 1 };
        var goal = new FitnessGoal { Name = "Snaga" };
        var mentorUser = new User
        {
            Id = 1, FirstName = "Haris", LastName = "Mehmedović", Username = "mentor", Email = "mentor@gobeyond.ba",
            DateOfBirth = new DateOnly(1990, 1, 1), Gender = gender, PasswordHash = "x", Role = UserRole.Mentor
        };
        var clientUser = new User
        {
            Id = ClientUserId, FirstName = "Tarik", LastName = "Hadžić", Username = "client", Email = "client@gobeyond.ba",
            DateOfBirth = new DateOnly(1999, 1, 1), Gender = gender, PasswordHash = "x", Role = UserRole.Client
        };
        var mentor = new MentorProfile
        {
            User = mentorUser, TrainingType = type, Bio = new string('b', 60), YearsOfExperience = 5,
            MonthlyPrice = Price, Status = MentorApprovalStatus.Approved
        };
        var client = new ClientProfile
        {
            User = clientUser, WeightKg = 80, HeightCm = 180, FitnessLevel = level, FitnessGoal = goal, TrainingExperienceYears = 1
        };
        var subscription = new Subscription
        {
            ClientProfile = client, MentorProfile = mentor, Status = SubscriptionStatus.PendingPayment, Price = Price, Currency = "usd"
        };
        var payment = new Payment
        {
            Subscription = subscription, Amount = Price, Currency = "usd", StripePaymentIntentId = "pi_test_1",
            Purpose = PaymentPurpose.Initial, Status = PaymentStatus.Pending
        };
        db.AddRange(mentor, client, subscription, payment);
        db.SaveChanges();
        return (subscription.Id, payment.Id);
    }
}

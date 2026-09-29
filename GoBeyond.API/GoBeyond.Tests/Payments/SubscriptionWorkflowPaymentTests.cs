using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Infrastructure.Services.Subscriptions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GoBeyond.Tests.Payments;

/// <summary>#1: uplata koja se ne može primijeniti se automatski vraća (ili ostaje RefundPending).</summary>
public class SubscriptionWorkflowPaymentTests
{
    private static readonly DateTime Now = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

    private readonly FakePaymentGateway _gateway = new();
    private readonly RecordingNotificationSender _notifications = new();
    private readonly SubscriptionWorkflow _workflow;

    public SubscriptionWorkflowPaymentTests()
    {
        _workflow = new SubscriptionWorkflow(_notifications, _gateway,
            Options.Create(new LifecycleOptions { SubscriptionPeriodDays = 30 }), NullLogger<SubscriptionWorkflow>.Instance);
    }

    private static Payment PaymentFor(SubscriptionStatus subscriptionStatus, PaymentPurpose purpose, string intentId = "pi_test_1")
    {
        var subscription = new Subscription
        {
            Id = 5,
            Status = subscriptionStatus,
            Price = 29.99m,
            Currency = "usd",
            EndDate = Now.AddDays(10),
            ClientProfile = new ClientProfile { User = new User { Id = 11, FirstName = "Tarik", LastName = "Hadžić", Email = "client@gobeyond.ba" } },
            MentorProfile = new MentorProfile { User = new User { Id = 22, FirstName = "Haris", LastName = "Mehmedović", Email = "mentor@gobeyond.ba" } }
        };
        var payment = new Payment
        {
            Id = 7, SubscriptionId = 5, Subscription = subscription, Amount = 29.99m, Currency = "usd",
            StripePaymentIntentId = intentId, Purpose = purpose, Status = PaymentStatus.Pending
        };
        subscription.Payments.Add(payment);
        return payment;
    }

    [Fact]
    public async Task InitialPayment_ForPendingSubscription_MovesItToAwaitingMentor()
    {
        var payment = PaymentFor(SubscriptionStatus.PendingPayment, PaymentPurpose.Initial);

        Assert.True(await _workflow.ApplySuccessfulPaymentAsync(payment, Now, CancellationToken.None));

        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(SubscriptionStatus.AwaitingMentor, payment.Subscription.Status);
        Assert.Contains(_notifications.Sent, x => x.UserId == 22 && x.Type == NotificationType.NewCollaborationRequest);
        Assert.Empty(_gateway.Refunds);
    }

    [Fact]
    public async Task InitialPayment_KeepsTheChargedAmountAsTheSubscriptionPrice()
    {
        // Ponovni ulazak u plaćanje je pretplati dao novu cijenu mentora, a naplaćen je raniji PaymentIntent (29,99).
        var payment = PaymentFor(SubscriptionStatus.PendingPayment, PaymentPurpose.Initial);
        payment.Subscription.Price = 35.00m;

        await _workflow.ApplySuccessfulPaymentAsync(payment, Now, CancellationToken.None);

        Assert.Equal(SubscriptionStatus.AwaitingMentor, payment.Subscription.Status);
        Assert.Equal((29.99m, "usd"), (payment.Subscription.Price, payment.Subscription.Currency));
    }

    [Fact]
    public async Task RenewalPayment_ForActiveSubscription_ExtendsEndDateBy30Days()
    {
        var payment = PaymentFor(SubscriptionStatus.Active, PaymentPurpose.Renewal);

        await _workflow.ApplySuccessfulPaymentAsync(payment, Now, CancellationToken.None);

        Assert.Equal(Now.AddDays(40), payment.Subscription.EndDate);
        Assert.Empty(_gateway.Refunds);
    }

    [Theory]
    [InlineData(SubscriptionStatus.Cancelled, PaymentPurpose.Initial)]      // klijent otkazao dok je plaćanje trajalo
    [InlineData(SubscriptionStatus.AwaitingMentor, PaymentPurpose.Initial)] // duplikat početne uplate
    [InlineData(SubscriptionStatus.Expired, PaymentPurpose.Renewal)]        // produženje stiglo nakon isteka
    [InlineData(SubscriptionStatus.Rejected, PaymentPurpose.Initial)]
    public async Task PaymentThatCannotBeApplied_IsRefundedAndClientIsNotified(SubscriptionStatus status, PaymentPurpose purpose)
    {
        var payment = PaymentFor(status, purpose);

        await _workflow.ApplySuccessfulPaymentAsync(payment, Now, CancellationToken.None);

        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal(Now, payment.RefundedAt);
        Assert.Equal(status, payment.Subscription.Status); // pretplata se ne mijenja
        Assert.Equal([("pi_test_1", "refund:pi_test_1")], _gateway.Refunds);
        var notification = Assert.Single(_notifications.Sent);
        Assert.Equal((11, NotificationType.PaymentRefunded, true), (notification.UserId, notification.Type, notification.Email));
        Assert.Contains("vraćen", notification.Body);
    }

    [Fact]
    public async Task PaymentThatCannotBeApplied_WhenRefundFails_IsMarkedRefundPending()
    {
        _gateway.FailRefunds = true;
        var payment = PaymentFor(SubscriptionStatus.Cancelled, PaymentPurpose.Initial);

        await _workflow.ApplySuccessfulPaymentAsync(payment, Now, CancellationToken.None);

        Assert.Equal(PaymentStatus.RefundPending, payment.Status);
        Assert.Null(payment.RefundedAt);
        var notification = Assert.Single(_notifications.Sent);
        Assert.Equal(NotificationType.PaymentRefunded, notification.Type);
        Assert.Contains("u obradi", notification.Body);
    }

    [Fact]
    public async Task RetryPendingRefund_WhenStripeRecovers_CompletesRefund()
    {
        _gateway.FailRefunds = true;
        var payment = PaymentFor(SubscriptionStatus.Cancelled, PaymentPurpose.Initial);
        await _workflow.ApplySuccessfulPaymentAsync(payment, Now, CancellationToken.None);

        Assert.False(await _workflow.RetryPendingRefundAsync(payment, Now, CancellationToken.None));
        Assert.Equal(PaymentStatus.RefundPending, payment.Status);

        _gateway.FailRefunds = false;
        Assert.True(await _workflow.RetryPendingRefundAsync(payment, Now, CancellationToken.None));
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Equal([("pi_test_1", "refund:pi_test_1")], _gateway.Refunds);
    }

    [Fact]
    public void RefundIdempotencyKey_UsesPaymentIntentIdNotDatabaseId()
    {
        // Ista uplata (Id 7) u drugoj bazi ima drugi PaymentIntent - ključ se ne smije ponoviti.
        var here = PaymentFor(SubscriptionStatus.AwaitingMentor, PaymentPurpose.Initial, intentId: "pi_A");
        var afterDatabaseReset = PaymentFor(SubscriptionStatus.AwaitingMentor, PaymentPurpose.Initial, intentId: "pi_B");

        Assert.Equal(here.Id, afterDatabaseReset.Id);
        Assert.Equal("refund:pi_A", SubscriptionWorkflow.RefundIdempotencyKey(here));
        Assert.NotEqual(SubscriptionWorkflow.RefundIdempotencyKey(here), SubscriptionWorkflow.RefundIdempotencyKey(afterDatabaseReset));
    }

    [Fact]
    public async Task AlreadyProcessedPayment_IsNotAppliedAgain()
    {
        var payment = PaymentFor(SubscriptionStatus.AwaitingMentor, PaymentPurpose.Initial);
        payment.Status = PaymentStatus.Refunded;

        Assert.False(await _workflow.ApplySuccessfulPaymentAsync(payment, Now, CancellationToken.None));
        Assert.Empty(_gateway.Refunds);
        Assert.Empty(_notifications.Sent);
    }

    [Fact]
    public async Task RefundOrFail_WhenStripeFails_ThrowsGivenMessage()
    {
        _gateway.FailRefunds = true;
        var payment = PaymentFor(SubscriptionStatus.AwaitingMentor, PaymentPurpose.Initial);
        payment.Status = PaymentStatus.Succeeded;

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            _workflow.RefundOrFailAsync(payment, Now, "Brisanje nije moguće.", CancellationToken.None));

        Assert.Equal("Brisanje nije moguće.", error.Message);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
    }

    [Fact]
    public async Task SeedPayment_IsRefundedLocallyWithoutCallingStripe()
    {
        _gateway.IsConfigured = false;
        _gateway.FailRefunds = true;
        var payment = PaymentFor(SubscriptionStatus.AwaitingMentor, PaymentPurpose.Initial, intentId: "seed_pi_emir_1");
        payment.Status = PaymentStatus.Succeeded;

        await _workflow.RefundOrFailAsync(payment, Now, "x", CancellationToken.None);

        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.Empty(_gateway.Refunds);
    }
}

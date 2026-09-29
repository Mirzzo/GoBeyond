using GoBeyond.Core.DTOs.Subscriptions;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Infrastructure.Services.Payments;
using GoBeyond.Tests.Subscriptions;

namespace GoBeyond.Tests.Payments;

/// <summary>
/// Zamijenjen ili prestar PaymentIntent se otkazuje na Stripe-u prije nego uplata postane Failed (inače ostaje plativ,
/// a naplata se nikad ne primijeni niti vrati), a usklađivanje provjerava i Failed uplate unutar prozora.
/// </summary>
public sealed class StaleIntentTests : IDisposable
{
    private readonly SubscriptionTestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task CreateIntent_AfterPriceChange_CancelsTheOldIntentAtStripeBeforeCreatingANewOne()
    {
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.PendingPayment, x => x.Price = 26.00m);
        var old = await _db.AddPaymentAsync(id, "pi_old", PaymentStatus.Pending, "requires_payment_method", amount: 27.50m);

        var result = await CreateIntentAsync(id);

        Assert.NotEqual(old, result.PaymentId);
        Assert.Equal(26.00m, Assert.Single(_db.Gateway.CreateCalls).Amount);
        Assert.Equal([("pi_old", "cancel:pi_old")], _db.Gateway.Cancels);
        Assert.Equal("canceled", _db.Gateway.Intents["pi_old"].Status);
        Assert.Equal(PaymentStatus.Failed, (await _db.SubscriptionAsync(id)).Payments.Single(x => x.Id == old).Status);
    }

    [Fact]
    public async Task CreateIntent_WhenTheOldIntentIsPaidWhileBeingCancelled_AppliesItAndReturns409()
    {
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.PendingPayment, x => x.Price = 26.00m);
        await _db.AddPaymentAsync(id, "pi_old", PaymentStatus.Pending, "requires_payment_method", amount: 27.50m);
        _db.Gateway.BeforeCancelIntent = intentId => _db.Gateway.SetStatus(intentId, "succeeded"); // klijent je upravo platio stari sheet

        var error = await Assert.ThrowsAsync<ConflictException>(() => CreateIntentAsync(id));

        Assert.Equal(PaymentService.AlreadyPaid, error.Message);
        Assert.Empty(_db.Gateway.CreateCalls);
        var subscription = await _db.SubscriptionAsync(id);
        Assert.Equal(SubscriptionStatus.AwaitingMentor, subscription.Status);
        Assert.Equal(PaymentStatus.Succeeded, Assert.Single(subscription.Payments).Status);
    }

    [Fact]
    public async Task CreateIntent_DoesNotReuseAnIntentOlderThanHalfTheReconcileWindow()
    {
        // Reconcile gleda uplate mlađe od 48 h (po CreatedAt); ponovo korišten stari PaymentIntent bi mogao biti plaćen
        // izvan prozora i nikad usklađen.
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.PendingPayment);
        var old = await _db.AddPaymentAsync(id, "pi_old", PaymentStatus.Pending, "requires_payment_method",
            createdAt: DateTime.UtcNow.AddHours(-30));

        var result = await CreateIntentAsync(id);

        Assert.NotEqual(old, result.PaymentId);
        Assert.Equal([("pi_old", "cancel:pi_old")], _db.Gateway.Cancels);
        Assert.Single(_db.Gateway.CreateCalls);
    }

    [Fact]
    public async Task CreateIntent_ReusesARecentIntentWithTheCurrentAmount()
    {
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.PendingPayment);
        var recent = await _db.AddPaymentAsync(id, "pi_recent", PaymentStatus.Pending, "requires_payment_method",
            createdAt: DateTime.UtcNow.AddHours(-2));

        var result = await CreateIntentAsync(id);

        Assert.Equal((recent, "pi_recent_secret"), (result.PaymentId, result.ClientSecret));
        Assert.Empty(_db.Gateway.Cancels);
        Assert.Empty(_db.Gateway.CreateCalls);
    }

    [Fact]
    public async Task Reconcile_FailedPaymentWhoseSupersededIntentWasPaid_IsApplied()
    {
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.PendingPayment);
        await _db.AddPaymentAsync(id, "pi_old", PaymentStatus.Failed, "succeeded", createdAt: DateTime.UtcNow.AddMinutes(-10));

        var applied = await ReconcileAsync();

        Assert.Equal(1, applied);
        var subscription = await _db.SubscriptionAsync(id);
        Assert.Equal(SubscriptionStatus.AwaitingMentor, subscription.Status);
        Assert.Equal(PaymentStatus.Succeeded, Assert.Single(subscription.Payments).Status);
    }

    [Fact]
    public async Task Reconcile_FailedPaymentPaidAfterTheClientCancelled_IsRefunded()
    {
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.Cancelled, x => x.PaidAt = null);
        await _db.AddPaymentAsync(id, "pi_old", PaymentStatus.Failed, "succeeded", createdAt: DateTime.UtcNow.AddMinutes(-10));

        await ReconcileAsync();

        Assert.Equal([("pi_old", "refund:pi_old")], _db.Gateway.Refunds);
        Assert.Equal(PaymentStatus.Refunded, Assert.Single((await _db.SubscriptionAsync(id)).Payments).Status);
    }

    [Fact]
    public async Task Reconcile_FailedPaymentWhoseIntentIsStillPayable_IsCancelledAtStripe()
    {
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.PendingPayment);
        await _db.AddPaymentAsync(id, "pi_old", PaymentStatus.Failed, "requires_payment_method", createdAt: DateTime.UtcNow.AddMinutes(-10));

        await ReconcileAsync();

        Assert.Equal([("pi_old", "cancel:pi_old")], _db.Gateway.Cancels);
        Assert.Equal(PaymentStatus.Failed, Assert.Single((await _db.SubscriptionAsync(id)).Payments).Status);
    }

    [Fact]
    public async Task Reconcile_PayableIntentOfASubscriptionThatNoLongerAcceptsIt_IsCancelledAtStripe()
    {
        // Pretplata je istekla dok Stripe nije bio dostupan, pa istek nije mogao zatvoriti PaymentIntent produženja.
        var expired = await _db.AddSubscriptionAsync(SubscriptionStatus.Expired);
        await _db.AddPaymentAsync(expired, "pi_renewal", PaymentStatus.Pending, "requires_payment_method", PaymentPurpose.Renewal,
            createdAt: DateTime.UtcNow.AddMinutes(-30));
        // Kontrola: napušten PaymentSheet pretplate koja još čeka plaćanje ostaje plativ (klijent se može vratiti).
        var pending = await _db.AddSubscriptionAsync(SubscriptionStatus.PendingPayment, clientProfileId: _db.SecondClientProfileId);
        await _db.AddPaymentAsync(pending, "pi_open", PaymentStatus.Pending, "requires_payment_method", createdAt: DateTime.UtcNow.AddMinutes(-30));

        await ReconcileAsync();

        Assert.Equal([("pi_renewal", "cancel:pi_renewal")], _db.Gateway.Cancels);
        Assert.Equal(PaymentStatus.Failed, Assert.Single((await _db.SubscriptionAsync(expired)).Payments).Status);
        Assert.Equal(PaymentStatus.Pending, Assert.Single((await _db.SubscriptionAsync(pending)).Payments).Status);
    }

    private Task<PaymentIntentDto> CreateIntentAsync(int subscriptionId) => _db.RunAsync(db =>
        _db.Payments(db).CreateIntentAsync(_db.ClientUser.Id, new CreatePaymentIntentRequest { SubscriptionId = subscriptionId }));

    private Task<int> ReconcileAsync() => _db.RunAsync(db =>
        _db.Payments(db).ReconcilePendingAsync(DateTime.UtcNow.AddMinutes(-5), DateTime.UtcNow.AddHours(-48)));
}

using GoBeyond.Core.DTOs.Admin;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;

namespace GoBeyond.Tests.Subscriptions;

/// <summary>
/// Otkazivanje pretplate (administrator i klijent): plaćen zahtjev koji mentor nije prihvatio se vraća, mentor dobija
/// obavijest samo o zahtjevu koji je vidio, a nedovršena uplata ne smije ostati naplaćena ili plativa nakon otkazivanja.
/// </summary>
public sealed class SubscriptionCancelTests : IDisposable
{
    private const string AdminReason = "Mentor ne odgovara na zahtjev";

    private readonly SubscriptionTestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task AdminCancel_OfPaidRequestAwaitingMentor_RefundsThePaymentAndTellsTheClient()
    {
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.AwaitingMentor);
        await _db.AddPaymentAsync(id, "pi_paid", PaymentStatus.Succeeded, "succeeded", amount: 27.50m);

        var result = await AdminCancelAsync(id);

        Assert.Equal(SubscriptionStatus.Cancelled, result.Status);
        Assert.Equal([("pi_paid", "refund:pi_paid")], _db.Gateway.Refunds);
        var payment = Assert.Single((await _db.SubscriptionAsync(id)).Payments);
        Assert.Equal(PaymentStatus.Refunded, payment.Status);
        Assert.NotNull(payment.RefundedAt);
        var client = Assert.Single(await _db.NotificationsAsync(_db.ClientUser.Id));
        Assert.Equal(NotificationType.SubscriptionCancelled, client.Type);
        Assert.Equal("Saradnja sa mentorom Selma Delić je prekinuta. Razlog: Mentor ne odgovara na zahtjev. " +
                     "Uplaćeni iznos od 27,50 USD biće vraćen na vašu karticu.", client.Body);
        Assert.Contains(await _db.NotificationsAsync(_db.MentorUser.Id), x => x.Type == NotificationType.SubscriptionCancelled);
    }

    [Fact]
    public async Task AdminCancel_WhenTheRefundFails_Returns400AndChangesNothing()
    {
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.AwaitingMentor);
        await _db.AddPaymentAsync(id, "pi_paid", PaymentStatus.Succeeded, "succeeded");
        _db.Gateway.FailRefunds = true;

        var error = await Assert.ThrowsAsync<ValidationException>(() => AdminCancelAsync(id));

        Assert.Equal("Otkazivanje nije moguće: povrat uplate klijentu nije uspio. Pokušajte ponovo.", error.Message);
        var subscription = await _db.SubscriptionAsync(id);
        Assert.Equal(SubscriptionStatus.AwaitingMentor, subscription.Status);
        Assert.Equal(PaymentStatus.Succeeded, Assert.Single(subscription.Payments).Status);
        Assert.Empty(await _db.NotificationsAsync(_db.ClientUser.Id));
    }

    [Fact]
    public async Task AdminCancel_OfNeverPaidRequest_DoesNotNotifyTheMentorAndClosesTheOpenIntent()
    {
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.PendingPayment);
        await _db.AddPaymentAsync(id, "pi_open", PaymentStatus.Pending, "requires_payment_method");

        await AdminCancelAsync(id);

        Assert.Empty(await _db.NotificationsAsync(_db.MentorUser.Id));
        var client = Assert.Single(await _db.NotificationsAsync(_db.ClientUser.Id));
        Assert.DoesNotContain("vraćen", client.Body);
        Assert.Equal([("pi_open", "cancel:pi_open")], _db.Gateway.Cancels);
        Assert.Equal(PaymentStatus.Failed, Assert.Single((await _db.SubscriptionAsync(id)).Payments).Status);
    }

    [Fact]
    public async Task AdminCancel_OfActiveSubscription_KeepsThePaymentAndNotifiesBoth()
    {
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.Active);
        await _db.AddPaymentAsync(id, "pi_paid", PaymentStatus.Succeeded, "succeeded");

        await AdminCancelAsync(id);

        Assert.Empty(_db.Gateway.Refunds);
        Assert.Equal(PaymentStatus.Succeeded, Assert.Single((await _db.SubscriptionAsync(id)).Payments).Status);
        var client = Assert.Single(await _db.NotificationsAsync(_db.ClientUser.Id));
        Assert.Equal("Saradnja sa mentorom Selma Delić je prekinuta. Razlog: Mentor ne odgovara na zahtjev.", client.Body);
        Assert.Single(await _db.NotificationsAsync(_db.MentorUser.Id));
    }

    [Fact]
    public async Task ClientCancel_OfPendingPaymentWhoseIntentWasAlreadyCharged_RefundsTheCharge()
    {
        // Klijent je platio, ali confirm nije stigao (aplikacija ugašena), pa zatim otkazuje pretplatu.
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.PendingPayment);
        await _db.AddPaymentAsync(id, "pi_paid", PaymentStatus.Pending, "succeeded");

        var result = await _db.RunAsync(db => _db.Subscriptions(db).CancelAsync(_db.ClientUser.Id, id));

        Assert.Equal(SubscriptionStatus.Cancelled, result.Status);
        Assert.Equal([("pi_paid", "refund:pi_paid")], _db.Gateway.Refunds);
        Assert.Equal(PaymentStatus.Refunded, Assert.Single((await _db.SubscriptionAsync(id)).Payments).Status);
        Assert.Contains(await _db.NotificationsAsync(_db.ClientUser.Id), x => x.Type == NotificationType.PaymentRefunded);
        Assert.Empty(await _db.NotificationsAsync(_db.MentorUser.Id));
    }

    [Fact]
    public async Task ClientCancel_OfPendingPayment_CancelsThePayableIntentAtStripe()
    {
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.PendingPayment);
        await _db.AddPaymentAsync(id, "pi_open", PaymentStatus.Pending, "requires_payment_method");

        await _db.RunAsync(db => _db.Subscriptions(db).CancelAsync(_db.ClientUser.Id, id));

        Assert.Equal([("pi_open", "cancel:pi_open")], _db.Gateway.Cancels);
        Assert.Equal(PaymentStatus.Failed, Assert.Single((await _db.SubscriptionAsync(id)).Payments).Status);
        Assert.Equal("canceled", _db.Gateway.Intents["pi_open"].Status);
    }

    [Fact]
    public async Task ClientCancel_WhenStripeIsUnreachable_Returns400AndChangesNothing()
    {
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.PendingPayment);
        await _db.AddPaymentAsync(id, "pi_open", PaymentStatus.Pending, "requires_payment_method");
        _db.Gateway.BeforeGetIntent = _ => throw new ValidationException("Komunikacija sa Stripe servisom nije uspjela. Pokušajte ponovo.");

        await Assert.ThrowsAsync<ValidationException>(() => _db.RunAsync(db => _db.Subscriptions(db).CancelAsync(_db.ClientUser.Id, id)));

        var subscription = await _db.SubscriptionAsync(id);
        Assert.Equal(SubscriptionStatus.PendingPayment, subscription.Status);
        Assert.Equal(PaymentStatus.Pending, Assert.Single(subscription.Payments).Status);
    }

    private Task<AdminSubscriptionDto> AdminCancelAsync(int id) =>
        _db.RunAsync(db => _db.Subscriptions(db).AdminCancelAsync(id, new CancelSubscriptionRequest { Reason = AdminReason }));
}

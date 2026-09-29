using GoBeyond.Core.DTOs.Admin;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Tests.Subscriptions;

namespace GoBeyond.Tests.Payments;

/// <summary>
/// Naplatu osporenu kod banke klijenta (Stripe "charge_disputed") Stripe ne vraća dok spor traje. Uplata tada dobija status
/// Disputed umjesto da se povrat ponavlja u svakom ciklusu, a zahtjev koji mentor nije prihvatio se ipak može odbiti ili
/// otkazati. Ostale greške povrata se i dalje ponavljaju, odnosno vraćaju 400.
/// </summary>
public sealed class DisputedRefundTests : IDisposable
{
    private const string DisputedSentence =
        "Uplata od 29,99 USD je osporena kod vaše banke, pa se ne vraća automatski. O povratu odlučuje postupak osporavanja.";

    private readonly SubscriptionTestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task RefundRetry_WhenTheChargeIsDisputed_StopsAfterOneAttemptWhileOtherFailuresAreStillRetried()
    {
        var disputed = await _db.AddSubscriptionAsync(SubscriptionStatus.Cancelled);
        await _db.AddPaymentAsync(disputed, "pi_disputed", PaymentStatus.RefundPending, "succeeded");
        var unreachable = await _db.AddSubscriptionAsync(SubscriptionStatus.Cancelled, clientProfileId: _db.SecondClientProfileId);
        await _db.AddPaymentAsync(unreachable, "pi_unreachable", PaymentStatus.RefundPending, "succeeded");
        _db.Gateway.DisputedCharges.Add("pi_disputed");
        _db.Gateway.BeforeRefund = id => id == "pi_unreachable"
            ? throw new ValidationException("Komunikacija sa Stripe servisom nije uspjela. Pokušajte ponovo.")
            : Task.CompletedTask;

        for (var run = 0; run < 3; run++)
            Assert.Equal(0, (await _db.RunAsync(db => _db.LifecycleProcessor(db).RunAsync(DateTime.UtcNow))).RefundsCompleted);

        Assert.Single(_db.Gateway.RefundAttempts, x => x == "pi_disputed");
        Assert.Equal(3, _db.Gateway.RefundAttempts.Count(x => x == "pi_unreachable"));
        var payment = Assert.Single((await _db.SubscriptionAsync(disputed)).Payments);
        Assert.Equal(PaymentStatus.Disputed, payment.Status);
        Assert.Null(payment.RefundedAt);
        Assert.Equal(PaymentStatus.RefundPending, Assert.Single((await _db.SubscriptionAsync(unreachable)).Payments).Status);
        var notice = Assert.Single(await _db.NotificationsAsync(_db.ClientUser.Id));
        Assert.Equal((NotificationType.PaymentRefunded, "Uplata je osporena"), (notice.Type, notice.Title));
        Assert.Equal("Uplata od 29,99 USD je osporena kod vaše banke, pa se njen povrat više ne ponavlja automatski. " +
                     "O povratu odlučuje postupak osporavanja.", notice.Body);
    }

    [Fact]
    public async Task MentorReject_OfARequestWhoseChargeIsDisputed_ClosesItWithoutARefund()
    {
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.AwaitingMentor);
        await _db.AddPaymentAsync(id, "pi_disputed", PaymentStatus.Succeeded, "succeeded");
        _db.Gateway.DisputedCharges.Add("pi_disputed");

        var response = await _db.RunAsync(db => _db.Collaboration(db).RejectAsync(_db.MentorUser.Id, id, "Trenutno nemam slobodnih termina"));

        Assert.Equal("Zahtjev klijenta Nađa Škrijelj je odbijen. Uplata je osporena kod banke klijenta, pa se ne vraća " +
                     "automatski; o povratu odlučuje postupak osporavanja.", response.Message);
        var subscription = await _db.SubscriptionAsync(id);
        Assert.Equal(SubscriptionStatus.Rejected, subscription.Status);
        Assert.Equal(PaymentStatus.Disputed, Assert.Single(subscription.Payments).Status);
        Assert.Empty(_db.Gateway.Refunds);
        var client = Assert.Single(await _db.NotificationsAsync(_db.ClientUser.Id));
        Assert.Equal("Vaš zahtjev za saradnju sa mentorom Selma Delić je odbijen. Razlog: Trenutno nemam slobodnih termina. " +
                     DisputedSentence, client.Body);

        await _db.RunAsync(db => _db.LifecycleProcessor(db).RunAsync(DateTime.UtcNow));
        Assert.Single(_db.Gateway.RefundAttempts);
    }

    [Fact]
    public async Task AdminCancel_OfARequestWhoseChargeIsDisputed_ClosesItAndNamesBothAmounts()
    {
        // Duplikat početne uplate čiji povrat još čeka (RefundPending) se vraća, a osporena naplata ne.
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.AwaitingMentor);
        await _db.AddPaymentAsync(id, "pi_disputed", PaymentStatus.Succeeded, "succeeded");
        await _db.AddPaymentAsync(id, "pi_duplicate", PaymentStatus.RefundPending, "succeeded", amount: 12.50m);
        _db.Gateway.DisputedCharges.Add("pi_disputed");

        var result = await _db.RunAsync(db => _db.Subscriptions(db).AdminCancelAsync(id,
            new CancelSubscriptionRequest { Reason = "Mentor ne odgovara na zahtjev" }));

        Assert.Equal(SubscriptionStatus.Cancelled, result.Status);
        Assert.Equal([("pi_duplicate", "refund:pi_duplicate")], _db.Gateway.Refunds);
        var payments = (await _db.SubscriptionAsync(id)).Payments.OrderBy(x => x.Id).Select(x => x.Status);
        Assert.Equal([PaymentStatus.Disputed, PaymentStatus.Refunded], payments);
        var client = Assert.Single(await _db.NotificationsAsync(_db.ClientUser.Id));
        Assert.Equal("Saradnja sa mentorom Selma Delić je prekinuta. Razlog: Mentor ne odgovara na zahtjev. " +
                     "Uplaćeni iznos od 12,50 USD biće vraćen na vašu karticu. " + DisputedSentence, client.Body);
        Assert.Single(await _db.NotificationsAsync(_db.MentorUser.Id));
    }

    [Fact]
    public async Task AdminCancel_WhenTheRefundFailsForAnotherReason_StillReturns400()
    {
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.AwaitingMentor);
        await _db.AddPaymentAsync(id, "pi_paid", PaymentStatus.Succeeded, "succeeded");
        _db.Gateway.FailRefunds = true;

        var error = await Assert.ThrowsAsync<ValidationException>(() => _db.RunAsync(db => _db.Subscriptions(db).AdminCancelAsync(id,
            new CancelSubscriptionRequest { Reason = "Mentor ne odgovara na zahtjev" })));

        Assert.Equal("Otkazivanje nije moguće: povrat uplate klijentu nije uspio. Pokušajte ponovo.", error.Message);
        Assert.Equal(PaymentStatus.Succeeded, Assert.Single((await _db.SubscriptionAsync(id)).Payments).Status);
    }

    [Fact]
    public async Task ClientCancel_OfPendingPaymentWhoseChargedIntentIsDisputed_MarksThePaymentDisputed()
    {
        // Klijent je platio osporenom karticom, confirm nije stigao, pa je otkazao pretplatu.
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.PendingPayment);
        await _db.AddPaymentAsync(id, "pi_disputed", PaymentStatus.Pending, "succeeded");
        _db.Gateway.DisputedCharges.Add("pi_disputed");

        var result = await _db.RunAsync(db => _db.Subscriptions(db).CancelAsync(_db.ClientUser.Id, id));
        await _db.RunAsync(db => _db.LifecycleProcessor(db).RunAsync(DateTime.UtcNow));

        Assert.Equal(SubscriptionStatus.Cancelled, result.Status);
        Assert.Equal(PaymentStatus.Disputed, Assert.Single((await _db.SubscriptionAsync(id)).Payments).Status);
        Assert.Single(_db.Gateway.RefundAttempts);
        var notice = Assert.Single(await _db.NotificationsAsync(_db.ClientUser.Id));
        Assert.Equal((NotificationType.PaymentRefunded, "Uplata je osporena"), (notice.Type, notice.Title));
        Assert.Equal("Uplata od 29,99 USD za saradnju sa mentorom Selma Delić nije mogla biti primijenjena jer pretplata više " +
                     "nije u odgovarajućem statusu (Otkazana). Uplata je osporena kod vaše banke, pa se ne vraća automatski. " +
                     "O povratu odlučuje postupak osporavanja.", notice.Body);
    }
}

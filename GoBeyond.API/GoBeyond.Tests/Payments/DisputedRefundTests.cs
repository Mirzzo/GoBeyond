using GoBeyond.Core.DTOs.Admin;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Security;
using GoBeyond.Infrastructure.Services.Admin;
using GoBeyond.Infrastructure.Services.Users;
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

    private const string AdminDisputeWarning =
        "Uplata od 29,99 USD je osporena kod banke klijenta i nije vraćena; ishod rješava postupak osporavanja na Stripe-u.";

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

        Assert.Equal("Zahtjev klijenta Nađa Škrijelj je odbijen. Uplata od 29,99 USD je osporena kod banke klijenta, pa se ne " +
                     "vraća automatski; o povratu odlučuje postupak osporavanja.", response.Message);
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
    public async Task MentorReject_WithARefundedAndADisputedPayment_NamesBothAmountsLikeTheClientNotice()
    {
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.AwaitingMentor);
        await _db.AddPaymentAsync(id, "pi_disputed", PaymentStatus.Succeeded, "succeeded");
        await _db.AddPaymentAsync(id, "pi_duplicate", PaymentStatus.RefundPending, "succeeded", amount: 12.50m);
        _db.Gateway.DisputedCharges.Add("pi_disputed");

        var response = await _db.RunAsync(db => _db.Collaboration(db).RejectAsync(_db.MentorUser.Id, id, "Trenutno nemam slobodnih termina"));

        Assert.Equal("Zahtjev klijenta Nađa Škrijelj je odbijen. Uplaćeni iznos od 12,50 USD je vraćen, a uplata od 29,99 USD je " +
                     "osporena kod banke klijenta, pa se ne vraća automatski; o povratu odlučuje postupak osporavanja.", response.Message);
        var client = Assert.Single(await _db.NotificationsAsync(_db.ClientUser.Id));
        Assert.Equal("Vaš zahtjev za saradnju sa mentorom Selma Delić je odbijen. Razlog: Trenutno nemam slobodnih termina. " +
                     "Uplaćeni iznos od 12,50 USD biće vraćen na vašu karticu. " + DisputedSentence, client.Body);
    }

    // A duplicate disputed earlier (as an unapplied payment) is not part of this reject: the reject refunded the paid request.
    [Fact]
    public async Task MentorReject_WhenOnlyAnEarlierDuplicateIsDisputed_SaysThePaymentWasRefunded()
    {
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.AwaitingMentor);
        await _db.AddPaymentAsync(id, "pi_paid", PaymentStatus.Succeeded, "succeeded");
        await _db.AddPaymentAsync(id, "pi_earlier_dispute", PaymentStatus.Disputed, "succeeded", amount: 12.50m);

        var response = await _db.RunAsync(db => _db.Collaboration(db).RejectAsync(_db.MentorUser.Id, id, "Trenutno nemam slobodnih termina"));

        Assert.Equal("Zahtjev klijenta Nađa Škrijelj je odbijen, a uplaćeni iznos od 29,99 USD je vraćen.", response.Message);
        Assert.Equal([("pi_paid", "refund:pi_paid")], _db.Gateway.Refunds);
        var client = Assert.Single(await _db.NotificationsAsync(_db.ClientUser.Id));
        Assert.Equal("Vaš zahtjev za saradnju sa mentorom Selma Delić je odbijen. Razlog: Trenutno nemam slobodnih termina. " +
                     "Uplaćeni iznos od 29,99 USD biće vraćen na vašu karticu.", client.Body);
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
        Assert.Equal(AdminDisputeWarning, result.Warning);
        Assert.Equal([(29.99m, PaymentStatus.Disputed), (12.50m, PaymentStatus.Refunded)],
            result.Payments.Select(x => (x.Amount, x.Status)).OrderByDescending(x => x.Amount));
        Assert.Equal([("pi_duplicate", "refund:pi_duplicate")], _db.Gateway.Refunds);
        var payments = (await _db.SubscriptionAsync(id)).Payments.OrderBy(x => x.Id).Select(x => x.Status);
        Assert.Equal([PaymentStatus.Disputed, PaymentStatus.Refunded], payments);
        var client = Assert.Single(await _db.NotificationsAsync(_db.ClientUser.Id));
        Assert.Equal("Saradnja sa mentorom Selma Delić je prekinuta. Razlog: Mentor ne odgovara na zahtjev. " +
                     "Uplaćeni iznos od 12,50 USD biće vraćen na vašu karticu. " + DisputedSentence, client.Body);
        Assert.Single(await _db.NotificationsAsync(_db.MentorUser.Id));
    }

    [Fact]
    public async Task AdminCancel_WithARefundOrAnEarlierDispute_HasNoWarning()
    {
        // Duplikat je osporen ranije (i vidi se u uplatama pretplate); ovo otkazivanje je vratilo plaćeni zahtjev.
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.AwaitingMentor);
        await _db.AddPaymentAsync(id, "pi_paid", PaymentStatus.Succeeded, "succeeded");
        await _db.AddPaymentAsync(id, "pi_earlier_dispute", PaymentStatus.Disputed, "succeeded", amount: 12.50m);

        var result = await _db.RunAsync(db => _db.Subscriptions(db).AdminCancelAsync(id,
            new CancelSubscriptionRequest { Reason = "Mentor ne odgovara na zahtjev" }));

        Assert.Null(result.Warning);
        Assert.Equal([(29.99m, PaymentStatus.Refunded), (12.50m, PaymentStatus.Disputed)],
            result.Payments.Select(x => (x.Amount, x.Status)).OrderByDescending(x => x.Amount));
    }

    // Klijent je platio osporenom karticom, a confirm nije stigao: otkazivanje zatvara nedovršenu uplatu i ne može je vratiti.
    [Fact]
    public async Task AdminCancel_OfPendingPaymentWhoseChargedIntentIsDisputed_WarnsTheAdmin()
    {
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.PendingPayment);
        await _db.AddPaymentAsync(id, "pi_disputed", PaymentStatus.Pending, "succeeded");
        _db.Gateway.DisputedCharges.Add("pi_disputed");

        var result = await _db.RunAsync(db => _db.Subscriptions(db).AdminCancelAsync(id,
            new CancelSubscriptionRequest { Reason = "Dvostruka registracija klijenta" }));

        Assert.Equal(SubscriptionStatus.Cancelled, result.Status);
        Assert.Equal(AdminDisputeWarning, result.Warning);
        Assert.Equal(PaymentStatus.Disputed, Assert.Single(result.Payments).Status);
    }

    [Fact]
    public async Task DeletingAMentor_WhoseRequestsHaveDisputedCharges_WarnsTheAdmin()
    {
        var first = await _db.AddSubscriptionAsync(SubscriptionStatus.AwaitingMentor);
        await _db.AddPaymentAsync(first, "pi_disputed_1", PaymentStatus.Succeeded, "succeeded");
        var second = await _db.AddSubscriptionAsync(SubscriptionStatus.AwaitingMentor, clientProfileId: _db.SecondClientProfileId);
        await _db.AddPaymentAsync(second, "pi_disputed_2", PaymentStatus.Succeeded, "succeeded", amount: 15.00m);
        _db.Gateway.DisputedCharges.UnionWith(["pi_disputed_1", "pi_disputed_2"]);

        var response = await _db.RunAsync(db => DeleteUserAsync(db, _db.MentorUser.Id));

        Assert.Equal("Korisnik Selma Delić je obrisan.", response.Message);
        Assert.Equal("Osporene uplate od ukupno 44,99 USD nisu vraćene; ishod rješava postupak osporavanja na Stripe-u.",
            response.Warning);
        Assert.Equal(PaymentStatus.Disputed, Assert.Single((await _db.SubscriptionAsync(second)).Payments).Status);
    }

    [Fact]
    public async Task DeletingAClient_WhoseRequestChargeIsDisputed_WarnsTheAdmin_AndWithoutADispute_DoesNot()
    {
        var disputed = await _db.AddSubscriptionAsync(SubscriptionStatus.AwaitingMentor);
        await _db.AddPaymentAsync(disputed, "pi_disputed", PaymentStatus.Succeeded, "succeeded");
        _db.Gateway.DisputedCharges.Add("pi_disputed");
        var refunded = await _db.AddSubscriptionAsync(SubscriptionStatus.AwaitingMentor, clientProfileId: _db.SecondClientProfileId);
        await _db.AddPaymentAsync(refunded, "pi_paid", PaymentStatus.Succeeded, "succeeded");

        var withDispute = await _db.RunAsync(db => DeleteUserAsync(db, _db.ClientUser.Id));
        var withoutDispute = await _db.RunAsync(db => DeleteUserAsync(db, _db.SecondClientUser.Id));

        Assert.Equal(("Korisnik Nađa Škrijelj je obrisan.", AdminDisputeWarning), (withDispute.Message, withDispute.Warning));
        Assert.Equal(("Korisnik Hana Kurić je obrisan.", (string?)null), (withoutDispute.Message, withoutDispute.Warning));
        Assert.Equal(PaymentStatus.Refunded, Assert.Single((await _db.SubscriptionAsync(refunded)).Payments).Status);
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

    private Task<AdminDeleteUserResponse> DeleteUserAsync(GoBeyondDbContext db, int userId) =>
        new AdminUserService(db, new UserAccountValidator(db), new PasswordHasher(), _db.Workflow(db)).DeleteAsync(adminUserId: 0, userId);
}

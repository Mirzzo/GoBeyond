using System.Data.Common;
using GoBeyond.Core.DTOs.Common;
using GoBeyond.Core.DTOs.Mentors;
using GoBeyond.Core.DTOs.Subscriptions;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Services.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GoBeyond.Tests.Subscriptions;

/// <summary>
/// Istovremeni prelazi iste pretplate (dva zahtjeva, ili zahtjev i SubscriptionLifecycleService): tačno jedan prelaz
/// pobjeđuje, a drugi vidi njegov rezultat umjesto da ga pregazi. Druga operacija se pokreće dok prva čeka na Stripe
/// (kukica lažnog gateway-a), svaka na svojoj konekciji, kao dva HTTP zahtjeva.
/// </summary>
public sealed class SubscriptionConcurrencyTests : IDisposable
{
    private readonly SubscriptionTestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task AcceptWhileRejectWaitsForRefund_OnlyTheRejectWins()
    {
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.AwaitingMentor);
        await _db.AddPaymentAsync(id, "pi_paid", PaymentStatus.Succeeded, "succeeded");
        Task<CollaborationRequestDto>? accept = null;
        _db.Gateway.BeforeRefund = async _ =>
        {
            _db.Gateway.BeforeRefund = null;
            // Mentor na drugom uređaju prihvata isti zahtjev dok odbijanje čeka Stripe povrat.
            accept = Task.Run(() => _db.RunAsync(db => _db.Collaboration(db).AcceptAsync(_db.MentorUser.Id, id)));
            await Task.Delay(500);
        };

        await _db.RunAsync(db => _db.Collaboration(db).RejectAsync(_db.MentorUser.Id, id, "Trenutno nemam slobodnih termina"));
        var acceptError = await Assert.ThrowsAsync<ValidationException>(() => accept!);

        Assert.Equal("Zahtjev se može prihvatiti samo dok čeka odgovor mentora.", acceptError.Message);
        var subscription = await _db.SubscriptionAsync(id);
        Assert.Equal(SubscriptionStatus.Rejected, subscription.Status);
        Assert.Null(subscription.AcceptedAt);
        Assert.Null(subscription.EndDate);
        Assert.Equal(PaymentStatus.Refunded, Assert.Single(subscription.Payments).Status);
        Assert.Single(_db.Gateway.Refunds);
        var clientOutcomes = (await _db.NotificationsAsync(_db.ClientUser.Id)).Select(x => x.Type).ToList();
        Assert.Equal([NotificationType.RequestRejected], clientOutcomes);
    }

    [Fact]
    public async Task RejectAfterAcceptWon_IsRefusedWithoutRefund()
    {
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.AwaitingMentor);
        await _db.AddPaymentAsync(id, "pi_paid", PaymentStatus.Succeeded, "succeeded");

        await _db.RunAsync(db => _db.Collaboration(db).AcceptAsync(_db.MentorUser.Id, id));
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            _db.RunAsync(db => _db.Collaboration(db).RejectAsync(_db.MentorUser.Id, id, "Trenutno nemam slobodnih termina.")));

        Assert.Equal("Zahtjev se može odbiti samo dok čeka odgovor mentora.", error.Message);
        Assert.Empty(_db.Gateway.Refunds);
        Assert.Equal(SubscriptionStatus.Active, (await _db.SubscriptionAsync(id)).Status);
    }

    [Fact]
    public async Task SecondRejectWhileTheFirstWaitsForRefund_IsRefusedAndRefundsOnlyOnce()
    {
        // Dvostruki tap na ODBIJ (PAY-12): drugi zahtjev ne smije ponovo slati povrat niti javiti da povrat nije uspio.
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.AwaitingMentor);
        await _db.AddPaymentAsync(id, "pi_paid", PaymentStatus.Succeeded, "succeeded");
        Task<MessageResponse>? second = null;
        _db.Gateway.BeforeRefund = async _ =>
        {
            _db.Gateway.BeforeRefund = null;
            second = Task.Run(() => _db.RunAsync(db =>
                _db.Collaboration(db).RejectAsync(_db.MentorUser.Id, id, "Trenutno nemam slobodnih termina.")));
            await Task.Delay(500);
        };

        await _db.RunAsync(db => _db.Collaboration(db).RejectAsync(_db.MentorUser.Id, id, "Trenutno nemam slobodnih termina."));
        var error = await Assert.ThrowsAsync<ValidationException>(() => second!);

        Assert.Equal("Zahtjev se može odbiti samo dok čeka odgovor mentora.", error.Message);
        Assert.Single(_db.Gateway.Refunds);
        Assert.Equal(SubscriptionStatus.Rejected, (await _db.SubscriptionAsync(id)).Status);
        Assert.Single(await _db.NotificationsAsync(_db.ClientUser.Id), x => x.Type == NotificationType.RequestRejected);
    }

    [Fact]
    public async Task FiveParallelCreateIntents_ShareOnePaymentIntent()
    {
        // Dvostruki tap na PLATI: istovremeni create-intent zahtjevi čekaju jedan na drugi (zaključana pretplata), pa
        // svi dobijaju isti PaymentIntent umjesto pet naplativih.
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.PendingPayment);
        _db.Gateway.BeforeCreateIntent = () => Task.Delay(300);

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Task.Run(() => _db.RunAsync(db =>
            _db.Payments(db).CreateIntentAsync(_db.ClientUser.Id, new CreatePaymentIntentRequest { SubscriptionId = id })))));

        Assert.Single(_db.Gateway.CreateCalls);
        Assert.Single(results.Select(x => (x.PaymentId, x.ClientSecret)).Distinct());
        Assert.Single((await _db.SubscriptionAsync(id)).Payments);
    }

    [Fact]
    public async Task ClientCancelWhileConfirmWaitsForStripe_StaysCancelledAndTheChargeIsRefunded()
    {
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.PendingPayment);
        var paymentId = await _db.AddPaymentAsync(id, "pi_paid", PaymentStatus.Pending, "succeeded");
        Task<SubscriptionDto>? cancel = null;
        _db.Gateway.BeforeGetIntent = async _ =>
        {
            _db.Gateway.BeforeGetIntent = null;
            cancel = Task.Run(() => _db.RunAsync(db => _db.Subscriptions(db).CancelAsync(_db.ClientUser.Id, id)));
            await Task.Delay(500);
        };

        var confirmed = await _db.RunAsync(db => _db.Payments(db).ConfirmAsync(_db.ClientUser.Id, paymentId));
        var cancelled = await cancel!;

        Assert.Equal(SubscriptionStatus.Cancelled, cancelled.Status);
        Assert.Equal(SubscriptionStatus.Cancelled, confirmed.Status);
        var subscription = await _db.SubscriptionAsync(id);
        Assert.Equal(SubscriptionStatus.Cancelled, subscription.Status);
        Assert.Equal(PaymentStatus.Refunded, Assert.Single(subscription.Payments).Status);
        Assert.Equal([("pi_paid", "refund:pi_paid")], _db.Gateway.Refunds);
        Assert.DoesNotContain(await _db.NotificationsAsync(_db.MentorUser.Id), x => x.Type == NotificationType.NewCollaborationRequest);
    }

    [Fact]
    public async Task ExpiryRunWhileRenewalConfirmWaitsForStripe_ExtendsInsteadOfExpiring()
    {
        var now = DateTime.UtcNow;
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.Active, x => x.EndDate = now.AddMinutes(-1));
        var paymentId = await _db.AddPaymentAsync(id, "pi_renewal", PaymentStatus.Pending, "succeeded", PaymentPurpose.Renewal,
            createdAt: now.AddMinutes(-2));
        Task? lifecycle = null;
        _db.Gateway.BeforeGetIntent = async _ =>
        {
            _db.Gateway.BeforeGetIntent = null;
            lifecycle = Task.Run(() => _db.RunAsync(db => _db.LifecycleProcessor(db).RunAsync(DateTime.UtcNow)));
            await Task.Delay(500);
        };

        var confirmed = await _db.RunAsync(db => _db.Payments(db).ConfirmAsync(_db.ClientUser.Id, paymentId));
        await lifecycle!;

        Assert.Equal(SubscriptionStatus.Active, confirmed.Status);
        var subscription = await _db.SubscriptionAsync(id);
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.True(subscription.EndDate > now.AddDays(29), $"EndDate {subscription.EndDate:O}");
        Assert.Equal(PaymentStatus.Succeeded, Assert.Single(subscription.Payments).Status);
        Assert.Empty(_db.Gateway.Refunds);
        Assert.DoesNotContain(await _db.NotificationsAsync(_db.ClientUser.Id), x => x.Type == NotificationType.SubscriptionExpired);
    }

    [Fact]
    public async Task TwoPaidInitialIntentsConfirmedAtTheSameTime_OneIsAppliedAndTheOtherRefunded()
    {
        // Klijent je platio i zamijenjeni (Failed) i novi PaymentIntent (dva uređaja); obje potvrde stižu istovremeno.
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.PendingPayment);
        var first = await _db.AddPaymentAsync(id, "pi_a", PaymentStatus.Failed, "succeeded");
        var second = await _db.AddPaymentAsync(id, "pi_b", PaymentStatus.Pending, "succeeded");
        Task<SubscriptionDetailDto>? confirmSecond = null;
        _db.Gateway.BeforeGetIntent = async intentId =>
        {
            if (intentId != "pi_a") return;
            _db.Gateway.BeforeGetIntent = null;
            confirmSecond = Task.Run(() => _db.RunAsync(db => _db.Payments(db).ConfirmAsync(_db.ClientUser.Id, second)));
            await Task.Delay(500);
        };

        var confirmedFirst = await _db.RunAsync(db => _db.Payments(db).ConfirmAsync(_db.ClientUser.Id, first));
        var confirmedSecond = await confirmSecond!;

        Assert.Equal(SubscriptionStatus.AwaitingMentor, confirmedFirst.Status);
        Assert.Equal(SubscriptionStatus.AwaitingMentor, confirmedSecond.Status);
        var payments = (await _db.SubscriptionAsync(id)).Payments.ToDictionary(x => x.Id, x => x.Status);
        Assert.Equal(PaymentStatus.Succeeded, payments[second]);
        Assert.Equal(PaymentStatus.Refunded, payments[first]);
        Assert.Equal([("pi_a", "refund:pi_a")], _db.Gateway.Refunds);
        Assert.Single(await _db.NotificationsAsync(_db.MentorUser.Id), x => x.Type == NotificationType.NewCollaborationRequest);
        Assert.Contains(await _db.NotificationsAsync(_db.ClientUser.Id), x => x.Type == NotificationType.PaymentRefunded);
    }

    [Fact]
    public async Task ConfirmWhileClientCancelWaitsForStripe_TheCancelStandsAndTheChargeIsRefundedOnce()
    {
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.PendingPayment);
        var paymentId = await _db.AddPaymentAsync(id, "pi_paid", PaymentStatus.Pending, "succeeded");
        Task<SubscriptionDetailDto>? confirm = null;
        _db.Gateway.BeforeGetIntent = async _ =>
        {
            // Otkazivanje provjerava nedovršenu uplatu na Stripe-u, a u tom trenutku stiže i confirm iz aplikacije.
            _db.Gateway.BeforeGetIntent = null;
            confirm = Task.Run(() => _db.RunAsync(db => _db.Payments(db).ConfirmAsync(_db.ClientUser.Id, paymentId)));
            await Task.Delay(500);
        };

        var cancelled = await _db.RunAsync(db => _db.Subscriptions(db).CancelAsync(_db.ClientUser.Id, id));
        var confirmed = await confirm!;

        Assert.Equal(SubscriptionStatus.Cancelled, cancelled.Status);
        Assert.Equal(SubscriptionStatus.Cancelled, confirmed.Status);
        var subscription = await _db.SubscriptionAsync(id);
        Assert.Equal(SubscriptionStatus.Cancelled, subscription.Status);
        Assert.Equal(PaymentStatus.Refunded, Assert.Single(subscription.Payments).Status);
        Assert.Equal([("pi_paid", "refund:pi_paid")], _db.Gateway.Refunds);
        Assert.DoesNotContain(await _db.NotificationsAsync(_db.MentorUser.Id), x => x.Type == NotificationType.NewCollaborationRequest);
    }

    [Fact]
    public async Task AcceptWhileAdminCancelWaitsForRefund_IsRefused()
    {
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.AwaitingMentor);
        await _db.AddPaymentAsync(id, "pi_paid", PaymentStatus.Succeeded, "succeeded");
        Task<CollaborationRequestDto>? accept = null;
        _db.Gateway.BeforeRefund = async _ =>
        {
            _db.Gateway.BeforeRefund = null;
            accept = Task.Run(() => _db.RunAsync(db => _db.Collaboration(db).AcceptAsync(_db.MentorUser.Id, id)));
            await Task.Delay(500);
        };

        await _db.RunAsync(db => _db.Subscriptions(db).AdminCancelAsync(id, new Core.DTOs.Admin.CancelSubscriptionRequest
        {
            Reason = "Mentor ne odgovara na zahtjev"
        }));
        var error = await Assert.ThrowsAsync<ValidationException>(() => accept!);

        Assert.Equal("Zahtjev se može prihvatiti samo dok čeka odgovor mentora.", error.Message);
        var subscription = await _db.SubscriptionAsync(id);
        Assert.Equal(SubscriptionStatus.Cancelled, subscription.Status);
        Assert.Null(subscription.AcceptedAt);
        Assert.Equal(PaymentStatus.Refunded, Assert.Single(subscription.Payments).Status);
        Assert.DoesNotContain(await _db.NotificationsAsync(_db.ClientUser.Id), x => x.Type == NotificationType.RequestAccepted);
    }

    [Fact]
    public async Task ClientCancelWhileExpiryWaitsForStripe_SeesTheExpiredSubscription()
    {
        var now = DateTime.UtcNow;
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.Active, x => x.EndDate = now.AddMinutes(-1));
        // Neplaćeno produženje (mlađe od praga usklađivanja): istek ga provjerava na Stripe-u.
        await _db.AddPaymentAsync(id, "pi_renewal", PaymentStatus.Pending, "requires_payment_method", PaymentPurpose.Renewal,
            createdAt: now.AddMinutes(-1));
        Task<SubscriptionDto>? cancel = null;
        _db.Gateway.BeforeGetIntent = async _ =>
        {
            _db.Gateway.BeforeGetIntent = null;
            cancel = Task.Run(() => _db.RunAsync(db => _db.Subscriptions(db).CancelAsync(_db.ClientUser.Id, id)));
            await Task.Delay(500);
        };

        var result = await _db.RunAsync(db => _db.LifecycleProcessor(db).RunAsync(now));
        var error = await Assert.ThrowsAsync<ValidationException>(() => cancel!);

        Assert.Equal("Pretplatu možete otkazati samo dok čeka plaćanje ili dok je aktivna.", error.Message);
        Assert.Equal(1, result.Expired);
        var subscription = await _db.SubscriptionAsync(id);
        Assert.Equal(SubscriptionStatus.Expired, subscription.Status);
        Assert.Equal(PaymentStatus.Failed, Assert.Single(subscription.Payments).Status);
        Assert.Contains(await _db.NotificationsAsync(_db.ClientUser.Id), x => x.Type == NotificationType.SubscriptionExpired);
    }

    [Theory]
    [InlineData(true)]  // istovremeni zahtjev kod istog mentora → vraća se ta PendingPayment pretplata (§7)
    [InlineData(false)] // istovremeni zahtjev kod drugog mentora → 409
    public async Task CreateWhileAnotherRequestOfTheClientOpensOne_LeavesExactlyOneOpenSubscription(bool sameMentor)
    {
        var concurrentId = 0;
        var otherRequest = new BeforeFirstWriteInterceptor(async () => concurrentId = await _db.AddSubscriptionAsync(
            SubscriptionStatus.PendingPayment, mentorProfileId: sameMentor ? _db.MentorProfileId : _db.SecondMentorProfileId));
        await using var db = _db.CreateContext(otherRequest);
        var request = new CreateSubscriptionRequest
        {
            MentorProfileId = _db.MentorProfileId, Questionnaire = SubscriptionTestDatabase.QuestionnaireRequest("Novi cilj")
        };

        if (sameMentor)
        {
            var result = await _db.Subscriptions(db).CreateAsync(_db.ClientUser.Id, request);
            Assert.Equal(concurrentId, result.Id);
            Assert.Equal("Novi cilj", result.Questionnaire?.PrimaryGoal);
        }
        else
        {
            var error = await Assert.ThrowsAsync<ConflictException>(() => _db.Subscriptions(db).CreateAsync(_db.ClientUser.Id, request));
            Assert.Equal(SubscriptionService.AlreadyCollaborating, error.Message);
        }
        Assert.Equal(1, await OpenSubscriptionCountAsync());
    }

    [Fact]
    public async Task FiveParallelCreates_ReturnTheSameSubscription()
    {
        var request = new CreateSubscriptionRequest
        {
            MentorProfileId = _db.MentorProfileId, Questionnaire = SubscriptionTestDatabase.QuestionnaireRequest()
        };

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            Task.Run(() => _db.RunAsync(db => _db.Subscriptions(db).CreateAsync(_db.ClientUser.Id, request)))));

        Assert.Single(results.Select(x => x.Id).Distinct());
        Assert.Equal(1, await OpenSubscriptionCountAsync());
    }

    private Task<int> OpenSubscriptionCountAsync() => _db.RunAsync(db => db.Subscriptions
        .CountAsync(x => x.ClientProfileId == _db.ClientProfileId && QueryExtensions.OpenStatuses.Contains(x.Status)));

    /// <summary>Jednom, neposredno prije nego servis otvori transakciju za upis, izvrši "istovremeni" zahtjev (druga konekcija).</summary>
    private sealed class BeforeFirstWriteInterceptor(Func<Task> concurrentRequest) : DbTransactionInterceptor
    {
        private int _fired;

        public override async ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(DbConnection connection,
            TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _fired, 1) == 0) await concurrentRequest();
            return result;
        }
    }
}

using GoBeyond.Core.DTOs.Admin;
using GoBeyond.Core.DTOs.Common;
using GoBeyond.Core.DTOs.Plans;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Notifications;
using GoBeyond.Infrastructure.Services.Plans;
using GoBeyond.Infrastructure.StateMachineServices.TrainingPlans;
using GoBeyond.Tests.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace GoBeyond.Tests.Plans;

/// <summary>
/// "IZRADI PLAN" prihvata zahtjev koji čeka mentora. Istovremeno odbijanje ili otkazivanje istog zahtjeva se izvršava prije
/// ili poslije (zaključana pretplata): kreiranje plana tada vidi njihov rezultat (400), odnosno odbijanje vidi prihvaćen
/// zahtjev, umjesto pogrešnog 409 "Plan za ovu pretplatu već postoji.".
/// </summary>
public sealed class PlanCreationConcurrencyTests : IDisposable
{
    private readonly SubscriptionTestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private TrainingPlanService Plans(GoBeyondDbContext db) => new(db,
        new TrainingPlanStateFactory([new DraftTrainingPlanState(), new PublishedTrainingPlanState(), new ArchivedTrainingPlanState()]),
        _db.Workflow(db), new NotificationSender(db), Options.Create(_db.Lifecycle));

    private Task<PlanDetailDto> CreatePlanAsync(int subscriptionId) =>
        _db.RunAsync(db => Plans(db).CreateAsync(_db.MentorUser.Id, new CreatePlanRequest { SubscriptionId = subscriptionId }));

    [Fact]
    public async Task CreatePlanWhileRejectWaitsForRefund_IsRefusedWithoutAcceptingTheRequest()
    {
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.AwaitingMentor);
        await _db.AddPaymentAsync(id, "pi_paid", PaymentStatus.Succeeded, "succeeded");
        Task<PlanDetailDto>? create = null;
        _db.Gateway.BeforeRefund = async _ =>
        {
            _db.Gateway.BeforeRefund = null;
            // Mentor na drugom uređaju klikne "IZRADI PLAN" dok odbijanje čeka Stripe povrat.
            create = Task.Run(() => CreatePlanAsync(id));
            await Task.Delay(500);
        };

        await _db.RunAsync(db => _db.Collaboration(db).RejectAsync(_db.MentorUser.Id, id, "Trenutno nemam slobodnih termina"));
        var error = await Assert.ThrowsAsync<ValidationException>(() => create!);

        Assert.Equal("Plan se može kreirati samo za aktivnu saradnju.", error.Message);
        var subscription = await _db.SubscriptionAsync(id);
        Assert.Equal(SubscriptionStatus.Rejected, subscription.Status);
        Assert.Null(subscription.AcceptedAt);
        Assert.False(await _db.RunAsync(db => db.TrainingPlans.AnyAsync()));
        Assert.Equal([NotificationType.RequestRejected], (await _db.NotificationsAsync(_db.ClientUser.Id)).Select(x => x.Type));
    }

    [Fact]
    public async Task CreatePlanWhileAdminCancelWaitsForRefund_IsRefusedWithoutAcceptingTheRequest()
    {
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.AwaitingMentor);
        await _db.AddPaymentAsync(id, "pi_paid", PaymentStatus.Succeeded, "succeeded");
        Task<PlanDetailDto>? create = null;
        _db.Gateway.BeforeRefund = async _ =>
        {
            _db.Gateway.BeforeRefund = null;
            create = Task.Run(() => CreatePlanAsync(id));
            await Task.Delay(500);
        };

        await _db.RunAsync(db => _db.Subscriptions(db).AdminCancelAsync(id, new CancelSubscriptionRequest
        {
            Reason = "Mentor ne odgovara na zahtjev"
        }));
        var error = await Assert.ThrowsAsync<ValidationException>(() => create!);

        Assert.Equal("Plan se može kreirati samo za aktivnu saradnju.", error.Message);
        var subscription = await _db.SubscriptionAsync(id);
        Assert.Equal(SubscriptionStatus.Cancelled, subscription.Status);
        Assert.Null(subscription.AcceptedAt);
        Assert.Equal(PaymentStatus.Refunded, Assert.Single(subscription.Payments).Status);
        Assert.False(await _db.RunAsync(db => db.TrainingPlans.AnyAsync()));
        Assert.DoesNotContain(await _db.NotificationsAsync(_db.ClientUser.Id), x => x.Type == NotificationType.RequestAccepted);
    }

    [Fact]
    public async Task RejectWhilePlanCreationAcceptsTheRequest_IsRefusedWithoutRefund()
    {
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.AwaitingMentor);
        await _db.AddPaymentAsync(id, "pi_paid", PaymentStatus.Succeeded, "succeeded");
        Task<MessageResponse>? reject = null;
        var concurrentReject = new BeforeSaveInterceptor(() =>
        {
            reject = Task.Run(() => _db.RunAsync(db =>
                _db.Collaboration(db).RejectAsync(_db.MentorUser.Id, id, "Trenutno nemam slobodnih termina")));
            return Task.Delay(500);
        });

        PlanDetailDto plan;
        await using (var db = _db.CreateContext(concurrentReject))
            plan = await Plans(db).CreateAsync(_db.MentorUser.Id, new CreatePlanRequest { SubscriptionId = id });
        var error = await Assert.ThrowsAsync<ValidationException>(() => reject!);

        Assert.Equal("Zahtjev se može odbiti samo dok čeka odgovor mentora.", error.Message);
        Assert.Equal(TrainingPlanStatus.Draft, plan.Status);
        var subscription = await _db.SubscriptionAsync(id);
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.Equal(PaymentStatus.Succeeded, Assert.Single(subscription.Payments).Status);
        Assert.Empty(_db.Gateway.Refunds);
    }

    /// <summary>Jednom, neposredno prije snimanja, pokreće "istovremenu" operaciju na drugoj konekciji.</summary>
    private sealed class BeforeSaveInterceptor(Func<Task> concurrentOperation) : SaveChangesInterceptor
    {
        private bool _fired;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_fired)
            {
                _fired = true;
                await concurrentOperation();
            }
            return result;
        }
    }
}

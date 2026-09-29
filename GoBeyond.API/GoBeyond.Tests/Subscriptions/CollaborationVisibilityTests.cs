using GoBeyond.Core.DTOs.Plans;
using GoBeyond.Core.DTOs.Subscriptions;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Common;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Tests.Subscriptions;

/// <summary>
/// Mentor ne vidi pretplate koje klijent nikad nije platio (zahtjev nikad nije stigao do njega), bez obzira na
/// trenutni status i filter. Korisnik kojem je uloga promijenjena iz Mentor nije dostupan za plaćanje i obnovu.
/// </summary>
public sealed class CollaborationVisibilityTests : IDisposable
{
    private readonly SubscriptionTestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task NeverPaidSubscriptions_AreInvisibleToTheMentor()
    {
        var paid = await _db.AddSubscriptionAsync(SubscriptionStatus.Active);
        var cancelledUnpaid = await _db.AddSubscriptionAsync(SubscriptionStatus.Cancelled, x => x.PaidAt = null,
            clientProfileId: _db.SecondClientProfileId);
        var pendingUnpaid = await _db.AddSubscriptionAsync(SubscriptionStatus.PendingPayment, clientProfileId: _db.SecondClientProfileId);

        async Task<List<int>> SubscriberIds(SubscriptionStatus? status) =>
            (await _db.RunAsync(db => _db.Collaboration(db).GetSubscribersAsync(_db.MentorUser.Id,
                new SubscriptionSearchObject { Status = status }))).Select(x => x.SubscriptionId).ToList();

        Assert.Equal([paid], await SubscriberIds(null));
        Assert.Empty(await SubscriberIds(SubscriptionStatus.Cancelled));
        Assert.Empty(await SubscriberIds(SubscriptionStatus.PendingPayment));
        foreach (var id in new[] { cancelledUnpaid, pendingUnpaid })
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _db.RunAsync(db => _db.Collaboration(db).GetRequestAsync(_db.MentorUser.Id, id)));
            await Assert.ThrowsAsync<NotFoundException>(() => _db.RunAsync(db => _db.Collaboration(db).GetSubscriberAsync(_db.MentorUser.Id, id)));
        }
        Assert.Equal(paid, (await _db.RunAsync(db => _db.Collaboration(db).GetSubscriberAsync(_db.MentorUser.Id, paid))).SubscriptionId);
    }

    [Fact]
    public async Task CreatingAPlanForANeverPaidSubscription_AnswersAsForANonexistentOne()
    {
        var pendingUnpaid = await _db.AddSubscriptionAsync(SubscriptionStatus.PendingPayment);
        var cancelledUnpaid = await _db.AddSubscriptionAsync(SubscriptionStatus.Cancelled, x => x.PaidAt = null,
            clientProfileId: _db.SecondClientProfileId);
        var otherMentors = await _db.AddSubscriptionAsync(SubscriptionStatus.Active, mentorProfileId: _db.SecondMentorProfileId,
            clientProfileId: _db.SecondClientProfileId);
        // Kontrola: plaćen i zatim otkazan zahtjev je mentor vidio, pa dobija razlog odbijanja.
        var cancelledPaid = await _db.AddSubscriptionAsync(SubscriptionStatus.Cancelled, clientProfileId: _db.SecondClientProfileId);

        Task<PlanDetailDto> CreatePlanAsync(int subscriptionId) => _db.RunAsync(db =>
            _db.Plans(db).CreateAsync(_db.MentorUser.Id, new CreatePlanRequest { SubscriptionId = subscriptionId }));

        foreach (var id in new[] { pendingUnpaid, cancelledUnpaid, otherMentors, 999_999 })
        {
            var notFound = await Assert.ThrowsAsync<NotFoundException>(() => CreatePlanAsync(id));
            Assert.Equal(DomainTexts.SubscriptionNotFound, notFound.Message);
        }
        var invalid = await Assert.ThrowsAsync<ValidationException>(() => CreatePlanAsync(cancelledPaid));
        Assert.Equal("Plan se može kreirati samo za aktivnu saradnju.", invalid.Message);
        Assert.False(await _db.RunAsync(db => db.TrainingPlans.AnyAsync()));
        Assert.Equal(SubscriptionStatus.PendingPayment, (await _db.SubscriptionAsync(pendingUnpaid)).Status);
    }

    [Fact]
    public async Task MentorWhoseRoleWasChanged_IsNotAvailableForPaymentOrRenewal()
    {
        var active = await _db.AddSubscriptionAsync(SubscriptionStatus.Active);
        var pending = await _db.AddSubscriptionAsync(SubscriptionStatus.PendingPayment, mentorProfileId: _db.SecondMentorProfileId,
            clientProfileId: _db.SecondClientProfileId);
        await _db.RunAsync(async db =>
        {
            // Administrator je promijenio ulogu (MentorProfile ostaje Approved).
            await db.Users.Where(x => x.Id == _db.MentorUser.Id || x.Id == _db.SecondMentorUser.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(u => u.Role, UserRole.Client));
        });

        var error = await Assert.ThrowsAsync<ValidationException>(() => _db.RunAsync(db =>
            _db.Payments(db).CreateIntentAsync(_db.SecondClientUser.Id, new CreatePaymentIntentRequest { SubscriptionId = pending })));
        var mine = await _db.RunAsync(db => _db.Subscriptions(db).GetMineAsync(_db.ClientUser.Id, null));

        Assert.Equal("Mentor trenutno nije dostupan, pa plaćanje nije moguće.", error.Message);
        Assert.Empty(_db.Gateway.CreateCalls);
        Assert.False(Assert.Single(mine, x => x.Id == active).CanRenew);
    }
}

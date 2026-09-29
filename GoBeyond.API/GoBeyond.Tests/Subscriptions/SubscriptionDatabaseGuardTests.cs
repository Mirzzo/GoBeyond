using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Tests.Subscriptions;

/// <summary>
/// Zaštite u bazi: jedinstveni indeks (najviše jedna otvorena pretplata po klijentu) i status kao concurrency token
/// (upis nad zastarjelim statusom ne uspijeva) - vrijede i za kod koji ne koristi SubscriptionLocks.
/// </summary>
public sealed class SubscriptionDatabaseGuardTests : IDisposable
{
    private readonly SubscriptionTestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Theory]
    [InlineData(SubscriptionStatus.PendingPayment)]
    [InlineData(SubscriptionStatus.AwaitingMentor)]
    [InlineData(SubscriptionStatus.Active)]
    public async Task SecondOpenSubscriptionOfAClient_IsRejectedByTheDatabase(SubscriptionStatus secondStatus)
    {
        await _db.AddSubscriptionAsync(SubscriptionStatus.Active);

        await Assert.ThrowsAsync<DbUpdateException>(() => _db.AddSubscriptionAsync(secondStatus, mentorProfileId: _db.SecondMentorProfileId));
    }

    [Fact]
    public async Task ClosedSubscriptions_DoNotCountAsOpen()
    {
        foreach (var status in new[] { SubscriptionStatus.Rejected, SubscriptionStatus.Cancelled, SubscriptionStatus.Expired })
            await _db.AddSubscriptionAsync(status);

        await _db.AddSubscriptionAsync(SubscriptionStatus.PendingPayment);

        Assert.Equal(4, await _db.RunAsync(db => db.Subscriptions.CountAsync(x => x.ClientProfileId == _db.ClientProfileId)));
    }

    [Fact]
    public async Task WriteBasedOnAStaleStatus_FailsInsteadOfOverwritingTheTransition()
    {
        // Npr. automatsko prihvatanje kroz "IZRADI PLAN" pročita AwaitingMentor, a zahtjev je u međuvremenu odbijen.
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.AwaitingMentor);
        await using var stale = _db.CreateContext();
        var subscription = await stale.Subscriptions.SingleAsync(x => x.Id == id);
        await _db.RunAsync(db => db.Subscriptions.Where(x => x.Id == id)
            .ExecuteUpdateAsync(x => x.SetProperty(s => s.Status, SubscriptionStatus.Rejected)));

        subscription.Status = SubscriptionStatus.Active;
        subscription.AcceptedAt = DateTime.UtcNow;

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
        var current = await _db.SubscriptionAsync(id);
        Assert.Equal(SubscriptionStatus.Rejected, current.Status);
        Assert.Null(current.AcceptedAt);
    }
}

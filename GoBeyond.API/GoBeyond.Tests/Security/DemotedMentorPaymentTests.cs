using GoBeyond.Core.DTOs.Subscriptions;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Infrastructure.Services.Payments;
using GoBeyond.Tests.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Tests.Security;

/// <summary>
/// Odbrana u dubinu (uz RoleChangeSubscriptionRaceTests): ako PendingPayment ipak ostane kod korisnika koji više nije mentor,
/// ne može se platiti, a klijent je može otkazati.
/// </summary>
public sealed class DemotedMentorPaymentTests : IDisposable
{
    private readonly SubscriptionTestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task PendingPaymentForDemotedMentor_CannotBePaidAndClientCanCancelIt()
    {
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.PendingPayment);
        await _db.RunAsync(db => db.Users.Where(x => x.Id == _db.MentorUser.Id)
            .ExecuteUpdateAsync(x => x.SetProperty(u => u.Role, UserRole.Client)));

        var error = await Assert.ThrowsAsync<ValidationException>(() => _db.RunAsync(db =>
            _db.Payments(db).CreateIntentAsync(_db.ClientUser.Id, new CreatePaymentIntentRequest { SubscriptionId = id })));
        Assert.Equal(PaymentService.MentorUnavailable, error.Message);
        Assert.Empty(_db.Gateway.CreateCalls);

        var cancelled = await _db.RunAsync(db => _db.Subscriptions(db).CancelAsync(_db.ClientUser.Id, id));
        Assert.Equal(SubscriptionStatus.Cancelled, cancelled.Status);
    }
}

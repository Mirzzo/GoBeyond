using System.Net;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Tests.Subscriptions;

/// <summary>
/// DELETE /api/admin/users/{id}: klijent saznaje da mu je uplata za neprihvaćen zahtjev vraćena, a mentor dobija
/// obavijest samo o zahtjevu koji je vidio (ne o neplaćenoj PendingPayment pretplati).
/// </summary>
public sealed class UserDeletionSubscriptionTests
{
    [Fact]
    public async Task DeletingAMentor_TellsTheClientAboutTheRefundOfTheRequestAwaitingMentor()
    {
        using var factory = new GoBeyondApiFactory();
        var admin = factory.ClientFor(TestUsers.Admin);
        var subscriptionId = AddSubscription(factory, SubscriptionStatus.AwaitingMentor, paidAmount: 20.00m);

        var response = await admin.DeleteAsync($"/api/admin/users/{UserId(factory, TestUsers.MentorA)}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var clientNotification = Assert.Single(Notifications(factory, TestUsers.Client));
        Assert.Equal("Saradnja sa mentorom Test mentor.a je prekinuta. Razlog: Mentor je uklonjen sa platforme. " +
                     "Uplaćeni iznos od 20,00 USD biće vraćen na vašu karticu.", clientNotification.Body);
        Assert.Equal(PaymentStatus.Refunded, factory.Query(db => db.Payments.AsNoTracking().Single(x => x.SubscriptionId == subscriptionId)).Status);
    }

    [Fact]
    public async Task DeletingAClient_DoesNotNotifyTheMentorAboutANeverPaidSubscription()
    {
        using var factory = new GoBeyondApiFactory();
        var admin = factory.ClientFor(TestUsers.Admin);
        var subscriptionId = AddSubscription(factory, SubscriptionStatus.PendingPayment, paidAmount: null);

        var response = await admin.DeleteAsync($"/api/admin/users/{UserId(factory, TestUsers.Client)}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(SubscriptionStatus.Cancelled, factory.Query(db => db.Subscriptions.AsNoTracking().Single(x => x.Id == subscriptionId)).Status);
        Assert.Empty(Notifications(factory, TestUsers.MentorA));
    }

    [Fact]
    public async Task DeletingAClient_NotifiesTheMentorOfAnActiveCollaboration()
    {
        using var factory = new GoBeyondApiFactory();
        var admin = factory.ClientFor(TestUsers.Admin);
        AddSubscription(factory, SubscriptionStatus.Active, paidAmount: 20.00m);

        await admin.DeleteAsync($"/api/admin/users/{UserId(factory, TestUsers.Client)}");

        var mentorNotification = Assert.Single(Notifications(factory, TestUsers.MentorA));
        Assert.Equal("Saradnja sa klijentom Test client je prekinuta. Razlog: Klijent je uklonjen sa platforme.", mentorNotification.Body);
    }

    private static int UserId(GoBeyondApiFactory factory, string username) =>
        factory.Query(db => db.Users.AsNoTracking().Single(x => x.Username == username).Id);

    private static List<Notification> Notifications(GoBeyondApiFactory factory, string username) =>
        factory.Query(db => db.Notifications.AsNoTracking().Where(x => x.User.Username == username).OrderBy(x => x.Id).ToList());

    private static int AddSubscription(GoBeyondApiFactory factory, SubscriptionStatus status, decimal? paidAmount) => factory.Query(db =>
    {
        var now = DateTime.UtcNow;
        var subscription = new Subscription
        {
            ClientProfileId = db.ClientProfiles.Single(x => x.User.Username == TestUsers.Client).Id,
            MentorProfileId = db.MentorProfiles.Single(x => x.User.Username == TestUsers.MentorA).Id,
            Status = status, Price = 20.00m, Currency = "usd", CreatedAt = now.AddDays(-1),
            PaidAt = paidAmount is null ? null : now.AddHours(-20),
            AcceptedAt = status == SubscriptionStatus.Active ? now.AddHours(-19) : null,
            StartDate = status == SubscriptionStatus.Active ? now.AddHours(-19) : null,
            EndDate = status == SubscriptionStatus.Active ? now.AddDays(29) : null
        };
        if (paidAmount is { } amount)
            subscription.Payments.Add(new Payment
            {
                Amount = amount, Currency = "usd", StripePaymentIntentId = $"seed_pi_{Guid.NewGuid():N}", Purpose = PaymentPurpose.Initial,
                Status = PaymentStatus.Succeeded, CreatedAt = now.AddHours(-20), PaidAt = now.AddHours(-20)
            });
        db.Subscriptions.Add(subscription);
        db.SaveChanges();
        return subscription.Id;
    });
}

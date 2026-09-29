using System.Text.RegularExpressions;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Infrastructure.Services.Subscriptions;
using GoBeyond.Tests.Payments;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GoBeyond.Tests.Subscriptions;

/// <summary>Iznosi u obavijestima i emailovima imaju decimalni zarez ("39,99 USD"), kao u aplikacijama i demo podacima.</summary>
public sealed class MoneyTextTests
{
    private readonly FakePaymentGateway _gateway = new();
    private readonly RecordingNotificationSender _notifications = new();
    private readonly SubscriptionWorkflow _workflow;

    public MoneyTextTests()
    {
        _workflow = new SubscriptionWorkflow(_notifications, _gateway,
            Options.Create(new LifecycleOptions { SubscriptionPeriodDays = 30 }), NullLogger<SubscriptionWorkflow>.Instance);
    }

    [Theory]
    [InlineData(39.99, "usd", "39,99 USD")]
    [InlineData(20, "usd", "20,00 USD")]
    [InlineData(0.5, "usd", "0,50 USD")]
    [InlineData(1234.5, "eur", "1234,50 EUR")]
    public void Money_UsesTheDecimalComma(decimal amount, string currency, string expected) =>
        Assert.Equal(expected, DomainTexts.Money(amount, currency));

    [Fact]
    public async Task InitialPaymentText_UsesTheDecimalComma()
    {
        var payment = AddPayment(NewSubscription(SubscriptionStatus.PendingPayment), PaymentStatus.Pending);

        await _workflow.ApplySuccessfulPaymentAsync(payment, DateTime.UtcNow, CancellationToken.None);

        var client = _notifications.Sent.Single(x => x.Type == NotificationType.PaymentSucceeded);
        Assert.Equal("Uplata od 39,99 USD je uspješna. Mentor Selma Delić će uskoro pregledati vaš zahtjev.", client.Body);
        AssertNoDecimalPointAmounts();
    }

    [Fact]
    public async Task UnappliedPaymentRefundTexts_UseTheDecimalComma()
    {
        // Uplata stiže za već otkazanu pretplatu: vraća se odmah, a ako Stripe odbije povrat, povrat se ponavlja kasnije.
        var refunded = AddPayment(NewSubscription(SubscriptionStatus.Cancelled), PaymentStatus.Pending);
        await _workflow.ApplySuccessfulPaymentAsync(refunded, DateTime.UtcNow, CancellationToken.None);
        _gateway.FailRefunds = true;
        var pending = AddPayment(NewSubscription(SubscriptionStatus.Cancelled), PaymentStatus.Pending);
        await _workflow.ApplySuccessfulPaymentAsync(pending, DateTime.UtcNow, CancellationToken.None);
        _gateway.FailRefunds = false;
        await _workflow.RetryPendingRefundAsync(pending, DateTime.UtcNow, CancellationToken.None);

        var texts = _notifications.Sent.Where(x => x.Type == NotificationType.PaymentRefunded).Select(x => x.Body).ToList();
        Assert.Equal(3, texts.Count);
        Assert.StartsWith("Uplata od 39,99 USD za saradnju sa mentorom Selma Delić nije mogla biti primijenjena", texts[0]);
        Assert.StartsWith("Uplata od 39,99 USD za saradnju sa mentorom Selma Delić nije mogla biti primijenjena", texts[1]);
        Assert.Equal("Iznos od 39,99 USD je vraćen na vašu karticu.", texts[2]);
        AssertNoDecimalPointAmounts();
    }

    private void AssertNoDecimalPointAmounts()
    {
        foreach (var (_, _, title, body, _) in _notifications.Sent)
        {
            Assert.DoesNotMatch(new Regex(@"\d\.\d{2} [A-Z]{3}"), body);
            Assert.DoesNotMatch(new Regex(@"\d\.\d{2} [A-Z]{3}"), title);
        }
    }

    private static Subscription NewSubscription(SubscriptionStatus status) => new()
    {
        Id = 5,
        Status = status,
        Price = 39.99m,
        Currency = "usd",
        ClientProfile = new ClientProfile { User = new User { Id = 11, FirstName = "Nađa", LastName = "Škrijelj", Email = "nadja@test.ba" } },
        MentorProfile = new MentorProfile { User = new User { Id = 22, FirstName = "Selma", LastName = "Delić", Email = "selma@test.ba" } }
    };

    private static Payment AddPayment(Subscription subscription, PaymentStatus status)
    {
        var payment = new Payment
        {
            Id = 7, SubscriptionId = subscription.Id, Subscription = subscription, Amount = 39.99m, Currency = "usd",
            StripePaymentIntentId = "pi_money", Purpose = PaymentPurpose.Initial, Status = status
        };
        subscription.Payments.Add(payment);
        return payment;
    }
}

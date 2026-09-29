using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Tests.Subscriptions;

namespace GoBeyond.Tests.Payments;

/// <summary>Ispravno potpisan, ali neispravan webhook payload: 400 (nije JSON) ili 200 bez obrade (neočekivan oblik), nikad 500.</summary>
public sealed class WebhookPayloadTests : IDisposable
{
    private readonly SubscriptionTestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Theory]
    [InlineData("this is not json")]
    [InlineData("{\"type\":\"payment_intent.succeeded\",")]
    [InlineData("")]
    public async Task NotJson_IsRejectedAs400(string payload)
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => HandleAsync(payload));

        Assert.Equal("Neispravan Stripe webhook payload.", error.Message);
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("\"text\"")]
    [InlineData("{\"type\":\"payment_intent.succeeded\",\"data\":{\"object\":\"string\"}}")]
    [InlineData("{\"type\":\"payment_intent.succeeded\",\"data\":[]}")]
    [InlineData("{\"type\":5,\"data\":{\"object\":{\"id\":\"pi_paid\"}}}")]
    [InlineData("{\"type\":\"payment_intent.succeeded\",\"data\":{\"object\":{\"id\":5,\"metadata\":[]}}}")]
    public async Task UnexpectedShape_IsIgnored(string payload)
    {
        var id = await _db.AddSubscriptionAsync(SubscriptionStatus.PendingPayment);
        await _db.AddPaymentAsync(id, "pi_paid", PaymentStatus.Pending, "succeeded");

        await HandleAsync(payload);

        var subscription = await _db.SubscriptionAsync(id);
        Assert.Equal(SubscriptionStatus.PendingPayment, subscription.Status);
        Assert.Equal(PaymentStatus.Pending, Assert.Single(subscription.Payments).Status);
    }

    private Task HandleAsync(string payload) => _db.RunAsync(db => _db.Payments(db).HandleWebhookAsync(payload, "t=1,v1=x"));
}

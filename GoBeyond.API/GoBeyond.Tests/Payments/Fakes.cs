using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Infrastructure.Services.Notifications;
using GoBeyond.Infrastructure.Services.Payments;

namespace GoBeyond.Tests.Payments;

/// <summary>Lažni Stripe gateway: bilježi pozive i vraća unaprijed zadane PaymentIntent-e.</summary>
internal sealed class FakePaymentGateway : IPaymentGateway
{
    private int _counter;

    public bool IsConfigured { get; set; } = true;
    public string PublishableKey => "pk_test_fake";
    public string Currency => "usd";
    public bool FailRefunds { get; set; }
    public bool WebhookSignatureValid { get; set; } = true;

    public Dictionary<string, PaymentIntentInfo> Intents { get; } = new();
    public List<(decimal Amount, IReadOnlyDictionary<string, string> Metadata, string IdempotencyKey)> CreateCalls { get; } = [];
    public List<(string PaymentIntentId, string IdempotencyKey)> Refunds { get; } = [];

    public Task<PaymentIntentInfo> CreatePaymentIntentAsync(decimal amount, string receiptEmail, IReadOnlyDictionary<string, string> metadata,
        string idempotencyKey, CancellationToken cancellationToken)
    {
        CreateCalls.Add((amount, metadata, idempotencyKey));
        _counter++;
        var intent = new PaymentIntentInfo($"pi_new_{_counter}", $"pi_new_{_counter}_secret", "requires_payment_method",
            StripePaymentGateway.ToMinorUnits(amount), Currency, metadata);
        Intents[intent.Id] = intent;
        return Task.FromResult(intent);
    }

    public Task<PaymentIntentInfo> GetPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken) =>
        Task.FromResult(Intents[paymentIntentId]);

    public Task RefundAsync(string paymentIntentId, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (FailRefunds) throw new ValidationException("Stripe nije prihvatio zahtjev.");
        Refunds.Add((paymentIntentId, idempotencyKey));
        return Task.CompletedTask;
    }

    public bool VerifyWebhookSignature(string payload, string signatureHeader, DateTimeOffset now) => WebhookSignatureValid;

    public static PaymentIntentInfo Intent(string id, string status, int subscriptionId, PaymentPurpose purpose, decimal amount,
        string currency = "usd") =>
        new(id, id + "_secret", status, StripePaymentGateway.ToMinorUnits(amount), currency,
            new Dictionary<string, string>
            {
                [PaymentIntentInfo.MetadataSubscriptionId] = subscriptionId.ToString(),
                [PaymentIntentInfo.MetadataPurpose] = purpose.ToString()
            });
}

/// <summary>Bilježi obavijesti i emailove umjesto upisa u bazu.</summary>
internal sealed class RecordingNotificationSender : INotificationSender
{
    public List<(int UserId, NotificationType Type, string Title, string Body, bool Email)> Sent { get; } = [];

    public Notification Notify(User recipient, NotificationType type, string title, string body, bool sendEmail)
    {
        Sent.Add((recipient.Id, type, title, body, sendEmail));
        return new Notification { UserId = recipient.Id, Type = type, Title = title, Body = body };
    }

    public void QueueEmail(User recipient, string eventType, string subject, string body)
    {
    }
}

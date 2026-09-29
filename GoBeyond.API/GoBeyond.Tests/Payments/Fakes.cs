using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Infrastructure.Services.Notifications;
using GoBeyond.Infrastructure.Services.Payments;

namespace GoBeyond.Tests.Payments;

/// <summary>
/// Lažni Stripe gateway: bilježi pozive i vraća unaprijed zadane PaymentIntent-e. Siguran za istovremene pozive
/// (testovi konkurentnosti), a kukice Before* omogućavaju da test ubaci istovremenu operaciju ili grešku u tačno
/// određenom trenutku Stripe poziva.
/// </summary>
internal sealed class FakePaymentGateway : IPaymentGateway
{
    private readonly object _sync = new();
    private int _counter;

    public bool IsConfigured { get; set; } = true;
    public string PublishableKey => "pk_test_fake";
    public string Currency => "usd";
    public bool FailRefunds { get; set; }
    public bool WebhookSignatureValid { get; set; } = true;

    /// <summary>Poziva se prije kreiranja PaymentIntent-a; može čekati (sporiji Stripe) ili baciti izuzetak.</summary>
    public Func<Task>? BeforeCreateIntent { get; set; }

    /// <summary>Poziva se prije svakog GET PaymentIntent-a (id); može baciti izuzetak ili čekati.</summary>
    public Func<string, Task>? BeforeGetIntent { get; set; }

    /// <summary>Poziva se prije otkazivanja PaymentIntent-a (npr. da ga test u međuvremenu "plati").</summary>
    public Action<string>? BeforeCancelIntent { get; set; }

    /// <summary>Poziva se prije svakog povrata (id PaymentIntent-a); može baciti izuzetak ili čekati.</summary>
    public Func<string, Task>? BeforeRefund { get; set; }

    public Dictionary<string, PaymentIntentInfo> Intents { get; } = new();
    public List<(decimal Amount, IReadOnlyDictionary<string, string> Metadata, string IdempotencyKey)> CreateCalls { get; } = [];
    public List<(string PaymentIntentId, string IdempotencyKey)> Refunds { get; } = [];
    public List<(string PaymentIntentId, string IdempotencyKey)> Cancels { get; } = [];

    public async Task<PaymentIntentInfo> CreatePaymentIntentAsync(decimal amount, string receiptEmail, IReadOnlyDictionary<string, string> metadata,
        string idempotencyKey, CancellationToken cancellationToken)
    {
        if (BeforeCreateIntent is { } hook) await hook();
        lock (_sync)
        {
            CreateCalls.Add((amount, metadata, idempotencyKey));
            _counter++;
            var intent = new PaymentIntentInfo($"pi_new_{_counter}", $"pi_new_{_counter}_secret", "requires_payment_method",
                StripePaymentGateway.ToMinorUnits(amount), Currency, metadata);
            Intents[intent.Id] = intent;
            return intent;
        }
    }

    public async Task<PaymentIntentInfo> GetPaymentIntentAsync(string paymentIntentId, CancellationToken cancellationToken)
    {
        if (BeforeGetIntent is { } hook) await hook(paymentIntentId);
        lock (_sync) return Intents[paymentIntentId];
    }

    /// <summary>
    /// Kao Stripe: PaymentIntent koji se još može platiti postaje "canceled"; za već plaćen (ili otkazan) vraća se
    /// njegovo trenutno stanje (pravi gateway tada čita PaymentIntent nakon greške payment_intent_unexpected_state).
    /// </summary>
    public Task<PaymentIntentInfo> CancelPaymentIntentAsync(string paymentIntentId, string idempotencyKey, CancellationToken cancellationToken)
    {
        BeforeCancelIntent?.Invoke(paymentIntentId);
        lock (_sync)
        {
            Cancels.Add((paymentIntentId, idempotencyKey));
            var intent = Intents[paymentIntentId];
            if (PaymentService.ReusableIntentStatuses.Contains(intent.Status))
                Intents[paymentIntentId] = intent = intent with { Status = "canceled" };
            return Task.FromResult(intent);
        }
    }

    public async Task RefundAsync(string paymentIntentId, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (BeforeRefund is { } hook) await hook(paymentIntentId);
        if (FailRefunds) throw new ValidationException("Stripe nije prihvatio zahtjev.");
        lock (_sync) Refunds.Add((paymentIntentId, idempotencyKey));
    }

    public bool VerifyWebhookSignature(string payload, string signatureHeader, DateTimeOffset now) => WebhookSignatureValid;

    public void SetStatus(string paymentIntentId, string status)
    {
        lock (_sync) Intents[paymentIntentId] = Intents[paymentIntentId] with { Status = status };
    }

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

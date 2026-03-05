using GoBeyond.Core.Enums;

namespace GoBeyond.Core.Entities;

public class Payment : BaseEntity
{
    public int SubscriptionId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "usd";
    public string StripePaymentIntentId { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public Subscription Subscription { get; set; } = null!;
}

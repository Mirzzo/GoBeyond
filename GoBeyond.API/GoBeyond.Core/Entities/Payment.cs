using GoBeyond.Core.Enums;

namespace GoBeyond.Core.Entities;

public class Payment : BaseEntity
{
    public int SubscriptionId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string StripePaymentIntentId { get; set; } = string.Empty;
    public PaymentPurpose Purpose { get; set; } = PaymentPurpose.Initial;
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
    public DateTime? RefundedAt { get; set; }

    public Subscription Subscription { get; set; } = null!;
}

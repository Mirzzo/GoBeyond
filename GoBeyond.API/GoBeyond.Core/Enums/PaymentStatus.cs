namespace GoBeyond.Core.Enums;

public enum PaymentStatus
{
    Pending = 1,
    Succeeded = 2,
    Failed = 3,
    Refunded = 4,

    /// <summary>
    /// Uplata je naplaćena, ali se ne može primijeniti (npr. stigla je za otkazanu pretplatu) i mora se vratiti;
    /// Stripe povrat još nije uspio. SubscriptionLifecycleService automatski ponavlja povrat.
    /// </summary>
    RefundPending = 5
}

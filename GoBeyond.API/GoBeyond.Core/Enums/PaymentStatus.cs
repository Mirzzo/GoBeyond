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
    RefundPending = 5,

    /// <summary>
    /// Uplatu je trebalo vratiti, ali je naplata osporena kod banke klijenta (Stripe dispute), pa Stripe povrat ne
    /// dozvoljava. O novcu odlučuje spor; ishod i eventualni ručni povrat se rješavaju na Stripe-u, a automatski povrat
    /// se više ne pokušava.
    /// </summary>
    Disputed = 6
}

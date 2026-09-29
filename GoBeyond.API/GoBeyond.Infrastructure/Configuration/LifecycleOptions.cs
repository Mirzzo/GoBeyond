namespace GoBeyond.Infrastructure.Configuration;

public sealed class LifecycleOptions
{
    public const string SectionName = "Lifecycle";

    /// <summary>Koliko često hosted servis provjerava pretplate.</summary>
    public int IntervalSeconds { get; set; }

    /// <summary>Trajanje jednog perioda pretplate (i produženja).</summary>
    public int SubscriptionPeriodDays { get; set; }

    public int ExpiringReminderDays { get; set; }
    public int PlanMissingAfterHours { get; set; }
    public int PlanMissingRepeatHours { get; set; }
    public int InactivityDays { get; set; }
    public int InactivityRepeatDays { get; set; }

    /// <summary>Najviše jedna PlanUpdated obavijest po planu u ovom intervalu.</summary>
    public int PlanUpdateNotificationThrottleMinutes { get; set; }

    /// <summary>
    /// Usklađivanje uplata bez webhook-a: Pending uplata starija od ovoliko minuta provjerava se na Stripe-u
    /// (normalan tok PaymentSheet → confirm traje nekoliko sekundi).
    /// </summary>
    public int PaymentReconcileAfterMinutes { get; set; }

    /// <summary>Pending uplate starije od ovoliko sati se više ne provjeravaju (napušteni PaymentIntent-i se ne ispituju vječno).</summary>
    public int PaymentReconcileWindowHours { get; set; }

    /// <summary>
    /// Vremenska zona platforme za datume u tekstovima obavijesti i emailova (npr. "traje do 29.11.2026."), da odgovaraju
    /// datumima u aplikacijama. IANA naziv; ako ga sistem ne prepozna, koristi se "Central European Standard Time".
    /// </summary>
    public string TimeZoneId { get; set; } = "Europe/Sarajevo";

    /// <summary>Kašnjenje prvog ciklusa nakon pokretanja API-ja (migracija i seed baze se završe prije toga).</summary>
    public int StartupDelaySeconds { get; set; } = 20;
}

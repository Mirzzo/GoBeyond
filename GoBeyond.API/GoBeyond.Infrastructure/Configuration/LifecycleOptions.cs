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
}

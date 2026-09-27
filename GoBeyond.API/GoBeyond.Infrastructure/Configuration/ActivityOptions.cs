namespace GoBeyond.Infrastructure.Configuration;

public sealed class ActivityOptions
{
    public const string SectionName = "Activity";

    /// <summary>Najviše sekundi koje se pripisuju po jednom heartbeat pozivu.</summary>
    public int MaxSecondsPerHeartbeat { get; set; }
}

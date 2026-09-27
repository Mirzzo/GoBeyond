namespace GoBeyond.Core.Entities;

public class Questionnaire : BaseEntity
{
    public int SubscriptionId { get; set; }
    public string PrimaryGoal { get; set; } = string.Empty;
    public string TimeCommitment { get; set; } = string.Empty;
    public string HealthIssues { get; set; } = string.Empty;
    public string Medications { get; set; } = string.Empty;
    public string WeeklySessions { get; set; } = string.Empty;
    public string OutsideActivity { get; set; } = string.Empty;

    public Subscription Subscription { get; set; } = null!;
}

namespace GoBeyond.Core.Entities;

public class Questionnaire : BaseEntity
{
    public int ClientProfileId { get; set; }
    public int SubscriptionId { get; set; }
    public string PrimaryGoal { get; set; } = string.Empty;
    public string TimeCommitment { get; set; } = string.Empty;
    public string HealthIssues { get; set; } = string.Empty;
    public string Medications { get; set; } = string.Empty;
    public string WeeklyAvailability { get; set; } = string.Empty;
    public string PhysicalActivityLevel { get; set; } = string.Empty;

    public ClientProfile ClientProfile { get; set; } = null!;
    public Subscription Subscription { get; set; } = null!;
}

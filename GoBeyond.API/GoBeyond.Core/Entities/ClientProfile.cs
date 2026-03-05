namespace GoBeyond.Core.Entities;

public class ClientProfile : BaseEntity
{
    public int UserId { get; set; }
    public decimal Weight { get; set; }
    public decimal Height { get; set; }
    public int Age { get; set; }
    public string FitnessLevel { get; set; } = string.Empty;

    public User User { get; set; } = null!;
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public ICollection<Questionnaire> Questionnaires { get; set; } = new List<Questionnaire>();
    public ICollection<TrainingPlan> TrainingPlans { get; set; } = new List<TrainingPlan>();
    public ICollection<ProgressEntry> ProgressEntries { get; set; } = new List<ProgressEntry>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}

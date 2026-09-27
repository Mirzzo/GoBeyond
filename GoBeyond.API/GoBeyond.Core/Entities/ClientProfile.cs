namespace GoBeyond.Core.Entities;

public class ClientProfile : BaseEntity
{
    public int UserId { get; set; }
    public decimal WeightKg { get; set; }
    public decimal HeightCm { get; set; }
    public int FitnessLevelId { get; set; }
    public int TrainingExperienceYears { get; set; }
    public int FitnessGoalId { get; set; }
    public string? GoalDescription { get; set; }
    public int? PreferredTrainingTypeId { get; set; }

    public User User { get; set; } = null!;
    public FitnessLevel FitnessLevel { get; set; } = null!;
    public FitnessGoal FitnessGoal { get; set; } = null!;
    public TrainingType? PreferredTrainingType { get; set; }
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public ICollection<ProgressEntry> ProgressEntries { get; set; } = new List<ProgressEntry>();
    public ICollection<TrainingSession> TrainingSessions { get; set; } = new List<TrainingSession>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
}

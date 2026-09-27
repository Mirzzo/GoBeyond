namespace GoBeyond.Core.Entities;

public class ProgressEntry : BaseEntity
{
    public int ClientProfileId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string? PhotoUrl { get; set; }
    public decimal WeightKg { get; set; }

    /// <summary>"Obimi".</summary>
    public string Measurements { get; set; } = string.Empty;

    /// <summary>"Snaga".</summary>
    public string Strength { get; set; } = string.Empty;

    /// <summary>"Kondicija".</summary>
    public string Conditioning { get; set; } = string.Empty;

    public int? TrainingPlanId { get; set; }

    /// <summary>JSON snapshot plana koji je klijent koristio kad je unio napredak ("HISTORIJA PLANA").</summary>
    public string? PlanSnapshotJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ClientProfile ClientProfile { get; set; } = null!;
    public TrainingPlan? TrainingPlan { get; set; }
}

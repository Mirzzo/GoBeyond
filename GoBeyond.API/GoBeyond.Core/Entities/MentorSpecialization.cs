namespace GoBeyond.Core.Entities;

/// <summary>Čista M2M međutabela MentorProfile - FitnessGoal (kompozitni ključ, bez dodatnih atributa).</summary>
public class MentorSpecialization
{
    public int MentorProfileId { get; set; }
    public int FitnessGoalId { get; set; }

    public MentorProfile MentorProfile { get; set; } = null!;
    public FitnessGoal FitnessGoal { get; set; } = null!;
}

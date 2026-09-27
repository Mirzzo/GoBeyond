namespace GoBeyond.Core.Entities;

/// <summary>Šifarnik: fitness cilj klijenta, ujedno i specijalizacija mentora.</summary>
public class FitnessGoal : BaseEntity, INamedEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

namespace GoBeyond.Core.Entities;

/// <summary>Šifarnik: vrsta treninga (Weightlifting, Calisthenics, Hybrid...).</summary>
public class TrainingType : BaseEntity, INamedEntity
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

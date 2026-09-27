namespace GoBeyond.Core.Entities;

/// <summary>Šifarnik: nivo fizičke spreme. SortOrder određuje redoslijed (1 = početnik).</summary>
public class FitnessLevel : BaseEntity, INamedEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
}

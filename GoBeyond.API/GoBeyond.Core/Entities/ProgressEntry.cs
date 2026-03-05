namespace GoBeyond.Core.Entities;

public class ProgressEntry : BaseEntity
{
    public int ClientProfileId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string? PhotoUrl { get; set; }
    public decimal? Weight { get; set; }
    public string? Measurements { get; set; }
    public string? Strength { get; set; }
    public string? Conditioning { get; set; }

    public ClientProfile ClientProfile { get; set; } = null!;
}

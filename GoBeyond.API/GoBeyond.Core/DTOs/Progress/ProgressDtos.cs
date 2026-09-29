using System.ComponentModel.DataAnnotations;

namespace GoBeyond.Core.DTOs.Progress;

public sealed class ProgressEntryItemDto
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public decimal WeightKg { get; set; }
    public string Measurements { get; set; } = string.Empty;
    public string Strength { get; set; } = string.Empty;
    public string Conditioning { get; set; } = string.Empty;
    public bool HasPlanSnapshot { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class UpsertProgressRequest
{
    private string _measurements = string.Empty;
    private string _strength = string.Empty;
    private string _conditioning = string.Empty;

    [Range(typeof(decimal), "30", "300", ErrorMessage = "Težina mora biti između 30 i 300 kg.")]
    public decimal WeightKg { get; set; }

    [Required(ErrorMessage = "Obimi su obavezni.")]
    [StringLength(300, MinimumLength = 2, ErrorMessage = "Obimi moraju imati 2–300 znakova.")]
    public string Measurements
    {
        get => _measurements;
        set => _measurements = value?.Trim() ?? string.Empty;
    }

    [Required(ErrorMessage = "Snaga je obavezna.")]
    [StringLength(300, MinimumLength = 2, ErrorMessage = "Snaga mora imati 2–300 znakova.")]
    public string Strength
    {
        get => _strength;
        set => _strength = value?.Trim() ?? string.Empty;
    }

    [Required(ErrorMessage = "Kondicija je obavezna.")]
    [StringLength(300, MinimumLength = 2, ErrorMessage = "Kondicija mora imati 2–300 znakova.")]
    public string Conditioning
    {
        get => _conditioning;
        set => _conditioning = value?.Trim() ?? string.Empty;
    }
}

public sealed class ProgressChartPointDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal WeightKg { get; set; }
}

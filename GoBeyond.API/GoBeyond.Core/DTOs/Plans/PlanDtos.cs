using System.ComponentModel.DataAnnotations;
using GoBeyond.Core.Enums;

namespace GoBeyond.Core.DTOs.Plans;

public sealed class PlanSummaryDto
{
    public int Id { get; set; }
    public int SubscriptionId { get; set; }
    public string ClientFullName { get; set; } = string.Empty;
    public string? ClientPhotoUrl { get; set; }
    public TrainingPlanStatus Status { get; set; }
    public int Version { get; set; }
    public int FilledDays { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public SubscriptionStatus SubscriptionStatus { get; set; }
    public bool CanEdit { get; set; }
}

public sealed class PlanDetailDto
{
    public int Id { get; set; }
    public int SubscriptionId { get; set; }
    public string MentorFullName { get; set; } = string.Empty;
    public string ClientFullName { get; set; } = string.Empty;
    public string? MotivationalQuote { get; set; }
    public TrainingPlanStatus Status { get; set; }
    public int Version { get; set; }
    public bool CanEdit { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public List<DayPlanDto> Days { get; set; } = [];
}

public sealed class DayPlanDto
{
    public int Id { get; set; }
    public int DayOfWeek { get; set; }
    public string DayName { get; set; } = string.Empty;
    public int TrainingDurationMinutes { get; set; }
    public string TrainingDescription { get; set; } = string.Empty;
    public int? NutritionDurationMinutes { get; set; }
    public string NutritionDescription { get; set; } = string.Empty;
}

public sealed class CreatePlanRequest
{
    private string? _motivationalQuote;

    [Range(1, int.MaxValue, ErrorMessage = "Odaberite pretplatu za koju se kreira plan.")]
    public int SubscriptionId { get; set; }

    [StringLength(300, ErrorMessage = "Motivacijska poruka može imati najviše 300 znakova.")]
    public string? MotivationalQuote
    {
        get => _motivationalQuote;
        set => _motivationalQuote = value?.Trim();
    }
}

public sealed class UpdatePlanRequest
{
    private string? _motivationalQuote;

    [StringLength(300, ErrorMessage = "Motivacijska poruka može imati najviše 300 znakova.")]
    public string? MotivationalQuote
    {
        get => _motivationalQuote;
        set => _motivationalQuote = value?.Trim();
    }
}

public sealed class UpsertDayPlanRequest
{
    private string _trainingDescription = string.Empty;
    private string _nutritionDescription = string.Empty;

    [Range(1, 600, ErrorMessage = "Trajanje treninga mora biti između 1 i 600 minuta.")]
    public int TrainingDurationMinutes { get; set; }

    [Required(ErrorMessage = "Opis treninga je obavezan.")]
    [StringLength(8000, MinimumLength = 10, ErrorMessage = "Opis treninga mora imati 10–8000 znakova.")]
    public string TrainingDescription
    {
        get => _trainingDescription;
        set => _trainingDescription = value?.Trim() ?? string.Empty;
    }

    [Range(1, 1440, ErrorMessage = "Trajanje ishrane mora biti između 1 i 1440 minuta.")]
    public int? NutritionDurationMinutes { get; set; }

    [Required(ErrorMessage = "Opis ishrane je obavezan.")]
    [StringLength(8000, MinimumLength = 10, ErrorMessage = "Opis ishrane mora imati 10–8000 znakova.")]
    public string NutritionDescription
    {
        get => _nutritionDescription;
        set => _nutritionDescription = value?.Trim() ?? string.Empty;
    }
}

public sealed class LogTrainingSessionRequest
{
    [Range(1, 10000, ErrorMessage = "Broj ponavljanja mora biti cijeli broj između 1 i 10000.")]
    public int Repetitions { get; set; }

    [StringLength(500, ErrorMessage = "Napomena može imati najviše 500 znakova.")]
    public string? Note { get; set; }
}

public sealed class TrainingSessionItemDto
{
    public int Id { get; set; }
    public int DayOfWeek { get; set; }
    public string DayName { get; set; } = string.Empty;
    public DateTime CompletedAt { get; set; }
    public int Repetitions { get; set; }
    public string? Note { get; set; }
}

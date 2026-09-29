using System.ComponentModel.DataAnnotations;
using GoBeyond.Core.Enums;

namespace GoBeyond.Core.DTOs.Subscriptions;

public sealed class QuestionnaireDto
{
    public string PrimaryGoal { get; set; } = string.Empty;
    public string TimeCommitment { get; set; } = string.Empty;
    public string HealthIssues { get; set; } = string.Empty;
    public string Medications { get; set; } = string.Empty;
    public string WeeklySessions { get; set; } = string.Empty;
    public string OutsideActivity { get; set; } = string.Empty;
}

public sealed class QuestionnaireRequest
{
    private string _primaryGoal = string.Empty;
    private string _timeCommitment = string.Empty;
    private string _healthIssues = string.Empty;
    private string _medications = string.Empty;
    private string _weeklySessions = string.Empty;
    private string _outsideActivity = string.Empty;

    [Required(ErrorMessage = "Opišite vaš glavni cilj.")]
    [StringLength(500, MinimumLength = 2, ErrorMessage = "Odgovor mora imati 2–500 znakova.")]
    public string PrimaryGoal
    {
        get => _primaryGoal;
        set => _primaryGoal = value?.Trim() ?? string.Empty;
    }

    [Required(ErrorMessage = "Navedite koliko vremena možete posvetiti treningu.")]
    [StringLength(500, MinimumLength = 2, ErrorMessage = "Odgovor mora imati 2–500 znakova.")]
    public string TimeCommitment
    {
        get => _timeCommitment;
        set => _timeCommitment = value?.Trim() ?? string.Empty;
    }

    [Required(ErrorMessage = "Navedite zdravstvene probleme ili upišite \"Nema\".")]
    [StringLength(500, MinimumLength = 2, ErrorMessage = "Odgovor mora imati 2–500 znakova.")]
    public string HealthIssues
    {
        get => _healthIssues;
        set => _healthIssues = value?.Trim() ?? string.Empty;
    }

    [Required(ErrorMessage = "Navedite lijekove koje koristite ili upišite \"Nema\".")]
    [StringLength(500, MinimumLength = 2, ErrorMessage = "Odgovor mora imati 2–500 znakova.")]
    public string Medications
    {
        get => _medications;
        set => _medications = value?.Trim() ?? string.Empty;
    }

    [Required(ErrorMessage = "Navedite koliko treninga sedmično planirate.")]
    [StringLength(500, MinimumLength = 2, ErrorMessage = "Odgovor mora imati 2–500 znakova.")]
    public string WeeklySessions
    {
        get => _weeklySessions;
        set => _weeklySessions = value?.Trim() ?? string.Empty;
    }

    [Required(ErrorMessage = "Opišite vaše fizičke aktivnosti van treninga.")]
    [StringLength(500, MinimumLength = 2, ErrorMessage = "Odgovor mora imati 2–500 znakova.")]
    public string OutsideActivity
    {
        get => _outsideActivity;
        set => _outsideActivity = value?.Trim() ?? string.Empty;
    }
}

public sealed class CreateSubscriptionRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Odaberite mentora.")]
    public int MentorProfileId { get; set; }

    [Required(ErrorMessage = "Upitnik je obavezan.")]
    public QuestionnaireRequest? Questionnaire { get; set; }
}

public class SubscriptionDto
{
    public int Id { get; set; }
    public int MentorProfileId { get; set; }
    public string MentorFullName { get; set; } = string.Empty;
    public string? MentorPhotoUrl { get; set; }
    public string TrainingTypeName { get; set; } = string.Empty;
    public SubscriptionStatus Status { get; set; }
    public decimal Price { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? StatusReason { get; set; }
    public bool CanReview { get; set; }
    public int? ReviewId { get; set; }
    public bool CanRenew { get; set; }
    public bool CanCancel { get; set; }
}

public sealed class SubscriptionDetailDto : SubscriptionDto
{
    public QuestionnaireDto? Questionnaire { get; set; }
    public List<PaymentItemDto> Payments { get; set; } = [];
}

public sealed class PaymentItemDto
{
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public PaymentPurpose Purpose { get; set; }
    public PaymentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
}

public sealed class CreatePaymentIntentRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Odaberite pretplatu koju plaćate.")]
    public int SubscriptionId { get; set; }
}

public sealed class PaymentIntentDto
{
    public int PaymentId { get; set; }
    public string ClientSecret { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public PaymentPurpose Purpose { get; set; }
}

using GoBeyond.Core.Enums;

namespace GoBeyond.Infrastructure.Common;

/// <summary>Ponavljajuće poruke i razlozi statusa (na jednom mjestu).</summary>
public static class DomainTexts
{
    public const string MentorRemovedReason = "Mentor je uklonjen sa platforme.";
    public const string ClientRemovedReason = "Klijent je uklonjen sa platforme.";
    public const string ClientCancelledReason = "Klijent je otkazao pretplatu.";
    public const string SubscriptionExpiredReason = "Period pretplate je istekao.";

    public const string UserNotFound = "Korisnik nije pronađen.";
    public const string MentorNotFound = "Mentor nije pronađen.";
    public const string SubscriptionNotFound = "Pretplata nije pronađena.";
    public const string PlanNotFound = "Trening plan nije pronađen.";
    public const string ProfileMissing = "Profil korisnika nije pronađen.";

    public static string PlanStatusName(TrainingPlanStatus status) => status switch
    {
        TrainingPlanStatus.Draft => "U izradi",
        TrainingPlanStatus.Published => "Objavljen",
        TrainingPlanStatus.Archived => "Arhiviran",
        _ => status.ToString()
    };

    public static string SubscriptionStatusName(SubscriptionStatus status) => status switch
    {
        SubscriptionStatus.PendingPayment => "Čeka plaćanje",
        SubscriptionStatus.AwaitingMentor => "Čeka mentora",
        SubscriptionStatus.Active => "Aktivna",
        SubscriptionStatus.Rejected => "Odbijena",
        SubscriptionStatus.Cancelled => "Otkazana",
        SubscriptionStatus.Expired => "Istekla",
        _ => status.ToString()
    };

    public static string RoleName(UserRole role) => role switch
    {
        UserRole.Admin => "Administrator",
        UserRole.Mentor => "Mentor",
        UserRole.Client => "Klijent",
        _ => role.ToString()
    };

    public static string Date(DateTime? value) => value?.ToString("dd.MM.yyyy.") ?? "-";
}

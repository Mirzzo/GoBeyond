using System.Collections.Concurrent;
using System.Globalization;
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

    /// <summary>Podrazumijevana vremenska zona platforme (korisnici su u BiH); mijenja se sa Lifecycle:TimeZoneId.</summary>
    public const string DefaultTimeZoneId = "Europe/Sarajevo";

    /// <summary>Windows naziv iste zone, ako sistem ne prepozna IANA naziv (npr. Windows bez ICU podataka).</summary>
    private const string WindowsTimeZoneId = "Central European Standard Time";

    private static readonly ConcurrentDictionary<string, TimeZoneInfo> TimeZones = new(StringComparer.Ordinal);

    /// <summary>
    /// Datum u vremenskoj zoni platforme, npr. "29.11.2026.", isti kao u aplikacijama (one prikazuju lokalno vrijeme), a ne
    /// UTC datum koji je oko ponoći dan ranije. Vrijednost bez Kind-a (iz baze) je UTC. Završna tačka je dio zapisa datuma,
    /// pa rečenica koja se završava datumom ne dodaje još jednu.
    /// </summary>
    public static string Date(DateTime? value, string? timeZoneId = null) =>
        value is { } date ? PlatformTime(date, timeZoneId).ToString("dd.MM.yyyy.", CultureInfo.InvariantCulture) : "-";

    /// <summary>Trenutak kao lokalno vrijeme platforme (npr. za tekući mjesec); vrijednost bez Kind-a (iz baze) je UTC.</summary>
    public static DateTime PlatformTime(DateTime value, string? timeZoneId = null)
    {
        var utc = value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, PlatformTimeZone(timeZoneId));
    }

    /// <summary>Iznos sa decimalnim zarezom, npr. "39,99 USD" (kao u aplikacijama i demo podacima).</summary>
    public static string Money(decimal amount, string currency) =>
        $"{amount.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',')} {currency.ToUpperInvariant()}";

    /// <summary>Broj godina sa ispravnim oblikom: "1 godina", "2 godine", "5 godina", "21 godina", "22 godine", "112 godina".</summary>
    public static string Years(int count) =>
        $"{count} {(count % 10 is >= 2 and <= 4 && count % 100 is < 12 or > 14 ? "godine" : "godina")}";

    /// <summary>Slobodan tekst (npr. razlog koji je upisao mentor) kao rečenica: dodaje tačku ako ne završava sa ".", "!" ili "?".</summary>
    public static string Sentence(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length == 0 || trimmed[^1] is '.' or '!' or '?' ? trimmed : trimmed + ".";
    }

    public static TimeZoneInfo PlatformTimeZone(string? timeZoneId = null) =>
        TimeZones.GetOrAdd(string.IsNullOrWhiteSpace(timeZoneId) ? DefaultTimeZoneId : timeZoneId.Trim(), FindTimeZone);

    private static TimeZoneInfo FindTimeZone(string id)
    {
        foreach (var candidate in new[] { id, WindowsTimeZoneId })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(candidate);
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                // probaj sljedeći naziv
            }
        }
        return TimeZoneInfo.Utc;
    }
}

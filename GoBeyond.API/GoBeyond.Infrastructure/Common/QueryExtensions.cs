using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Common;

/// <summary>Ponavljajući Include/Where izrazi nad DbSet-ovima.</summary>
public static class QueryExtensions
{
    /// <summary>Korisnik sa svim podacima potrebnim za profil.</summary>
    public static IQueryable<User> WithProfile(this IQueryable<User> query) => query
        .Include(x => x.Gender)
        .Include(x => x.MentorProfile).ThenInclude(x => x!.TrainingType)
        .Include(x => x.MentorProfile).ThenInclude(x => x!.Specializations).ThenInclude(x => x.FitnessGoal)
        .Include(x => x.ClientProfile).ThenInclude(x => x!.FitnessLevel)
        .Include(x => x.ClientProfile).ThenInclude(x => x!.FitnessGoal)
        .Include(x => x.ClientProfile).ThenInclude(x => x!.PreferredTrainingType);

    /// <summary>Plan sa podacima potrebnim za PlanDetail/PlanSummary.</summary>
    public static IQueryable<TrainingPlan> WithDetails(this IQueryable<TrainingPlan> query) => query
        .Include(x => x.Subscription)
        .Include(x => x.MentorProfile).ThenInclude(x => x.User)
        .Include(x => x.ClientProfile).ThenInclude(x => x.User)
        .Include(x => x.Days);

    /// <summary>
    /// Mentori vidljivi klijentima: odobreni, aktivni, neobrisani i sa ulogom Mentor (profil korisnika kojem je
    /// administrator promijenio ulogu ostaje sačuvan, ali se ne nudi dok mu se uloga ne vrati).
    /// </summary>
    public static IQueryable<MentorProfile> Visible(this IQueryable<MentorProfile> query) => query
        .Where(x => x.Status == MentorApprovalStatus.Approved && x.User.IsActive && !x.User.IsDeleted && x.User.Role == UserRole.Mentor);

    /// <summary>Statusi u kojima klijent "ima" saradnju (može imati samo jednu takvu).</summary>
    public static readonly SubscriptionStatus[] OpenStatuses =
        [SubscriptionStatus.PendingPayment, SubscriptionStatus.AwaitingMentor, SubscriptionStatus.Active];

    /// <summary>Najduži pojam pretrage koji ide u upit (duži se skraćuje; SQL Server odbija LIKE uzorak duži od 4000 znakova).</summary>
    public const int MaxSearchLength = 100;

    /// <summary>Pojam pretrage bez razmaka na krajevima, skraćen na <see cref="MaxSearchLength"/> znakova; prazan → null.</summary>
    public static string? NormalizeSearch(this string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return null;
        var term = search.Trim();
        if (term.Length <= MaxSearchLength) return term;

        // Ne prepolovljuje emoji/surogatni par na granici.
        var length = char.IsHighSurrogate(term[MaxSearchLength - 1]) ? MaxSearchLength - 1 : MaxSearchLength;
        return term[..length].TrimEnd();
    }
}

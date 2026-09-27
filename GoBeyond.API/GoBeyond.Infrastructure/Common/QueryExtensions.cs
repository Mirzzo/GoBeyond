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

    /// <summary>Mentori vidljivi klijentima: odobreni, aktivni i neobrisani.</summary>
    public static IQueryable<MentorProfile> Visible(this IQueryable<MentorProfile> query) => query
        .Where(x => x.Status == MentorApprovalStatus.Approved && x.User.IsActive && !x.User.IsDeleted);

    /// <summary>Statusi u kojima klijent "ima" saradnju (može imati samo jednu takvu).</summary>
    public static readonly SubscriptionStatus[] OpenStatuses =
        [SubscriptionStatus.PendingPayment, SubscriptionStatus.AwaitingMentor, SubscriptionStatus.Active];

    public static string? NormalizeSearch(this string? search) =>
        string.IsNullOrWhiteSpace(search) ? null : search.Trim();
}

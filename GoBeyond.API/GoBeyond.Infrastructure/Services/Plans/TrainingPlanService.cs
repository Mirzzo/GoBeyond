using GoBeyond.Core.DTOs.Plans;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Notifications;
using GoBeyond.Infrastructure.Services.Subscriptions;
using GoBeyond.Infrastructure.StateMachineServices.TrainingPlans;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GoBeyond.Infrastructure.Services.Plans;

public interface ITrainingPlanService
{
    Task<List<PlanSummaryDto>> GetMentorPlansAsync(int mentorUserId, PlanSearchObject search, CancellationToken cancellationToken = default);
    Task<PlanDetailDto> GetBySubscriptionAsync(int mentorUserId, int subscriptionId, CancellationToken cancellationToken = default);
    Task<PlanDetailDto> GetByIdAsync(int userId, UserRole role, int planId, CancellationToken cancellationToken = default);
    Task<PlanDetailDto> CreateAsync(int mentorUserId, CreatePlanRequest request, CancellationToken cancellationToken = default);
    Task<PlanDetailDto> UpdateAsync(int mentorUserId, int planId, UpdatePlanRequest request, CancellationToken cancellationToken = default);
    Task<PlanDetailDto> UpsertDayAsync(int mentorUserId, int planId, int dayOfWeek, UpsertDayPlanRequest request, CancellationToken cancellationToken = default);
    Task<PlanDetailDto> RemoveDayAsync(int mentorUserId, int planId, int dayOfWeek, CancellationToken cancellationToken = default);
    Task<PlanDetailDto> PublishAsync(int mentorUserId, int planId, CancellationToken cancellationToken = default);
    Task<PlanDetailDto> ArchiveAsync(int mentorUserId, int planId, CancellationToken cancellationToken = default);
    Task<PlanDetailDto> GetMyCurrentAsync(int clientUserId, CancellationToken cancellationToken = default);
    Task<TrainingPlan?> FindCurrentPlanAsync(int clientProfileId, CancellationToken cancellationToken = default);
    Task<TrainingSessionItemDto> LogSessionAsync(int clientUserId, int planId, int dayOfWeek, LogTrainingSessionRequest request, CancellationToken cancellationToken = default);
    Task<List<TrainingSessionItemDto>> GetSessionsAsync(int userId, UserRole role, int planId, CancellationToken cancellationToken = default);
}

public sealed class TrainingPlanService(
    GoBeyondDbContext db,
    ITrainingPlanStateFactory stateFactory,
    ISubscriptionWorkflow workflow,
    INotificationSender notifications,
    IOptions<LifecycleOptions> lifecycleOptions) : ITrainingPlanService
{
    public const string NoPublishedPlan = "Još nemate objavljen plan.";

    public async Task<List<PlanSummaryDto>> GetMentorPlansAsync(int mentorUserId, PlanSearchObject search,
        CancellationToken cancellationToken = default)
    {
        var query = db.TrainingPlans.AsNoTracking().WithDetails().Where(x => x.MentorProfile.UserId == mentorUserId);
        if (search.Search.NormalizeSearch() is { } term)
            query = query.Where(x => x.ClientProfile.User.FirstName.Contains(term) ||
                                     x.ClientProfile.User.LastName.Contains(term) ||
                                     (x.ClientProfile.User.FirstName + " " + x.ClientProfile.User.LastName).Contains(term));
        if (search.Status is { } status) query = query.Where(x => x.Status == status);

        var plans = await query.OrderByDescending(x => x.UpdatedAt).AsSplitQuery().ToListAsync(cancellationToken);
        return plans.Select(PlanMapper.ToSummary).ToList();
    }

    public async Task<PlanDetailDto> GetBySubscriptionAsync(int mentorUserId, int subscriptionId, CancellationToken cancellationToken = default)
    {
        var plan = await db.TrainingPlans.AsNoTracking().WithDetails().AsSplitQuery()
                       .FirstOrDefaultAsync(x => x.SubscriptionId == subscriptionId && x.MentorProfile.UserId == mentorUserId, cancellationToken)
                   ?? throw new NotFoundException("Plan za ovu pretplatu još nije kreiran.");
        return PlanMapper.ToDetail(plan);
    }

    public async Task<PlanDetailDto> GetByIdAsync(int userId, UserRole role, int planId, CancellationToken cancellationToken = default)
    {
        var plan = await db.TrainingPlans.AsNoTracking().WithDetails().AsSplitQuery()
                       .FirstOrDefaultAsync(x => x.Id == planId, cancellationToken)
                   ?? throw new NotFoundException(DomainTexts.PlanNotFound);
        EnsureCanRead(plan, userId, role);
        return PlanMapper.ToDetail(plan);
    }

    public async Task<PlanDetailDto> CreateAsync(int mentorUserId, CreatePlanRequest request, CancellationToken cancellationToken = default)
    {
        var subscription = await db.Subscriptions
            .Include(x => x.ClientProfile).ThenInclude(x => x.User)
            .Include(x => x.MentorProfile).ThenInclude(x => x.User)
            .Include(x => x.TrainingPlan)
            .FirstOrDefaultAsync(x => x.Id == request.SubscriptionId && x.MentorProfile.UserId == mentorUserId, cancellationToken)
            ?? throw new NotFoundException(DomainTexts.SubscriptionNotFound);

        if (subscription.TrainingPlan is not null)
            throw new ConflictException("Plan za ovu pretplatu već postoji.");

        var now = DateTime.UtcNow;
        // "IZRADI PLAN" = prihvatanje zahtjeva ako još čeka mentora.
        if (subscription.Status == SubscriptionStatus.AwaitingMentor)
            workflow.Accept(subscription, now);
        if (subscription.Status != SubscriptionStatus.Active)
            throw new ValidationException("Plan se može kreirati samo za aktivnu saradnju.");

        var plan = new TrainingPlan
        {
            Subscription = subscription,
            SubscriptionId = subscription.Id,
            MentorProfile = subscription.MentorProfile,
            MentorProfileId = subscription.MentorProfileId,
            ClientProfile = subscription.ClientProfile,
            ClientProfileId = subscription.ClientProfileId,
            MotivationalQuote = Normalize(request.MotivationalQuote),
            Status = TrainingPlanStatus.Draft,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.TrainingPlans.Add(plan);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("Plan za ovu pretplatu već postoji.");
        }
        return PlanMapper.ToDetail(plan);
    }

    public Task<PlanDetailDto> UpdateAsync(int mentorUserId, int planId, UpdatePlanRequest request, CancellationToken cancellationToken = default) =>
        MutateAsync(mentorUserId, planId, cancellationToken, (state, plan, now) =>
            state.UpdateDetails(plan, Normalize(request.MotivationalQuote), now));

    public Task<PlanDetailDto> UpsertDayAsync(int mentorUserId, int planId, int dayOfWeek, UpsertDayPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureValidDay(dayOfWeek);
        return MutateAsync(mentorUserId, planId, cancellationToken, (state, plan, now) => state.UpsertDay(plan, dayOfWeek, request, now));
    }

    public Task<PlanDetailDto> RemoveDayAsync(int mentorUserId, int planId, int dayOfWeek, CancellationToken cancellationToken = default)
    {
        EnsureValidDay(dayOfWeek);
        return MutateAsync(mentorUserId, planId, cancellationToken, (state, plan, now) => state.RemoveDay(plan, dayOfWeek, now));
    }

    public Task<PlanDetailDto> PublishAsync(int mentorUserId, int planId, CancellationToken cancellationToken = default) =>
        MutateAsync(mentorUserId, planId, cancellationToken, (state, plan, now) => state.Publish(plan, now));

    public Task<PlanDetailDto> ArchiveAsync(int mentorUserId, int planId, CancellationToken cancellationToken = default) =>
        MutateAsync(mentorUserId, planId, cancellationToken, (state, plan, now) => state.Archive(plan, now));

    public async Task<PlanDetailDto> GetMyCurrentAsync(int clientUserId, CancellationToken cancellationToken = default)
    {
        var clientProfileId = await GetClientProfileIdAsync(clientUserId, cancellationToken);
        var plan = await FindCurrentPlanAsync(clientProfileId, cancellationToken) ?? throw new NotFoundException(NoPublishedPlan);
        return PlanMapper.ToDetail(plan);
    }

    /// <summary>
    /// Trenutni plan klijenta: zadnji objavljeni/arhivirani plan aktivne pretplate, a ako ga nema,
    /// plan zadnje saradnje prekinute zbog brisanja mentora (ostaje dostupan samo za čitanje).
    /// </summary>
    public async Task<TrainingPlan?> FindCurrentPlanAsync(int clientProfileId, CancellationToken cancellationToken = default)
    {
        var visible = db.TrainingPlans.AsNoTracking().WithDetails().AsSplitQuery()
            .Where(x => x.ClientProfileId == clientProfileId &&
                        (x.Status == TrainingPlanStatus.Published || x.Status == TrainingPlanStatus.Archived));

        return await visible.Where(x => x.Subscription.Status == SubscriptionStatus.Active)
                   .OrderByDescending(x => x.PublishedAt).FirstOrDefaultAsync(cancellationToken)
               ?? await visible.Where(x => x.Subscription.Status == SubscriptionStatus.Cancelled &&
                                           x.Subscription.StatusReason == DomainTexts.MentorRemovedReason)
                   .OrderByDescending(x => x.Subscription.CancelledAt).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<TrainingSessionItemDto> LogSessionAsync(int clientUserId, int planId, int dayOfWeek,
        LogTrainingSessionRequest request, CancellationToken cancellationToken = default)
    {
        EnsureValidDay(dayOfWeek);
        var plan = await db.TrainingPlans.Include(x => x.Days).Include(x => x.ClientProfile)
                       .FirstOrDefaultAsync(x => x.Id == planId && x.ClientProfile.UserId == clientUserId, cancellationToken)
                   ?? throw new NotFoundException(DomainTexts.PlanNotFound);
        if (plan.Status != TrainingPlanStatus.Published)
            throw new ValidationException("Trening se može evidentirati samo za objavljen plan.");

        var day = plan.Days.FirstOrDefault(x => x.DayOfWeek == dayOfWeek)
                  ?? throw new NotFoundException("Plan nema trening za odabrani dan.");

        var session = new TrainingSession
        {
            TrainingPlanId = plan.Id,
            DayPlanId = day.Id,
            ClientProfileId = plan.ClientProfileId,
            CompletedAt = DateTime.UtcNow,
            Repetitions = request.Repetitions,
            Note = Normalize(request.Note)
        };
        db.TrainingSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return ToSessionItem(session, day.DayOfWeek);
    }

    public async Task<List<TrainingSessionItemDto>> GetSessionsAsync(int userId, UserRole role, int planId,
        CancellationToken cancellationToken = default)
    {
        var plan = await db.TrainingPlans.AsNoTracking().WithDetails().AsSplitQuery()
                       .FirstOrDefaultAsync(x => x.Id == planId, cancellationToken)
                   ?? throw new NotFoundException(DomainTexts.PlanNotFound);
        EnsureCanRead(plan, userId, role);

        var sessions = await db.TrainingSessions.AsNoTracking().Include(x => x.DayPlan)
            .Where(x => x.TrainingPlanId == planId)
            .OrderByDescending(x => x.CompletedAt)
            .ToListAsync(cancellationToken);
        return sessions.Select(x => ToSessionItem(x, x.DayPlan.DayOfWeek)).ToList();
    }

    public static TrainingSessionItemDto ToSessionItem(TrainingSession session, int dayOfWeek) => new()
    {
        Id = session.Id,
        DayOfWeek = dayOfWeek,
        DayName = BosnianCalendar.DayName(dayOfWeek),
        CompletedAt = session.CompletedAt,
        Repetitions = session.Repetitions,
        Note = session.Note
    };

    /// <summary>Zajednički tok za sve mentorove izmjene: učitaj → provjeri pravo → stanje → obavijest → snimi.</summary>
    private async Task<PlanDetailDto> MutateAsync(int mentorUserId, int planId, CancellationToken cancellationToken,
        Action<BaseTrainingPlanState, TrainingPlan, DateTime> action)
    {
        var plan = await db.TrainingPlans.WithDetails().AsSplitQuery()
                       .FirstOrDefaultAsync(x => x.Id == planId && x.MentorProfile.UserId == mentorUserId, cancellationToken)
                   ?? throw new NotFoundException(DomainTexts.PlanNotFound);
        if (!PlanMapper.CanEdit(plan))
            throw new ValidationException("Plan se više ne može mijenjati jer saradnja sa klijentom nije aktivna.");

        var now = DateTime.UtcNow;
        var statusBefore = plan.Status;
        var versionBefore = plan.Version;
        action(stateFactory.GetState(plan.Status), plan, now);

        var client = plan.ClientProfile.User;
        var mentorName = plan.MentorProfile.User.FullName;
        if (plan.Status == TrainingPlanStatus.Published && statusBefore != TrainingPlanStatus.Published)
        {
            plan.LastUpdateNotifiedAt = now;
            notifications.Notify(client, NotificationType.PlanPublished, "Vaš trening plan je objavljen",
                $"Mentor {mentorName} je objavio vaš sedmični trening plan (verzija {plan.Version}). Pogledajte ga u meniju \"Moj plan\".",
                sendEmail: true);
        }
        else if (plan.Status == TrainingPlanStatus.Published && plan.Version != versionBefore && ShouldNotifyUpdate(plan, now))
        {
            plan.LastUpdateNotifiedAt = now;
            notifications.Notify(client, NotificationType.PlanUpdated, "Vaš trening plan je ažuriran",
                $"Mentor {mentorName} je ažurirao vaš trening plan (verzija {plan.Version}). Pogledajte izmjene u meniju \"Moj plan\".",
                sendEmail: true);
        }

        await db.SaveChangesAsync(cancellationToken);
        return PlanMapper.ToDetail(plan);
    }

    /// <summary>Najviše jedna PlanUpdated obavijest po planu u konfigurisanom intervalu (default 10 min).</summary>
    private bool ShouldNotifyUpdate(TrainingPlan plan, DateTime now) =>
        plan.LastUpdateNotifiedAt is not { } last ||
        now - last >= TimeSpan.FromMinutes(lifecycleOptions.Value.PlanUpdateNotificationThrottleMinutes);

    private static void EnsureCanRead(TrainingPlan plan, int userId, UserRole role)
    {
        var allowed = role switch
        {
            UserRole.Admin => true,
            UserRole.Mentor => plan.MentorProfile.UserId == userId,
            UserRole.Client => plan.ClientProfile.UserId == userId && plan.Status != TrainingPlanStatus.Draft,
            _ => false
        };
        if (!allowed) throw new ForbiddenException("Nemate pristup ovom trening planu.");
    }

    private static void EnsureValidDay(int dayOfWeek)
    {
        if (dayOfWeek is < 1 or > BaseTrainingPlanState.DaysInWeek)
            throw new ValidationException("dayOfWeek", "Dan mora biti broj od 1 (Ponedjeljak) do 7 (Nedjelja).");
    }

    private async Task<int> GetClientProfileIdAsync(int clientUserId, CancellationToken cancellationToken) =>
        await db.ClientProfiles.Where(x => x.UserId == clientUserId).Select(x => (int?)x.Id).FirstOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(DomainTexts.ProfileMissing);

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

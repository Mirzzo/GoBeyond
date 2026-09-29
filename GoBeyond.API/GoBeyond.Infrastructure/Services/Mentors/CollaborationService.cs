using GoBeyond.Core.DTOs.Common;
using GoBeyond.Core.DTOs.Mentors;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Plans;
using GoBeyond.Infrastructure.Services.Progress;
using GoBeyond.Infrastructure.Services.Subscriptions;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Services.Mentors;

public interface ICollaborationService
{
    Task<List<CollaborationRequestDto>> GetRequestsAsync(int mentorUserId, string? search, CancellationToken cancellationToken = default);
    Task<ClientDescriptionDto> GetRequestAsync(int mentorUserId, int subscriptionId, CancellationToken cancellationToken = default);
    Task<CollaborationRequestDto> AcceptAsync(int mentorUserId, int subscriptionId, CancellationToken cancellationToken = default);
    Task<MessageResponse> RejectAsync(int mentorUserId, int subscriptionId, string reason, CancellationToken cancellationToken = default);
    Task<List<SubscriberDto>> GetSubscribersAsync(int mentorUserId, SubscriptionSearchObject search, CancellationToken cancellationToken = default);
    Task<SubscriberDetailDto> GetSubscriberAsync(int mentorUserId, int subscriptionId, CancellationToken cancellationToken = default);
}

public sealed class CollaborationService(GoBeyondDbContext db, ISubscriptionWorkflow workflow) : ICollaborationService
{
    public async Task<List<CollaborationRequestDto>> GetRequestsAsync(int mentorUserId, string? search, CancellationToken cancellationToken = default)
    {
        // Zahtjevi = čekaju mentora ILI su prihvaćeni, a plan još nije objavljen.
        var query = MentorSubscriptions(mentorUserId).Where(x =>
            x.Status == SubscriptionStatus.AwaitingMentor ||
            (x.Status == SubscriptionStatus.Active && (x.TrainingPlan == null || x.TrainingPlan.Status == TrainingPlanStatus.Draft)));
        query = ApplyClientSearch(query, search);

        return await query.OrderBy(x => x.PaidAt ?? x.CreatedAt)
            .Select(x => new CollaborationRequestDto
            {
                SubscriptionId = x.Id,
                ClientFullName = x.ClientProfile.User.FirstName + " " + x.ClientProfile.User.LastName,
                ClientPhotoUrl = x.ClientProfile.User.ProfileImageUrl,
                Status = x.Status,
                RequestedAt = x.PaidAt ?? x.CreatedAt,
                PlanId = x.TrainingPlan != null ? x.TrainingPlan.Id : null,
                PlanStatus = x.TrainingPlan != null ? x.TrainingPlan.Status : null
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<ClientDescriptionDto> GetRequestAsync(int mentorUserId, int subscriptionId, CancellationToken cancellationToken = default)
    {
        var subscription = await DescriptionQuery(mentorUserId).FirstOrDefaultAsync(x => x.Id == subscriptionId, cancellationToken)
                           ?? throw new NotFoundException(DomainTexts.SubscriptionNotFound);
        return FillDescription(new ClientDescriptionDto(), subscription);
    }

    public async Task<CollaborationRequestDto> AcceptAsync(int mentorUserId, int subscriptionId, CancellationToken cancellationToken = default)
    {
        // Zaključana pretplata: istovremeno odbijanje (ili otkazivanje) čeka, odnosno prihvatanje vidi njegov rezultat (400).
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.LockSubscriptionAsync(subscriptionId, cancellationToken);
        var subscription = await WorkflowQuery(mentorUserId).FirstOrDefaultAsync(x => x.Id == subscriptionId, cancellationToken)
                           ?? throw new NotFoundException(DomainTexts.SubscriptionNotFound);
        workflow.Accept(subscription, DateTime.UtcNow);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CollaborationRequestDto
        {
            SubscriptionId = subscription.Id,
            ClientFullName = subscription.ClientProfile.User.FullName,
            ClientPhotoUrl = subscription.ClientProfile.User.ProfileImageUrl,
            Status = subscription.Status,
            RequestedAt = subscription.PaidAt ?? subscription.CreatedAt,
            PlanId = subscription.TrainingPlan?.Id,
            PlanStatus = subscription.TrainingPlan?.Status
        };
    }

    public async Task<MessageResponse> RejectAsync(int mentorUserId, int subscriptionId, string reason, CancellationToken cancellationToken = default)
    {
        // Pretplata se zaključava prije čitanja: istovremeno prihvatanje (ili drugo odbijanje) čeka dok povrat i odbijanje
        // ne završe, pa vidi Rejected (400) umjesto da ga pregazi. Neuspio povrat poništava transakciju (ostaje AwaitingMentor).
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.LockSubscriptionAsync(subscriptionId, cancellationToken);
        var subscription = await WorkflowQuery(mentorUserId).Include(x => x.Payments)
                               .FirstOrDefaultAsync(x => x.Id == subscriptionId, cancellationToken)
                           ?? throw new NotFoundException(DomainTexts.SubscriptionNotFound);

        var refunds = await workflow.RejectAsync(subscription, reason.Trim(), DateTime.UtcNow, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new MessageResponse(RejectMessage(subscription.ClientProfile.User.FullName, refunds, subscription.Currency));
    }

    /// <summary>
    /// Poruka mentoru o odbijanju, sa iznosima koje je upravo ovo odbijanje vratilo, odnosno ostavilo osporenim (isti iznosi
    /// kao u obavijesti klijentu). Uplata osporena ranije (npr. duplikat) se ovdje ne spominje.
    /// </summary>
    private static string RejectMessage(string clientName, RefundOutcome refunds, string currency)
    {
        var rejected = $"Zahtjev klijenta {clientName} je odbijen";
        var refunded = DomainTexts.Money(refunds.Refunded, currency);
        if (refunds.Disputed == 0)
            return refunds.Refunded > 0 ? $"{rejected}, a uplaćeni iznos od {refunded} je vraćen." : $"{rejected}.";

        var disputed = DomainTexts.Money(refunds.Disputed, currency);
        const string notRefunded = "je osporena kod banke klijenta, pa se ne vraća automatski; o povratu odlučuje postupak osporavanja.";
        return refunds.Refunded > 0
            ? $"{rejected}. Uplaćeni iznos od {refunded} je vraćen, a uplata od {disputed} {notRefunded}"
            : $"{rejected}. Uplata od {disputed} {notRefunded}";
    }

    public async Task<List<SubscriberDto>> GetSubscribersAsync(int mentorUserId, SubscriptionSearchObject search,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyClientSearch(MentorSubscriptions(mentorUserId), search.Search);
        query = search.Status is { } status
            ? query.Where(x => x.Status == status)
            : query.Where(x => x.Status != SubscriptionStatus.PendingPayment && x.Status != SubscriptionStatus.Rejected);

        return await query
            .OrderBy(x => x.Status == SubscriptionStatus.Active ? 0 : x.Status == SubscriptionStatus.AwaitingMentor ? 1 : 2)
            .ThenByDescending(x => x.StartDate ?? x.CreatedAt)
            .Select(x => new SubscriberDto
            {
                SubscriptionId = x.Id,
                ClientFullName = x.ClientProfile.User.FirstName + " " + x.ClientProfile.User.LastName,
                ClientPhotoUrl = x.ClientProfile.User.ProfileImageUrl,
                Status = x.Status,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                PlanId = x.TrainingPlan != null ? x.TrainingPlan.Id : null,
                PlanStatus = x.TrainingPlan != null ? x.TrainingPlan.Status : null,
                LastTrainingAt = x.TrainingPlan != null ? x.TrainingPlan.Sessions.Max(s => (DateTime?)s.CompletedAt) : null
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<SubscriberDetailDto> GetSubscriberAsync(int mentorUserId, int subscriptionId, CancellationToken cancellationToken = default)
    {
        var subscription = await DescriptionQuery(mentorUserId)
                               .Include(x => x.TrainingPlan)
                               .FirstOrDefaultAsync(x => x.Id == subscriptionId, cancellationToken)
                           ?? throw new NotFoundException(DomainTexts.SubscriptionNotFound);

        var dto = FillDescription(new SubscriberDetailDto(), subscription);
        dto.StartDate = subscription.StartDate;
        dto.EndDate = subscription.EndDate;

        if (subscription.TrainingPlan is { } plan)
        {
            var sessions = await db.TrainingSessions.AsNoTracking().Include(x => x.DayPlan)
                .Where(x => x.TrainingPlanId == plan.Id)
                .OrderByDescending(x => x.CompletedAt)
                .ToListAsync(cancellationToken);
            dto.Sessions = sessions.Select(x => TrainingPlanService.ToSessionItem(x, x.DayPlan.DayOfWeek)).ToList();
        }

        var progress = await db.ProgressEntries.AsNoTracking()
            .Where(x => x.ClientProfileId == subscription.ClientProfileId)
            .OrderByDescending(x => x.Year).ThenByDescending(x => x.Month)
            .ToListAsync(cancellationToken);
        dto.Progress = progress.Select(ProgressService.ToItem).ToList();
        return dto;
    }

    /// <summary>
    /// Pretplate koje mentor vidi: samo plaćene. Neplaćena pretplata (PaidAt == null) nikad nije stigla do mentora, pa se ne
    /// prikazuje ni kasnije (npr. kao Cancelled), ni sa eksplicitnim ?status= filterom, ni po id-u.
    /// </summary>
    private IQueryable<Subscription> MentorSubscriptions(int mentorUserId) =>
        db.Subscriptions.AsNoTracking().Where(x => x.MentorProfile.UserId == mentorUserId && x.PaidAt != null);

    private static IQueryable<Subscription> ApplyClientSearch(IQueryable<Subscription> query, string? search) =>
        search.NormalizeSearch() is { } term
            ? query.Where(x => x.ClientProfile.User.FirstName.Contains(term) || x.ClientProfile.User.LastName.Contains(term) ||
                               (x.ClientProfile.User.FirstName + " " + x.ClientProfile.User.LastName).Contains(term))
            : query;

    private IQueryable<Subscription> DescriptionQuery(int mentorUserId) => MentorSubscriptions(mentorUserId)
        .Include(x => x.Questionnaire)
        .Include(x => x.ClientProfile).ThenInclude(x => x.User).ThenInclude(x => x.Gender)
        .Include(x => x.ClientProfile).ThenInclude(x => x.FitnessLevel)
        .Include(x => x.ClientProfile).ThenInclude(x => x.FitnessGoal);

    private IQueryable<Subscription> WorkflowQuery(int mentorUserId) => db.Subscriptions
        .Where(x => x.MentorProfile.UserId == mentorUserId && x.PaidAt != null)
        .Include(x => x.ClientProfile).ThenInclude(x => x.User)
        .Include(x => x.MentorProfile).ThenInclude(x => x.User)
        .Include(x => x.TrainingPlan);

    private static T FillDescription<T>(T dto, Subscription subscription) where T : ClientDescriptionDto
    {
        var client = subscription.ClientProfile;
        dto.SubscriptionId = subscription.Id;
        dto.Status = subscription.Status;
        dto.ClientFullName = client.User.FullName;
        dto.ClientPhotoUrl = client.User.ProfileImageUrl;
        dto.Age = ProfileMapper.Age(client.User.DateOfBirth);
        dto.GenderName = client.User.Gender.Name;
        dto.WeightKg = client.WeightKg;
        dto.HeightCm = client.HeightCm;
        dto.FitnessLevelName = client.FitnessLevel.Name;
        dto.TrainingExperienceYears = client.TrainingExperienceYears;
        dto.FitnessGoalName = client.FitnessGoal.Name;
        dto.GoalDescription = client.GoalDescription;
        dto.RequestedAt = subscription.PaidAt ?? subscription.CreatedAt;
        dto.Questionnaire = subscription.Questionnaire is { } q ? SubscriptionService.ToQuestionnaireDto(q) : null;
        return dto;
    }
}

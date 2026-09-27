using GoBeyond.Core.DTOs.Admin;
using GoBeyond.Core.DTOs.Subscriptions;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Payments;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Services.Subscriptions;

public interface ISubscriptionService
{
    Task<SubscriptionDetailDto> CreateAsync(int clientUserId, CreateSubscriptionRequest request, CancellationToken cancellationToken = default);
    Task<List<SubscriptionDto>> GetMineAsync(int clientUserId, SubscriptionStatus? status, CancellationToken cancellationToken = default);
    Task<SubscriptionDetailDto> GetMineByIdAsync(int clientUserId, int id, CancellationToken cancellationToken = default);
    Task<SubscriptionDto> CancelAsync(int clientUserId, int id, CancellationToken cancellationToken = default);

    Task<List<AdminSubscriptionDto>> GetAllAsync(SubscriptionSearchObject search, CancellationToken cancellationToken = default);
    Task<AdminSubscriptionDto> AdminCancelAsync(int id, CancelSubscriptionRequest request, CancellationToken cancellationToken = default);
}

public sealed class SubscriptionService(
    GoBeyondDbContext db,
    ISubscriptionWorkflow workflow,
    IPaymentGateway paymentGateway) : ISubscriptionService
{
    public const string AlreadyCollaborating = "Već imate aktivnu ili započetu saradnju sa mentorom.";

    public async Task<SubscriptionDetailDto> CreateAsync(int clientUserId, CreateSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        var client = await GetClientAsync(clientUserId, cancellationToken);
        var mentor = await db.MentorProfiles.Visible().Include(x => x.User)
                         .FirstOrDefaultAsync(x => x.Id == request.MentorProfileId, cancellationToken)
                     ?? throw new NotFoundException(DomainTexts.MentorNotFound);

        var open = await db.Subscriptions.Include(x => x.Questionnaire)
            .Where(x => x.ClientProfileId == client.Id && QueryExtensions.OpenStatuses.Contains(x.Status))
            .ToListAsync(cancellationToken);

        var questionnaire = request.Questionnaire!;
        var pendingWithSameMentor = open.FirstOrDefault(x =>
            x.Status == SubscriptionStatus.PendingPayment && x.MentorProfileId == mentor.Id);
        if (pendingWithSameMentor is not null)
        {
            // Klijent se vratio na plaćanje: vraća se postojeća pretplata sa ažuriranim upitnikom.
            ApplyQuestionnaire(pendingWithSameMentor.Questionnaire ??= new Questionnaire(), questionnaire);
            pendingWithSameMentor.Price = mentor.MonthlyPrice;
            await db.SaveChangesAsync(cancellationToken);
            return await GetMineByIdAsync(clientUserId, pendingWithSameMentor.Id, cancellationToken);
        }
        if (open.Count > 0) throw new ConflictException(AlreadyCollaborating);

        var subscription = new Subscription
        {
            ClientProfileId = client.Id,
            MentorProfileId = mentor.Id,
            Status = SubscriptionStatus.PendingPayment,
            Price = mentor.MonthlyPrice,
            Currency = paymentGateway.Currency,
            CreatedAt = DateTime.UtcNow,
            Questionnaire = new Questionnaire()
        };
        ApplyQuestionnaire(subscription.Questionnaire, questionnaire);
        db.Subscriptions.Add(subscription);
        await db.SaveChangesAsync(cancellationToken);
        return await GetMineByIdAsync(clientUserId, subscription.Id, cancellationToken);
    }

    public async Task<List<SubscriptionDto>> GetMineAsync(int clientUserId, SubscriptionStatus? status, CancellationToken cancellationToken = default)
    {
        var client = await GetClientAsync(clientUserId, cancellationToken);
        var query = ClientQuery().Where(x => x.ClientProfileId == client.Id);
        if (status is { } value) query = query.Where(x => x.Status == value);
        var subscriptions = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(cancellationToken);
        return subscriptions.Select(x => Fill(new SubscriptionDto(), x)).ToList();
    }

    public async Task<SubscriptionDetailDto> GetMineByIdAsync(int clientUserId, int id, CancellationToken cancellationToken = default)
    {
        var client = await GetClientAsync(clientUserId, cancellationToken);
        var subscription = await ClientQuery()
            .Include(x => x.Questionnaire)
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == id && x.ClientProfileId == client.Id, cancellationToken)
            ?? throw new NotFoundException(DomainTexts.SubscriptionNotFound);

        var dto = Fill(new SubscriptionDetailDto(), subscription);
        dto.Questionnaire = subscription.Questionnaire is { } q ? ToQuestionnaireDto(q) : null;
        dto.Payments = subscription.Payments.OrderByDescending(x => x.CreatedAt).Select(x => new PaymentItemDto
        {
            Amount = x.Amount, Currency = x.Currency, Purpose = x.Purpose, Status = x.Status, CreatedAt = x.CreatedAt, PaidAt = x.PaidAt
        }).ToList();
        return dto;
    }

    public async Task<SubscriptionDto> CancelAsync(int clientUserId, int id, CancellationToken cancellationToken = default)
    {
        var client = await GetClientAsync(clientUserId, cancellationToken);
        var subscription = await WorkflowQuery().FirstOrDefaultAsync(x => x.Id == id && x.ClientProfileId == client.Id, cancellationToken)
                           ?? throw new NotFoundException(DomainTexts.SubscriptionNotFound);

        if (subscription.Status is not (SubscriptionStatus.PendingPayment or SubscriptionStatus.Active))
            throw new ValidationException("Pretplatu možete otkazati samo dok čeka plaćanje ili dok je aktivna.");

        var wasActive = subscription.Status == SubscriptionStatus.Active;
        workflow.Cancel(subscription, DomainTexts.ClientCancelledReason, DateTime.UtcNow, notifyClient: false, notifyMentor: wasActive);
        await db.SaveChangesAsync(cancellationToken);

        var reloaded = await ClientQuery().FirstAsync(x => x.Id == id, cancellationToken);
        return Fill(new SubscriptionDto(), reloaded);
    }

    public async Task<List<AdminSubscriptionDto>> GetAllAsync(SubscriptionSearchObject search, CancellationToken cancellationToken = default)
    {
        var query = db.Subscriptions.AsNoTracking();
        if (search.Search.NormalizeSearch() is { } term)
            query = query.Where(x => (x.ClientProfile.User.FirstName + " " + x.ClientProfile.User.LastName).Contains(term) ||
                                     (x.MentorProfile.User.FirstName + " " + x.MentorProfile.User.LastName).Contains(term));
        if (search.Status is { } status) query = query.Where(x => x.Status == status);

        return await query.OrderByDescending(x => x.CreatedAt).Select(AdminProjection).ToListAsync(cancellationToken);
    }

    public async Task<AdminSubscriptionDto> AdminCancelAsync(int id, CancelSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        var subscription = await WorkflowQuery().FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                           ?? throw new NotFoundException(DomainTexts.SubscriptionNotFound);
        workflow.Cancel(subscription, request.Reason.Trim(), DateTime.UtcNow, notifyClient: true, notifyMentor: true);
        await db.SaveChangesAsync(cancellationToken);
        return await db.Subscriptions.AsNoTracking().Where(x => x.Id == id).Select(AdminProjection).FirstAsync(cancellationToken);
    }

    public static QuestionnaireDto ToQuestionnaireDto(Questionnaire q) => new()
    {
        PrimaryGoal = q.PrimaryGoal,
        TimeCommitment = q.TimeCommitment,
        HealthIssues = q.HealthIssues,
        Medications = q.Medications,
        WeeklySessions = q.WeeklySessions,
        OutsideActivity = q.OutsideActivity
    };

    private static readonly System.Linq.Expressions.Expression<Func<Subscription, AdminSubscriptionDto>> AdminProjection = x => new AdminSubscriptionDto
    {
        Id = x.Id,
        ClientFullName = x.ClientProfile.User.FirstName + " " + x.ClientProfile.User.LastName,
        MentorFullName = x.MentorProfile.User.FirstName + " " + x.MentorProfile.User.LastName,
        TrainingTypeName = x.MentorProfile.TrainingType.Name,
        Status = x.Status,
        Price = x.Price,
        Currency = x.Currency,
        CreatedAt = x.CreatedAt,
        StartDate = x.StartDate,
        EndDate = x.EndDate,
        StatusReason = x.StatusReason
    };

    private static void ApplyQuestionnaire(Questionnaire target, QuestionnaireRequest source)
    {
        target.PrimaryGoal = source.PrimaryGoal.Trim();
        target.TimeCommitment = source.TimeCommitment.Trim();
        target.HealthIssues = source.HealthIssues.Trim();
        target.Medications = source.Medications.Trim();
        target.WeeklySessions = source.WeeklySessions.Trim();
        target.OutsideActivity = source.OutsideActivity.Trim();
    }

    private static T Fill<T>(T dto, Subscription x) where T : SubscriptionDto
    {
        var mentorUser = x.MentorProfile.User;
        var mentorAvailable = !mentorUser.IsDeleted && mentorUser.IsActive && x.MentorProfile.Status == MentorApprovalStatus.Approved;
        dto.Id = x.Id;
        dto.MentorProfileId = x.MentorProfileId;
        dto.MentorFullName = mentorUser.FullName;
        dto.MentorPhotoUrl = mentorUser.ProfileImageUrl;
        dto.TrainingTypeName = x.MentorProfile.TrainingType.Name;
        dto.Status = x.Status;
        dto.Price = x.Price;
        dto.Currency = x.Currency;
        dto.CreatedAt = x.CreatedAt;
        dto.StartDate = x.StartDate;
        dto.EndDate = x.EndDate;
        dto.StatusReason = x.StatusReason;
        dto.ReviewId = x.Review?.Id;
        dto.CanReview = x.AcceptedAt is not null && x.Review is null &&
                        x.Status is SubscriptionStatus.Active or SubscriptionStatus.Expired or SubscriptionStatus.Cancelled;
        dto.CanRenew = x.Status == SubscriptionStatus.Active && mentorAvailable;
        dto.CanCancel = x.Status is SubscriptionStatus.PendingPayment or SubscriptionStatus.Active;
        return dto;
    }

    private IQueryable<Subscription> ClientQuery() => db.Subscriptions.AsNoTracking()
        .Include(x => x.MentorProfile).ThenInclude(x => x.User)
        .Include(x => x.MentorProfile).ThenInclude(x => x.TrainingType)
        .Include(x => x.Review);

    private IQueryable<Subscription> WorkflowQuery() => db.Subscriptions
        .Include(x => x.ClientProfile).ThenInclude(x => x.User)
        .Include(x => x.MentorProfile).ThenInclude(x => x.User);

    private async Task<ClientProfile> GetClientAsync(int clientUserId, CancellationToken cancellationToken) =>
        await db.ClientProfiles.FirstOrDefaultAsync(x => x.UserId == clientUserId, cancellationToken)
        ?? throw new NotFoundException(DomainTexts.ProfileMissing);
}

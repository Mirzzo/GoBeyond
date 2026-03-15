using GoBeyond.API.Extensions;
using GoBeyond.API.Utilities;
using GoBeyond.Core.DTOs;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Infrastructure.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.API.Controllers;

[Authorize]
[ApiController]
[Route("api/subscriptions")]
public class SubscriptionsController(GoBeyondDbContext dbContext) : ControllerBase
{
    [Authorize(Policy = "ClientOnly")]
    [HttpPost]
    public async Task<SubscriptionDetailDto> Create(
        [FromBody] CreateSubscriptionRequestDto request,
        CancellationToken cancellationToken)
    {
        var clientUserId = User.GetUserId();

        var client = await dbContext.Users
            .Include(x => x.ClientProfile)
                .ThenInclude(x => x!.Subscriptions)
            .FirstOrDefaultAsync(x => x.Id == clientUserId && x.ClientProfile != null, cancellationToken)
            ?? throw new InvalidOperationException("Client profile not found.");

        var mentor = await dbContext.MentorProfiles
            .Include(x => x.User)
            .FirstOrDefaultAsync(x =>
                x.Id == request.MentorId &&
                x.Status == MentorApprovalStatus.Approved &&
                x.User.IsActive,
                cancellationToken)
            ?? throw new InvalidOperationException("Mentor is not available.");

        var existing = await dbContext.Subscriptions
            .AnyAsync(x =>
                x.ClientProfileId == client.ClientProfile!.Id &&
                x.MentorProfileId == mentor.Id &&
                (x.Status == SubscriptionStatus.Pending || x.Status == SubscriptionStatus.Active),
                cancellationToken);

        if (existing)
        {
            throw new InvalidOperationException("You already have an active onboarding flow with this mentor.");
        }

        var now = DateTime.UtcNow.Date;
        var subscription = new Subscription
        {
            ClientProfileId = client.ClientProfile!.Id,
            MentorProfileId = mentor.Id,
            Status = SubscriptionStatus.Pending,
            StartDate = now,
            EndDate = now.AddDays(28),
            AmountPaid = mentor.Price,
            Questionnaire = new Questionnaire
            {
                ClientProfileId = client.ClientProfile.Id,
                PrimaryGoal = request.PrimaryGoal.Trim(),
                TimeCommitment = request.TimeCommitment.Trim(),
                HealthIssues = request.HealthIssues.Trim(),
                Medications = request.Medications.Trim(),
                WeeklyAvailability = request.WeeklyAvailability.Trim(),
                PhysicalActivityLevel = request.PhysicalActivityLevel.Trim()
            }
        };

        dbContext.Subscriptions.Add(subscription);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.Notifications.Add(new Notification
        {
            UserId = mentor.UserId,
            Title = "New collaboration request",
            Body = $"{client.FirstName} {client.LastName}".Trim() + " submitted a questionnaire and is waiting for onboarding.",
            Type = NotificationType.NewSubscriber,
            IsRead = false
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        var createdSubscription = await LoadSubscriptionAsync(subscription.Id, cancellationToken);
        return DtoMapper.ToSubscriptionDetailDto(createdSubscription);
    }

    [Authorize(Policy = "ClientOnly")]
    [HttpGet("my")]
    public async Task<IReadOnlyList<SubscriptionDto>> GetMySubscriptions(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var clientUserId = User.GetUserId();

        var query = dbContext.Subscriptions
            .Include(x => x.MentorProfile)
                .ThenInclude(x => x.User)
            .Include(x => x.Questionnaire)
            .Include(x => x.Payments)
            .Include(x => x.TrainingPlans)
            .Include(x => x.ClientProfile)
                .ThenInclude(x => x.User)
            .Where(x => x.ClientProfile.UserId == clientUserId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLowerInvariant();
            var hasStatusFilter = Enum.TryParse<SubscriptionStatus>(search, ignoreCase: true, out var parsedStatus);
            query = query.Where(x =>
                x.MentorProfile.User.FirstName.ToLower().Contains(normalizedSearch) ||
                x.MentorProfile.User.LastName.ToLower().Contains(normalizedSearch) ||
                (hasStatusFilter && x.Status == parsedStatus) ||
                (x.Questionnaire != null && x.Questionnaire.PrimaryGoal.ToLower().Contains(normalizedSearch)));
        }

        var subscriptions = await query
            .OrderByDescending(x => x.StartDate)
            .ToListAsync(cancellationToken);

        return subscriptions
            .Select(DtoMapper.ToSubscriptionDto)
            .ToList();
    }

    [Authorize(Policy = "MentorOrAdmin")]
    [HttpGet("{id:int}")]
    public async Task<SubscriptionDetailDto> GetById(int id, CancellationToken cancellationToken)
    {
        var subscription = await LoadSubscriptionAsync(id, cancellationToken);

        if (!User.IsInRole(UserRole.Admin.ToString()))
        {
            var mentorUserId = User.GetUserId();
            var mentorOwnsSubscription = await dbContext.MentorProfiles
                .AnyAsync(x => x.Id == subscription.MentorProfileId && x.UserId == mentorUserId, cancellationToken);

            if (!mentorOwnsSubscription)
            {
                throw new InvalidOperationException("Subscription is not assigned to the current mentor.");
            }
        }

        return DtoMapper.ToSubscriptionDetailDto(subscription);
    }

    [Authorize(Policy = "ClientOnly")]
    [HttpPost("{id:int}/cancel")]
    public async Task<SubscriptionDto> Cancel(int id, CancellationToken cancellationToken)
    {
        var clientUserId = User.GetUserId();

        var subscription = await dbContext.Subscriptions
            .Include(x => x.MentorProfile)
                .ThenInclude(x => x.User)
            .Include(x => x.ClientProfile)
                .ThenInclude(x => x.User)
            .Include(x => x.Questionnaire)
            .Include(x => x.Payments)
            .Include(x => x.TrainingPlans)
            .FirstOrDefaultAsync(x => x.Id == id && x.ClientProfile.UserId == clientUserId, cancellationToken)
            ?? throw new InvalidOperationException("Subscription not found.");

        subscription.Status = SubscriptionStatus.Cancelled;
        subscription.EndDate = DateTime.UtcNow.Date;

        var latestPayment = subscription.Payments
            .OrderByDescending(x => x.Id)
            .FirstOrDefault();

        if (latestPayment is not null && latestPayment.Status == PaymentStatus.Succeeded)
        {
            latestPayment.Status = PaymentStatus.Refunded;
        }

        dbContext.Notifications.Add(new Notification
        {
            UserId = subscription.MentorProfile.UserId,
            Title = "Subscription cancelled",
            Body = $"{subscription.ClientProfile.User.FirstName} {subscription.ClientProfile.User.LastName}".Trim() + " cancelled the subscription.",
            Type = NotificationType.SubscriptionExpiring,
            IsRead = false
        });

        await dbContext.SaveChangesAsync(cancellationToken);
        return DtoMapper.ToSubscriptionDto(subscription);
    }

    private async Task<Subscription> LoadSubscriptionAsync(int subscriptionId, CancellationToken cancellationToken)
        => await dbContext.Subscriptions
            .Include(x => x.MentorProfile)
                .ThenInclude(x => x.User)
            .Include(x => x.ClientProfile)
                .ThenInclude(x => x.User)
            .Include(x => x.Questionnaire)
            .Include(x => x.Payments)
            .Include(x => x.TrainingPlans)
            .FirstOrDefaultAsync(x => x.Id == subscriptionId, cancellationToken)
            ?? throw new InvalidOperationException("Subscription not found.");
}

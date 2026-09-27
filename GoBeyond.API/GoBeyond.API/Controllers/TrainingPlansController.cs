using GoBeyond.API.Extensions;
using GoBeyond.API.Utilities;
using GoBeyond.Core.DTOs;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Messaging;
using GoBeyond.Infrastructure.StateMachineServices.TrainingPlans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.API.Controllers;

[Authorize]
[ApiController]
[Route("api/training-plans")]
public class TrainingPlansController(
    GoBeyondDbContext dbContext,
    TrainingPlanStateFactory trainingPlanStateFactory,
    INotificationPublisher notificationPublisher) : ControllerBase
{
    [Authorize(Policy = "MentorOrAdmin")]
    [HttpGet]
    public async Task<IReadOnlyList<TrainingPlanSummaryDto>> GetPlans(
        [FromQuery] string? search,
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        var query = dbContext.TrainingPlans
            .Include(x => x.DayPlans)
            .Include(x => x.Sessions)
            .Include(x => x.ClientProfile)
                .ThenInclude(x => x.User)
            .Include(x => x.MentorProfile)
                .ThenInclude(x => x.User)
            .AsQueryable();

        if (!User.IsInRole(UserRole.Admin.ToString()))
        {
            var mentorUserId = User.GetUserId();
            var mentorProfileId = await dbContext.MentorProfiles
                .Where(x => x.UserId == mentorUserId)
                .Select(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);

            query = query.Where(x => x.MentorProfileId == mentorProfileId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.ClientProfile.User.FirstName.ToLower().Contains(normalizedSearch) ||
                x.ClientProfile.User.LastName.ToLower().Contains(normalizedSearch) ||
                x.MotivationalQuote.ToLower().Contains(normalizedSearch));
        }

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<TrainingPlanStatus>(status, ignoreCase: true, out var parsedStatus))
        {
            query = query.Where(x => x.Status == parsedStatus);
        }

        var plans = await query
            .OrderByDescending(x => x.WeekNumber)
            .ThenByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return plans
            .Select(DtoMapper.ToTrainingPlanSummary)
            .ToList();
    }

    [Authorize(Policy = "MentorOnly")]
    [HttpPost]
    public async Task<TrainingPlanDetailDto> Create(
        [FromBody] UpsertTrainingPlanRequestDto request,
        CancellationToken cancellationToken)
    {
        var mentor = await GetCurrentMentorProfileAsync(cancellationToken);
        EnsureDays(request.Days);

        var subscription = await dbContext.Subscriptions
            .Include(x => x.ClientProfile)
                .ThenInclude(x => x.User)
            .Include(x => x.MentorProfile)
                .ThenInclude(x => x.User)
            .FirstOrDefaultAsync(
                x => x.Id == request.SubscriptionId &&
                    x.MentorProfileId == mentor.Id &&
                    x.Status == SubscriptionStatus.Active,
                cancellationToken)
            ?? throw new InvalidOperationException("Active subscription not found for this mentor.");

        var existingPlan = await dbContext.TrainingPlans
            .AnyAsync(
                x => x.SubscriptionId == request.SubscriptionId && x.WeekNumber == request.WeekNumber,
                cancellationToken);

        if (existingPlan)
        {
            throw new InvalidOperationException("A training plan already exists for this subscription week.");
        }

        var trainingPlan = new TrainingPlan
        {
            SubscriptionId = subscription.Id,
            MentorProfileId = mentor.Id,
            ClientProfileId = subscription.ClientProfileId,
            MotivationalQuote = request.MotivationalQuote.Trim(),
            WeekNumber = request.WeekNumber,
            Status = TrainingPlanStatus.Draft,
            DayPlans = request.Days.Select(ToDayPlan).ToList()
        };

        dbContext.TrainingPlans.Add(trainingPlan);
        await dbContext.SaveChangesAsync(cancellationToken);

        var createdPlan = await LoadTrainingPlanAsync(trainingPlan.Id, cancellationToken);
        return DtoMapper.ToTrainingPlanDetail(createdPlan);
    }

    [Authorize(Policy = "MentorOrAdmin")]
    [HttpGet("{id:int}")]
    public async Task<TrainingPlanDetailDto> GetById(int id, CancellationToken cancellationToken)
    {
        var trainingPlan = await LoadTrainingPlanAsync(id, cancellationToken);
        await EnsurePlanAccessAsync(trainingPlan, cancellationToken);
        if (trainingPlan.Subscription.Status != SubscriptionStatus.Active)
            throw new InvalidOperationException("Only plans for active subscriptions can be edited.");

        return DtoMapper.ToTrainingPlanDetail(trainingPlan);
    }

    [Authorize(Policy = "MentorOnly")]
    [HttpPut("{id:int}")]
    public async Task<TrainingPlanDetailDto> Update(
        int id,
        [FromBody] UpsertTrainingPlanRequestDto request,
        CancellationToken cancellationToken)
    {
        EnsureDays(request.Days);

        var trainingPlan = await LoadTrainingPlanAsync(id, cancellationToken);
        await EnsurePlanAccessAsync(trainingPlan, cancellationToken);

        if (request.SubscriptionId != trainingPlan.SubscriptionId)
            throw new InvalidOperationException("A plan cannot be moved to another subscription.");
        if (await dbContext.TrainingPlans.AnyAsync(x => x.Id != id && x.SubscriptionId == request.SubscriptionId && x.WeekNumber == request.WeekNumber, cancellationToken))
            throw new InvalidOperationException("A training plan already exists for this subscription week.");

        trainingPlan.MotivationalQuote = request.MotivationalQuote.Trim();
        trainingPlan.WeekNumber = request.WeekNumber;
        var requestedDays = request.Days.Select(x => Enum.Parse<DayOfWeek>(x.DayOfWeek, true)).ToHashSet();
        foreach (var oldDay in trainingPlan.DayPlans.Where(x => !requestedDays.Contains(x.DayOfWeek)).ToList())
        {
            if (trainingPlan.Sessions.Any(x => x.DayPlanId == oldDay.Id))
                throw new InvalidOperationException("A completed training day cannot be removed from the plan.");
            dbContext.DayPlans.Remove(oldDay);
            trainingPlan.DayPlans.Remove(oldDay);
        }
        foreach (var day in request.Days)
        {
            var parsedDay = Enum.Parse<DayOfWeek>(day.DayOfWeek, true);
            var existing = trainingPlan.DayPlans.FirstOrDefault(x => x.DayOfWeek == parsedDay);
            if (existing is null) trainingPlan.DayPlans.Add(ToDayPlan(day));
            else
            {
                existing.TrainingDuration = TimeSpan.FromMinutes(day.TrainingDurationMinutes);
                existing.TrainingDescription = day.TrainingDescription.Trim();
                existing.NutritionDuration = TimeSpan.FromMinutes(day.NutritionDurationMinutes);
                existing.NutritionDescription = day.NutritionDescription.Trim();
            }
        }

        if (trainingPlan.Status == TrainingPlanStatus.Published)
        {
            dbContext.Notifications.Add(new Notification
            {
                UserId = trainingPlan.ClientProfile.UserId,
                Title = "Training plan updated",
                Body = $"Week {trainingPlan.WeekNumber} was updated by your mentor.",
                Type = NotificationType.PlanReady
            });
            await notificationPublisher.PublishAsync("plan.updated", trainingPlan.ClientProfile.User.Email,
                "Training plan updated", $"Week {trainingPlan.WeekNumber} was updated by your mentor.", cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var updatedPlan = await LoadTrainingPlanAsync(trainingPlan.Id, cancellationToken);
        return DtoMapper.ToTrainingPlanDetail(updatedPlan);
    }

    [Authorize(Policy = "MentorOnly")]
    [HttpPut("{id:int}/publish")]
    public async Task<TrainingPlanDetailDto> Publish(int id, CancellationToken cancellationToken)
    {
        var trainingPlan = await LoadTrainingPlanAsync(id, cancellationToken);
        await EnsurePlanAccessAsync(trainingPlan, cancellationToken);

        var state = trainingPlanStateFactory.Resolve(trainingPlan.Status);
        if (trainingPlan.Subscription.Status != SubscriptionStatus.Active)
            throw new InvalidOperationException("Only plans for active subscriptions can be published.");
        if (trainingPlan.DayPlans.Count == 0)
            throw new InvalidOperationException("At least one training day is required.");
        await state.PublishAsync(trainingPlan, cancellationToken);

        dbContext.Notifications.Add(new Notification
        {
            UserId = trainingPlan.ClientProfile.UserId,
            Title = "Training plan ready",
            Body = $"Week {trainingPlan.WeekNumber} is now available in your client app.",
            Type = NotificationType.PlanReady,
            IsRead = false
        });

        await notificationPublisher.PublishAsync("plan.published", trainingPlan.ClientProfile.User.Email,
            "Training plan ready", $"Week {trainingPlan.WeekNumber} is now available in your client app.", cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        var publishedPlan = await LoadTrainingPlanAsync(trainingPlan.Id, cancellationToken);
        return DtoMapper.ToTrainingPlanDetail(publishedPlan);
    }

    [Authorize(Policy = "MentorOrAdmin")]
    [HttpGet("by-subscription/{subscriptionId:int}")]
    public async Task<IReadOnlyList<TrainingPlanSummaryDto>> GetBySubscription(
        int subscriptionId,
        CancellationToken cancellationToken)
    {
        var plans = await dbContext.TrainingPlans
            .Include(x => x.DayPlans)
            .Include(x => x.Sessions)
            .Include(x => x.ClientProfile)
                .ThenInclude(x => x.User)
            .Include(x => x.MentorProfile)
                .ThenInclude(x => x.User)
            .Where(x => x.SubscriptionId == subscriptionId)
            .OrderByDescending(x => x.WeekNumber)
            .ToListAsync(cancellationToken);

        if (plans.Count == 0)
        {
            return [];
        }

        await EnsurePlanAccessAsync(plans[0], cancellationToken);

        return plans
            .Select(DtoMapper.ToTrainingPlanSummary)
            .ToList();
    }

    [Authorize(Policy = "ClientOnly")]
    [HttpGet("my-current")]
    public async Task<TrainingPlanDetailDto> GetMyCurrentPlan(CancellationToken cancellationToken)
    {
        var clientUserId = User.GetUserId();

        var trainingPlan = await dbContext.TrainingPlans
            .Include(x => x.DayPlans)
            .Include(x => x.Sessions)
            .Include(x => x.ClientProfile)
                .ThenInclude(x => x.User)
            .Include(x => x.MentorProfile)
                .ThenInclude(x => x.User)
            .Include(x => x.Subscription)
            .Where(x =>
                x.ClientProfile.UserId == clientUserId &&
                x.Status == TrainingPlanStatus.Published &&
                (x.Subscription.Status == SubscriptionStatus.Active || x.Subscription.Status == SubscriptionStatus.Cancelled))
            .OrderByDescending(x => x.Subscription.Status == SubscriptionStatus.Active)
            .ThenByDescending(x => x.WeekNumber)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("No published plan is available for the current client.");

        return DtoMapper.ToTrainingPlanDetail(trainingPlan);
    }

    [Authorize(Policy = "ClientOnly")]
    [HttpGet("history")]
    public async Task<IReadOnlyList<TrainingPlanSummaryDto>> GetMyPlanHistory([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var clientUserId = User.GetUserId();
        var query = dbContext.TrainingPlans
            .Include(x => x.DayPlans).Include(x => x.Sessions)
            .Include(x => x.ClientProfile).ThenInclude(x => x.User)
            .Include(x => x.MentorProfile).ThenInclude(x => x.User)
            .Where(x => x.ClientProfile.UserId == clientUserId && x.Status == TrainingPlanStatus.Published);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.MotivationalQuote.ToLower().Contains(term) ||
                x.MentorProfile.User.FirstName.ToLower().Contains(term) ||
                x.MentorProfile.User.LastName.ToLower().Contains(term));
        }
        var plans = await query.OrderByDescending(x => x.Id).ToListAsync(cancellationToken);
        return plans.Select(DtoMapper.ToTrainingPlanSummary).ToList();
    }

    [Authorize(Policy = "ClientOnly")]
    [HttpGet("history/{id:int}")]
    public async Task<TrainingPlanDetailDto> GetMyHistoricalPlan(int id, CancellationToken cancellationToken)
    {
        var plan = await LoadTrainingPlanAsync(id, cancellationToken);
        if (plan.ClientProfile.UserId != User.GetUserId() || plan.Status != TrainingPlanStatus.Published)
            throw new InvalidOperationException("Published plan not found for this client.");
        return DtoMapper.ToTrainingPlanDetail(plan);
    }

    [Authorize(Policy = "ClientOnly")]
    [HttpPost("{planId:int}/days/{dayId:int}/complete")]
    public async Task<TrainingSessionDto> CompleteDay(int planId, int dayId, [FromBody] CompleteTrainingDayRequestDto request, CancellationToken cancellationToken)
    {
        var plan = await LoadTrainingPlanAsync(planId, cancellationToken);
        if (plan.ClientProfile.UserId != User.GetUserId() || plan.Status != TrainingPlanStatus.Published ||
            !plan.DayPlans.Any(x => x.Id == dayId))
            throw new InvalidOperationException("Published training day not found for this client.");

        var session = await dbContext.TrainingSessions.FirstOrDefaultAsync(
            x => x.ClientProfileId == plan.ClientProfileId && x.DayPlanId == dayId, cancellationToken);
        if (session is null)
        {
            session = new TrainingSession
            {
                ClientProfileId = plan.ClientProfileId,
                TrainingPlanId = planId,
                DayPlanId = dayId
            };
            dbContext.TrainingSessions.Add(session);
        }
        session.Repetitions = request.Repetitions;
        session.CompletedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new TrainingSessionDto(session.Id, planId, dayId, session.Repetitions, session.CompletedAt);
    }

    private async Task<MentorProfile> GetCurrentMentorProfileAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        return await dbContext.MentorProfiles
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Mentor profile not found.");
    }

    private async Task<TrainingPlan> LoadTrainingPlanAsync(int id, CancellationToken cancellationToken)
        => await dbContext.TrainingPlans
            .Include(x => x.DayPlans)
            .Include(x => x.Sessions)
            .Include(x => x.ClientProfile)
                .ThenInclude(x => x.User)
            .Include(x => x.MentorProfile)
                .ThenInclude(x => x.User)
            .Include(x => x.Subscription)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("Training plan not found.");

    private async Task EnsurePlanAccessAsync(TrainingPlan trainingPlan, CancellationToken cancellationToken)
    {
        if (User.IsInRole(UserRole.Admin.ToString()))
        {
            return;
        }

        var mentorUserId = User.GetUserId();
        var ownsPlan = await dbContext.MentorProfiles
            .AnyAsync(x => x.Id == trainingPlan.MentorProfileId && x.UserId == mentorUserId, cancellationToken);

        if (!ownsPlan)
        {
            throw new InvalidOperationException("Training plan is not assigned to the current mentor.");
        }
    }

    private static DayPlan ToDayPlan(UpsertTrainingPlanDayRequestDto request)
    {
        if (!Enum.TryParse<DayOfWeek>(request.DayOfWeek, ignoreCase: true, out var dayOfWeek))
        {
            throw new InvalidOperationException("Invalid day of week.");
        }

        return new DayPlan
        {
            DayOfWeek = dayOfWeek,
            TrainingDuration = TimeSpan.FromMinutes(request.TrainingDurationMinutes),
            TrainingDescription = request.TrainingDescription.Trim(),
            NutritionDuration = TimeSpan.FromMinutes(request.NutritionDurationMinutes),
            NutritionDescription = request.NutritionDescription.Trim()
        };
    }

    private static void EnsureDays(IReadOnlyList<UpsertTrainingPlanDayRequestDto>? days)
    {
        if (days is null || days.Count == 0)
        {
            throw new InvalidOperationException("At least one day plan is required.");
        }

        if (days.Any(x => !Enum.TryParse<DayOfWeek>(x.DayOfWeek, true, out var parsed) || !Enum.IsDefined(parsed)))
            throw new InvalidOperationException("Every training day must be a valid weekday.");

        var duplicateDay = days
            .GroupBy(x => x.DayOfWeek.Trim().ToLowerInvariant())
            .FirstOrDefault(x => x.Count() > 1);

        if (duplicateDay is not null)
        {
            throw new InvalidOperationException("Each day can appear only once in a training plan.");
        }
    }
}

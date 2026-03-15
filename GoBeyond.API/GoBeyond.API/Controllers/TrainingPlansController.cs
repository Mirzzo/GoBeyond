using GoBeyond.API.Extensions;
using GoBeyond.API.Utilities;
using GoBeyond.Core.DTOs;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Infrastructure.Database;
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
    TrainingPlanStateFactory trainingPlanStateFactory) : ControllerBase
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

        trainingPlan.MotivationalQuote = request.MotivationalQuote.Trim();
        trainingPlan.WeekNumber = request.WeekNumber;

        dbContext.DayPlans.RemoveRange(trainingPlan.DayPlans);
        trainingPlan.DayPlans.Clear();
        foreach (var day in request.Days)
        {
            trainingPlan.DayPlans.Add(ToDayPlan(day));
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
        await state.PublishAsync(trainingPlan, cancellationToken);

        dbContext.Notifications.Add(new Notification
        {
            UserId = trainingPlan.ClientProfile.UserId,
            Title = "Training plan ready",
            Body = $"Week {trainingPlan.WeekNumber} is now available in your client app.",
            Type = NotificationType.PlanReady,
            IsRead = false
        });

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
            .Include(x => x.ClientProfile)
                .ThenInclude(x => x.User)
            .Include(x => x.MentorProfile)
                .ThenInclude(x => x.User)
            .Include(x => x.Subscription)
            .Where(x =>
                x.ClientProfile.UserId == clientUserId &&
                x.Status == TrainingPlanStatus.Published &&
                x.Subscription.Status == SubscriptionStatus.Active)
            .OrderByDescending(x => x.WeekNumber)
            .ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("No published plan is available for the current client.");

        return DtoMapper.ToTrainingPlanDetail(trainingPlan);
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

    private static void EnsureDays(IReadOnlyList<UpsertTrainingPlanDayRequestDto> days)
    {
        if (days.Count == 0)
        {
            throw new InvalidOperationException("At least one day plan is required.");
        }

        var duplicateDay = days
            .GroupBy(x => x.DayOfWeek.Trim().ToLowerInvariant())
            .FirstOrDefault(x => x.Count() > 1);

        if (duplicateDay is not null)
        {
            throw new InvalidOperationException("Each day can appear only once in a training plan.");
        }
    }
}

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

[ApiController]
[Route("api/mentors")]
public class MentorsController(GoBeyondDbContext dbContext) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public async Task<IReadOnlyList<MentorSummaryDto>> GetMentors(
        [FromQuery] string? search,
        [FromQuery] string? category,
        CancellationToken cancellationToken)
    {
        var query = dbContext.MentorProfiles
            .Include(x => x.User)
            .Include(x => x.Reviews)
            .Include(x => x.Subscriptions)
            .Where(x => x.Status == MentorApprovalStatus.Approved && x.User.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLowerInvariant();
            var hasCategorySearch = Enum.TryParse<MentorCategory>(search, ignoreCase: true, out var parsedCategory);
            query = query.Where(x =>
                x.User.FirstName.ToLower().Contains(normalizedSearch) ||
                x.User.LastName.ToLower().Contains(normalizedSearch) ||
                x.User.Email.ToLower().Contains(normalizedSearch) ||
                x.Bio.ToLower().Contains(normalizedSearch) ||
                (hasCategorySearch && x.Category == parsedCategory));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            if (!Enum.TryParse<MentorCategory>(category, ignoreCase: true, out var parsedCategory))
            {
                throw new InvalidOperationException("Invalid mentor category filter.");
            }

            query = query.Where(x => x.Category == parsedCategory);
        }

        var mentors = await query
            .OrderBy(x => x.User.FirstName)
            .ThenBy(x => x.User.LastName)
            .ToListAsync(cancellationToken);

        return mentors
            .Select(DtoMapper.ToMentorSummary)
            .ToList();
    }

    [AllowAnonymous]
    [HttpGet("{id:int}")]
    public async Task<MentorDetailDto> GetMentorById(int id, CancellationToken cancellationToken)
    {
        var mentor = await dbContext.MentorProfiles
            .Include(x => x.User)
            .Include(x => x.Certificates)
            .Include(x => x.Reviews)
            .Include(x => x.Subscriptions)
            .FirstOrDefaultAsync(x => x.Id == id && x.User.IsActive, cancellationToken)
            ?? throw new InvalidOperationException("Mentor not found.");

        if (mentor.Status != MentorApprovalStatus.Approved)
        {
            throw new InvalidOperationException("Mentor is not publicly available.");
        }

        return DtoMapper.ToMentorDetail(mentor);
    }

    [AllowAnonymous]
    [HttpGet("{id:int}/reviews")]
    public async Task<IReadOnlyList<ReviewDto>> GetMentorReviews(int id, CancellationToken cancellationToken)
    {
        var reviews = await dbContext.Reviews
            .Include(x => x.ClientProfile)
                .ThenInclude(x => x.User)
            .Where(x => x.MentorProfileId == id)
            .OrderByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return reviews
            .Select(DtoMapper.ToReviewDto)
            .ToList();
    }

    [Authorize(Policy = "MentorOnly")]
    [HttpPut("profile")]
    public async Task<MentorDetailDto> UpdateProfile(
        [FromBody] UpdateMentorProfileRequestDto request,
        CancellationToken cancellationToken)
    {
        var mentor = await GetCurrentMentorProfileAsync(
            includeCertificates: true,
            includeReviews: true,
            includeSubscriptions: true,
            cancellationToken: cancellationToken);

        mentor.Bio = request.Bio.Trim();
        mentor.Age = request.Age;
        mentor.Category = DtoMapper.ParseMentorCategory(request.Category);
        mentor.Price = request.Price;
        mentor.Status = MentorApprovalStatus.Pending;

        await dbContext.SaveChangesAsync(cancellationToken);
        return DtoMapper.ToMentorDetail(mentor);
    }

    [Authorize(Policy = "MentorOnly")]
    [HttpPost("certificates")]
    public async Task<IReadOnlyList<FileReferenceDto>> UploadCertificate(
        [FromBody] UploadCertificateRequestDto request,
        CancellationToken cancellationToken)
    {
        var mentor = await GetCurrentMentorProfileAsync(includeCertificates: true, cancellationToken: cancellationToken);

        mentor.Certificates.Add(new MentorCertificate
        {
            FileName = request.FileName.Trim(),
            FileUrl = request.FileUrl.Trim()
        });
        mentor.Status = MentorApprovalStatus.Pending;

        await dbContext.SaveChangesAsync(cancellationToken);

        return mentor.Certificates
            .OrderBy(x => x.Id)
            .Select(x => new FileReferenceDto(x.FileName, x.FileUrl))
            .ToList();
    }

    [Authorize(Policy = "MentorOnly")]
    [HttpGet("collaboration-requests")]
    public async Task<IReadOnlyList<CollaborationRequestDto>> GetCollaborationRequests(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var mentor = await GetCurrentMentorProfileAsync(cancellationToken: cancellationToken);

        var query = dbContext.Subscriptions
            .Include(x => x.ClientProfile)
                .ThenInclude(x => x.User)
            .Include(x => x.Questionnaire)
            .Include(x => x.TrainingPlans)
            .Where(x =>
                x.MentorProfileId == mentor.Id &&
                x.Status == SubscriptionStatus.Active &&
                x.Questionnaire != null &&
                !x.TrainingPlans.Any(y => y.Status == TrainingPlanStatus.Published));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.ClientProfile.User.FirstName.ToLower().Contains(normalizedSearch) ||
                x.ClientProfile.User.LastName.ToLower().Contains(normalizedSearch) ||
                x.ClientProfile.User.Email.ToLower().Contains(normalizedSearch) ||
                x.Questionnaire!.PrimaryGoal.ToLower().Contains(normalizedSearch) ||
                x.ClientProfile.FitnessLevel.ToLower().Contains(normalizedSearch));
        }

        var collaborationRequests = await query
            .OrderBy(x => x.StartDate)
            .ToListAsync(cancellationToken);

        return collaborationRequests
            .Select(DtoMapper.ToCollaborationRequest)
            .ToList();
    }

    [Authorize(Policy = "MentorOnly")]
    [HttpGet("subscribers")]
    public async Task<IReadOnlyList<MentorSubscriberDto>> GetSubscribers(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        var mentor = await GetCurrentMentorProfileAsync(cancellationToken: cancellationToken);

        var query = dbContext.Subscriptions
            .Include(x => x.ClientProfile)
                .ThenInclude(x => x.User)
            .Include(x => x.Questionnaire)
            .Include(x => x.TrainingPlans)
            .Where(x => x.MentorProfileId == mentor.Id && x.Status == SubscriptionStatus.Active);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.ClientProfile.User.FirstName.ToLower().Contains(normalizedSearch) ||
                x.ClientProfile.User.LastName.ToLower().Contains(normalizedSearch) ||
                x.ClientProfile.User.Email.ToLower().Contains(normalizedSearch) ||
                (x.Questionnaire != null && x.Questionnaire.PrimaryGoal.ToLower().Contains(normalizedSearch)));
        }

        var subscriptions = await query
            .OrderByDescending(x => x.StartDate)
            .ToListAsync(cancellationToken);

        var clientProfileIds = subscriptions
            .Select(x => x.ClientProfileId)
            .Distinct()
            .ToList();

        var latestProgressByClient = await dbContext.ProgressEntries
            .Where(x => clientProfileIds.Contains(x.ClientProfileId))
            .GroupBy(x => x.ClientProfileId)
            .Select(x => x
                .OrderByDescending(y => y.Year)
                .ThenByDescending(y => y.Month)
                .First())
            .ToListAsync(cancellationToken);

        var progressLookup = latestProgressByClient.ToDictionary(x => x.ClientProfileId, x => x);

        return subscriptions
            .Select(x => DtoMapper.ToMentorSubscriber(
                x,
                progressLookup.GetValueOrDefault(x.ClientProfileId)))
            .ToList();
    }

    [Authorize(Policy = "MentorOnly")]
    [HttpGet("clients/{clientId:int}")]
    public async Task<ClientDetailDto> GetClientDetails(int clientId, CancellationToken cancellationToken)
    {
        var mentor = await GetCurrentMentorProfileAsync(cancellationToken: cancellationToken);

        var subscription = await dbContext.Subscriptions
            .Include(x => x.ClientProfile)
                .ThenInclude(x => x.User)
            .Include(x => x.Questionnaire)
            .Where(x => x.MentorProfileId == mentor.Id && x.ClientProfile.UserId == clientId)
            .OrderByDescending(x => x.StartDate)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Client relationship not found.");

        var progressEntries = await dbContext.ProgressEntries
            .Where(x => x.ClientProfileId == subscription.ClientProfileId)
            .OrderByDescending(x => x.Year)
            .ThenByDescending(x => x.Month)
            .ToListAsync(cancellationToken);

        return DtoMapper.ToClientDetail(subscription, progressEntries);
    }

    [Authorize(Policy = "ClientOnly")]
    [HttpGet("recommended")]
    public async Task<IReadOnlyList<MentorSummaryDto>> GetRecommendedMentors(CancellationToken cancellationToken)
    {
        var clientUserId = User.GetUserId();

        var client = await dbContext.Users
            .Include(x => x.ClientProfile)
            .Include(x => x.ClientProfile!.Subscriptions)
                .ThenInclude(x => x.Questionnaire)
            .FirstOrDefaultAsync(x => x.Id == clientUserId && x.ClientProfile != null, cancellationToken)
            ?? throw new InvalidOperationException("Client profile not found.");

        var latestQuestionnaire = client.ClientProfile!.Subscriptions
            .Where(x => x.Questionnaire != null)
            .OrderByDescending(x => x.StartDate)
            .Select(x => x.Questionnaire)
            .FirstOrDefault();

        var mentors = await dbContext.MentorProfiles
            .Include(x => x.User)
            .Include(x => x.Reviews)
            .Include(x => x.Subscriptions)
            .Where(x => x.Status == MentorApprovalStatus.Approved && x.User.IsActive)
            .ToListAsync(cancellationToken);

        var normalizedGoal = latestQuestionnaire?.PrimaryGoal.ToLowerInvariant() ?? client.ClientProfile.FitnessLevel.ToLowerInvariant();

        var ordered = mentors
            .Select(x => new
            {
                Mentor = x,
                Score = BuildRecommendationScore(x, normalizedGoal, client.ClientProfile.FitnessLevel)
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Mentor.User.FirstName)
            .Take(5)
            .Select(x => DtoMapper.ToMentorSummary(x.Mentor))
            .ToList();

        return ordered;
    }

    private async Task<MentorProfile> GetCurrentMentorProfileAsync(
        bool includeCertificates = false,
        bool includeReviews = false,
        bool includeSubscriptions = false,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserId();

        IQueryable<MentorProfile> query = dbContext.MentorProfiles
            .Include(x => x.User)
            .Where(x => x.UserId == userId);

        if (includeCertificates)
        {
            query = query.Include(x => x.Certificates);
        }

        if (includeReviews)
        {
            query = query
                .Include(x => x.Reviews)
                    .ThenInclude(x => x.ClientProfile)
                        .ThenInclude(x => x.User);
        }

        if (includeSubscriptions)
        {
            query = query.Include(x => x.Subscriptions);
        }

        return await query.FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Mentor profile not found.");
    }

    private static int BuildRecommendationScore(MentorProfile mentor, string normalizedGoal, string fitnessLevel)
    {
        var score = mentor.Reviews.Count == 0
            ? 20
            : (int)Math.Round(mentor.Reviews.Average(x => x.Rating) * 20);

        score += Math.Max(0, 20 - mentor.Subscriptions.Count(x => x.Status == SubscriptionStatus.Active));

        if (normalizedGoal.Contains("strength") || normalizedGoal.Contains("muscle") || fitnessLevel.Contains("advanced"))
        {
            if (mentor.Category is MentorCategory.Hybrid or MentorCategory.Weightlifting)
            {
                score += 25;
            }
        }

        if (normalizedGoal.Contains("bodyweight") || normalizedGoal.Contains("mobility") || normalizedGoal.Contains("consistency"))
        {
            if (mentor.Category is MentorCategory.Hybrid or MentorCategory.Calisthenics)
            {
                score += 20;
            }
        }

        if (normalizedGoal.Contains("fat") || normalizedGoal.Contains("conditioning") || normalizedGoal.Contains("habit"))
        {
            if (mentor.Category == MentorCategory.Hybrid)
            {
                score += 18;
            }
        }

        if (mentor.Bio.Contains("recovery", StringComparison.OrdinalIgnoreCase))
        {
            score += 8;
        }

        return score;
    }
}

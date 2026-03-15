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
[Route("api/reviews")]
public class ReviewsController(GoBeyondDbContext dbContext) : ControllerBase
{
    [Authorize(Policy = "ClientOnly")]
    [HttpPost]
    public async Task<ReviewDto> Create(
        [FromBody] CreateReviewRequestDto request,
        CancellationToken cancellationToken)
    {
        var clientUserId = User.GetUserId();

        var client = await dbContext.Users
            .Include(x => x.ClientProfile)
            .FirstOrDefaultAsync(x => x.Id == clientUserId && x.ClientProfile != null, cancellationToken)
            ?? throw new InvalidOperationException("Client profile not found.");

        var mentor = await dbContext.MentorProfiles
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == request.MentorId && x.Status == MentorApprovalStatus.Approved, cancellationToken)
            ?? throw new InvalidOperationException("Mentor not found.");

        var hasRelationship = await dbContext.Subscriptions
            .AnyAsync(x =>
                x.ClientProfileId == client.ClientProfile!.Id &&
                x.MentorProfileId == mentor.Id,
                cancellationToken);

        if (!hasRelationship)
        {
            throw new InvalidOperationException("You can only review mentors you have subscribed to.");
        }

        var review = await dbContext.Reviews
            .Include(x => x.ClientProfile)
                .ThenInclude(x => x.User)
            .FirstOrDefaultAsync(
                x => x.ClientProfileId == client.ClientProfile.Id && x.MentorProfileId == mentor.Id,
                cancellationToken);

        if (review is null)
        {
            review = new Review
            {
                ClientProfileId = client.ClientProfile.Id,
                MentorProfileId = mentor.Id,
                Rating = request.Rating,
                Comment = request.Comment.Trim()
            };

            dbContext.Reviews.Add(review);
            await dbContext.SaveChangesAsync(cancellationToken);

            review = await dbContext.Reviews
                .Include(x => x.ClientProfile)
                    .ThenInclude(x => x.User)
                .FirstAsync(x => x.Id == review.Id, cancellationToken);
        }
        else
        {
            review.Rating = request.Rating;
            review.Comment = request.Comment.Trim();
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return DtoMapper.ToReviewDto(review);
    }

    [AllowAnonymous]
    [HttpGet("mentor/{mentorId:int}")]
    public async Task<IReadOnlyList<ReviewDto>> GetMentorReviews(int mentorId, CancellationToken cancellationToken)
    {
        var reviews = await dbContext.Reviews
            .Include(x => x.ClientProfile)
                .ThenInclude(x => x.User)
            .Where(x => x.MentorProfileId == mentorId)
            .OrderByDescending(x => x.Id)
            .ToListAsync(cancellationToken);

        return reviews
            .Select(DtoMapper.ToReviewDto)
            .ToList();
    }
}

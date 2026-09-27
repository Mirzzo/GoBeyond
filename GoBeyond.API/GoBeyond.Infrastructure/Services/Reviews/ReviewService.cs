using GoBeyond.Core.DTOs.Communication;
using GoBeyond.Core.DTOs.Mentors;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Services.Reviews;

public interface IReviewService
{
    Task<ReviewDto> CreateAsync(int clientUserId, CreateReviewRequest request, CancellationToken cancellationToken = default);
    Task<ReviewDto> UpdateAsync(int clientUserId, int reviewId, UpdateReviewRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int clientUserId, int reviewId, CancellationToken cancellationToken = default);
}

public sealed class ReviewService(GoBeyondDbContext db) : IReviewService
{
    public async Task<ReviewDto> CreateAsync(int clientUserId, CreateReviewRequest request, CancellationToken cancellationToken = default)
    {
        var subscription = await db.Subscriptions
                               .Include(x => x.ClientProfile).ThenInclude(x => x.User)
                               .Include(x => x.Review)
                               .FirstOrDefaultAsync(x => x.Id == request.SubscriptionId && x.ClientProfile.UserId == clientUserId, cancellationToken)
                           ?? throw new NotFoundException(DomainTexts.SubscriptionNotFound);

        var wasAccepted = subscription.AcceptedAt is not null &&
                          subscription.Status is SubscriptionStatus.Active or SubscriptionStatus.Expired or SubscriptionStatus.Cancelled;
        if (!wasAccepted)
            throw new ValidationException("Recenziju možete ostaviti samo za saradnju koju je mentor prihvatio.");
        if (subscription.Review is not null)
            throw new ConflictException("Već ste ostavili recenziju za ovu saradnju. Možete je urediti.");

        var review = new Review
        {
            SubscriptionId = subscription.Id,
            ClientProfileId = subscription.ClientProfileId,
            MentorProfileId = subscription.MentorProfileId,
            Rating = request.Rating,
            Comment = request.Comment.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        db.Reviews.Add(review);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("Već ste ostavili recenziju za ovu saradnju. Možete je urediti.");
        }
        review.ClientProfile = subscription.ClientProfile;
        return ToDto(review, clientUserId);
    }

    public async Task<ReviewDto> UpdateAsync(int clientUserId, int reviewId, UpdateReviewRequest request, CancellationToken cancellationToken = default)
    {
        var review = await LoadOwnAsync(clientUserId, reviewId, cancellationToken);
        review.Rating = request.Rating;
        review.Comment = request.Comment.Trim();
        review.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(review, clientUserId);
    }

    public async Task DeleteAsync(int clientUserId, int reviewId, CancellationToken cancellationToken = default)
    {
        var review = await LoadOwnAsync(clientUserId, reviewId, cancellationToken);
        db.Reviews.Remove(review);
        await db.SaveChangesAsync(cancellationToken);
    }

    public static ReviewDto ToDto(Review review, int? currentUserId) => new()
    {
        Id = review.Id,
        ClientFullName = review.ClientProfile.User.FullName,
        ClientPhotoUrl = review.ClientProfile.User.ProfileImageUrl,
        Rating = review.Rating,
        Comment = review.Comment,
        CreatedAt = review.CreatedAt,
        IsMine = currentUserId is not null && review.ClientProfile.UserId == currentUserId
    };

    private async Task<Review> LoadOwnAsync(int clientUserId, int reviewId, CancellationToken cancellationToken) =>
        await db.Reviews.Include(x => x.ClientProfile).ThenInclude(x => x.User)
            .FirstOrDefaultAsync(x => x.Id == reviewId && x.ClientProfile.UserId == clientUserId, cancellationToken)
        ?? throw new NotFoundException("Recenzija nije pronađena.");
}

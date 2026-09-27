using GoBeyond.API.Extensions;
using GoBeyond.Core.DTOs.Communication;
using GoBeyond.Core.DTOs.Mentors;
using GoBeyond.Infrastructure.Services.Reviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers.Client;

[ApiController]
[Route("api/reviews")]
[Authorize(Policy = Policies.ClientOnly)]
public sealed class ReviewsController(IReviewService reviewService) : ControllerBase
{
    [HttpPost]
    public Task<ReviewDto> Create([FromBody] CreateReviewRequest request, CancellationToken cancellationToken) =>
        reviewService.CreateAsync(User.GetUserId(), request, cancellationToken);

    [HttpPut("{id:int}")]
    public Task<ReviewDto> Update(int id, [FromBody] UpdateReviewRequest request, CancellationToken cancellationToken) =>
        reviewService.UpdateAsync(User.GetUserId(), id, request, cancellationToken);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await reviewService.DeleteAsync(User.GetUserId(), id, cancellationToken);
        return NoContent();
    }
}

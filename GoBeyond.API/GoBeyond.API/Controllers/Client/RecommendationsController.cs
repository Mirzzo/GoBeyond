using GoBeyond.API.Extensions;
using GoBeyond.Core.DTOs.Mentors;
using GoBeyond.Infrastructure.Services.Recommendations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers.Client;

[ApiController]
[Route("api/recommendations")]
[Authorize(Policy = Policies.ClientOnly)]
public sealed class RecommendationsController(IRecommendationService recommendationService) : ControllerBase
{
    /// <summary>Personalizovane preporuke mentora (content-based filtering) sa ocjenom 0–1 i razlozima.</summary>
    [HttpGet("mentors")]
    public Task<List<MentorRecommendationDto>> GetMentors([FromQuery] int take = 5, CancellationToken cancellationToken = default) =>
        recommendationService.RecommendForClientAsync(User.GetUserId(), take, cancellationToken);
}

using GoBeyond.API.Extensions;
using GoBeyond.Core.DTOs.Mentors;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Services.Mentors;
using GoBeyond.Infrastructure.Services.Recommendations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers.Client;

/// <summary>Javni pregled mentora (anonimno dostupno); isMine u recenzijama se računa ako je korisnik prijavljen.</summary>
[ApiController]
[Route("api/mentors")]
[AllowAnonymous]
public sealed class MentorsController(IMentorCatalogService catalogService, IRecommendationService recommendationService) : ControllerBase
{
    /// <summary>?trainingTypeId=&amp;search=&amp;sortBy=rating|name|price&amp;sortDirection=asc|desc</summary>
    [HttpGet]
    public Task<List<MentorSummaryDto>> Get([FromQuery] MentorCatalogSearchObject searchObject, CancellationToken cancellationToken) =>
        catalogService.GetMentorsAsync(searchObject, cancellationToken);

    [HttpGet("{mentorProfileId:int}")]
    public Task<MentorDetailDto> GetById(int mentorProfileId, CancellationToken cancellationToken) =>
        catalogService.GetMentorAsync(mentorProfileId, User.TryGetUserId(), cancellationToken);

    [HttpGet("{mentorProfileId:int}/reviews")]
    public Task<List<ReviewDto>> GetReviews(int mentorProfileId, CancellationToken cancellationToken) =>
        catalogService.GetReviewsAsync(mentorProfileId, User.TryGetUserId(), cancellationToken);

    /// <summary>Content-based sličnost mentor↔mentor (kosinusna sličnost vektora karakteristika).</summary>
    [HttpGet("{mentorProfileId:int}/similar")]
    public Task<List<MentorSummaryDto>> GetSimilar(int mentorProfileId, [FromQuery] int take = 3, CancellationToken cancellationToken = default) =>
        recommendationService.GetSimilarMentorsAsync(mentorProfileId, take, cancellationToken);
}

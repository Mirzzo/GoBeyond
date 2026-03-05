using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers;

[ApiController]
[Route("api/reviews")]
public class ReviewsController : ControllerBase
{
    [Authorize(Policy = "ClientOnly")]
    [HttpPost]
    public IActionResult Create() => StatusCode(StatusCodes.Status501NotImplemented);

    [AllowAnonymous]
    [HttpGet("mentor/{mentorId:int}")]
    public IActionResult GetMentorReviews(int mentorId) => StatusCode(StatusCodes.Status501NotImplemented);
}

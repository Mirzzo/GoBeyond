using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers;

[ApiController]
[Route("api/mentors")]
public class MentorsController : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult GetMentors() => StatusCode(StatusCodes.Status501NotImplemented);

    [AllowAnonymous]
    [HttpGet("{id:int}")]
    public IActionResult GetMentorById(int id) => StatusCode(StatusCodes.Status501NotImplemented);

    [AllowAnonymous]
    [HttpGet("{id:int}/reviews")]
    public IActionResult GetMentorReviews(int id) => StatusCode(StatusCodes.Status501NotImplemented);

    [Authorize(Policy = "MentorOnly")]
    [HttpPut("profile")]
    public IActionResult UpdateProfile() => StatusCode(StatusCodes.Status501NotImplemented);

    [Authorize(Policy = "MentorOnly")]
    [HttpPost("certificates")]
    public IActionResult UploadCertificate() => StatusCode(StatusCodes.Status501NotImplemented);

    [Authorize(Policy = "MentorOnly")]
    [HttpGet("collaboration-requests")]
    public IActionResult GetCollaborationRequests() => StatusCode(StatusCodes.Status501NotImplemented);

    [Authorize(Policy = "MentorOnly")]
    [HttpGet("subscribers")]
    public IActionResult GetSubscribers() => StatusCode(StatusCodes.Status501NotImplemented);

    [Authorize(Policy = "MentorOnly")]
    [HttpGet("clients/{clientId:int}")]
    public IActionResult GetClientDetails(int clientId) => StatusCode(StatusCodes.Status501NotImplemented);

    [Authorize(Policy = "ClientOnly")]
    [HttpGet("recommended")]
    public IActionResult GetRecommendedMentors() => StatusCode(StatusCodes.Status501NotImplemented);
}

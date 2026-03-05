using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers;

[Authorize]
[ApiController]
[Route("api/training-plans")]
public class TrainingPlansController : ControllerBase
{
    [Authorize(Policy = "MentorOnly")]
    [HttpPost]
    public IActionResult Create() => StatusCode(StatusCodes.Status501NotImplemented);

    [Authorize(Policy = "MentorOrAdmin")]
    [HttpGet("{id:int}")]
    public IActionResult GetById(int id) => StatusCode(StatusCodes.Status501NotImplemented);

    [Authorize(Policy = "MentorOnly")]
    [HttpPut("{id:int}")]
    public IActionResult Update(int id) => StatusCode(StatusCodes.Status501NotImplemented);

    [Authorize(Policy = "MentorOnly")]
    [HttpPut("{id:int}/publish")]
    public IActionResult Publish(int id) => StatusCode(StatusCodes.Status501NotImplemented);

    [Authorize(Policy = "MentorOrAdmin")]
    [HttpGet("by-subscription/{subscriptionId:int}")]
    public IActionResult GetBySubscription(int subscriptionId) => StatusCode(StatusCodes.Status501NotImplemented);

    [Authorize(Policy = "ClientOnly")]
    [HttpGet("my-current")]
    public IActionResult GetMyCurrentPlan() => StatusCode(StatusCodes.Status501NotImplemented);
}

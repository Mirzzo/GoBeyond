using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers;

[Authorize]
[ApiController]
[Route("api/subscriptions")]
public class SubscriptionsController : ControllerBase
{
    [Authorize(Policy = "ClientOnly")]
    [HttpPost]
    public IActionResult Create() => StatusCode(StatusCodes.Status501NotImplemented);

    [Authorize(Policy = "ClientOnly")]
    [HttpGet("my")]
    public IActionResult GetMySubscriptions() => StatusCode(StatusCodes.Status501NotImplemented);

    [Authorize(Policy = "MentorOrAdmin")]
    [HttpGet("{id:int}")]
    public IActionResult GetById(int id) => StatusCode(StatusCodes.Status501NotImplemented);

    [Authorize(Policy = "ClientOnly")]
    [HttpPost("{id:int}/cancel")]
    public IActionResult Cancel(int id) => StatusCode(StatusCodes.Status501NotImplemented);
}

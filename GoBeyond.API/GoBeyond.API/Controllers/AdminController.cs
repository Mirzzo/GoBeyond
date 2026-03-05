using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers;

[Authorize(Policy = "AdminOnly")]
[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    [HttpGet("mentor-requests")]
    public IActionResult GetMentorRequests() => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPut("mentor-requests/{id:int}/approve")]
    public IActionResult ApproveMentorRequest(int id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPut("mentor-requests/{id:int}/reject")]
    public IActionResult RejectMentorRequest(int id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("mentors")]
    public IActionResult GetMentors() => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("clients")]
    public IActionResult GetClients() => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPut("users/{id:int}/block")]
    public IActionResult BlockUser(int id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpDelete("users/{id:int}")]
    public IActionResult DeleteUser(int id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("reports/mentors/{id:int}")]
    public IActionResult GetMentorReport(int id) => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("reports/overview")]
    public IActionResult GetOverviewReport() => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet("subscriptions")]
    public IActionResult GetSubscriptions() => StatusCode(StatusCodes.Status501NotImplemented);
}

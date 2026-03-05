using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers;

[Authorize]
[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    [HttpGet]
    public IActionResult GetNotifications() => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPut("{id:int}/read")]
    public IActionResult MarkAsRead(int id) => StatusCode(StatusCodes.Status501NotImplemented);
}

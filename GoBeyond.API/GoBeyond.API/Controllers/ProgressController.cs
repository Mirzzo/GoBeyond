using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers;

[Authorize(Policy = "ClientOnly")]
[ApiController]
[Route("api/progress")]
public class ProgressController : ControllerBase
{
    [HttpPost]
    public IActionResult Create() => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpGet]
    public IActionResult GetHistory() => StatusCode(StatusCodes.Status501NotImplemented);

    [HttpPost("photo")]
    public IActionResult UploadPhoto() => StatusCode(StatusCodes.Status501NotImplemented);
}

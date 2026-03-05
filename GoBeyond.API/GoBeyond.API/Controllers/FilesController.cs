using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers;

[Authorize]
[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
    [HttpPost("upload")]
    public IActionResult Upload() => StatusCode(StatusCodes.Status501NotImplemented);
}

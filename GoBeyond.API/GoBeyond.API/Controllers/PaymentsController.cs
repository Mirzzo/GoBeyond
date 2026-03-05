using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    [Authorize(Policy = "ClientOnly")]
    [HttpPost("create-intent")]
    public IActionResult CreateIntent() => StatusCode(StatusCodes.Status501NotImplemented);

    [AllowAnonymous]
    [HttpPost("webhook")]
    public IActionResult StripeWebhook() => StatusCode(StatusCodes.Status501NotImplemented);
}

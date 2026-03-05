using GoBeyond.Core.DTOs.Common;
using GoBeyond.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers.Base;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseReadOnlyController<TModel, TSearch>(
    IReadOnlyService<TModel, TSearch> service) : ControllerBase
{
    [HttpGet]
    public virtual Task<PagedResult<TModel>> Get([FromQuery] TSearch search, CancellationToken cancellationToken)
        => service.GetAsync(search, cancellationToken);

    [HttpGet("{id:int}")]
    public virtual async Task<ActionResult<TModel>> GetById(int id, CancellationToken cancellationToken)
    {
        var item = await service.GetByIdAsync(id, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        return Ok(item);
    }
}

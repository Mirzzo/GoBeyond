using GoBeyond.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers.Base;

[ApiController]
[Route("api/[controller]")]
public abstract class BaseController<TModel, TSearch, TInsert, TUpdate>(
    ICrudService<TModel, TSearch, TInsert, TUpdate> service)
    : BaseReadOnlyController<TModel, TSearch>(service)
{
    [HttpPost]
    public virtual Task<TModel> Insert([FromBody] TInsert request, CancellationToken cancellationToken)
        => service.InsertAsync(request, cancellationToken);

    [HttpPut("{id:int}")]
    public virtual async Task<ActionResult<TModel>> Update(int id, [FromBody] TUpdate request, CancellationToken cancellationToken)
    {
        var item = await service.UpdateAsync(id, request, cancellationToken);
        if (item is null)
        {
            return NotFound();
        }

        return Ok(item);
    }

    [HttpDelete("{id:int}")]
    public virtual async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await service.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }
}

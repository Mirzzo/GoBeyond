using GoBeyond.API.Extensions;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Services.Base;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers.Base;

/// <summary>Generički CRUD kontroler: dodavanje, izmjena i brisanje su dozvoljeni samo administratoru.</summary>
public abstract class BaseCRUDController<TModel, TSearch, TInsert, TUpdate>(ICRUDService<TModel, TSearch, TInsert, TUpdate> service)
    : BaseReadOnlyController<TModel, TSearch>(service)
    where TSearch : BaseSearchObject
{
    [HttpPost]
    [Authorize(Policy = Policies.AdminOnly)]
    public virtual Task<TModel> Insert([FromBody] TInsert request, CancellationToken cancellationToken) =>
        service.InsertAsync(request, cancellationToken);

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public virtual Task<TModel> Update(int id, [FromBody] TUpdate request, CancellationToken cancellationToken) =>
        service.UpdateAsync(id, request, cancellationToken);

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.AdminOnly)]
    public virtual async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}

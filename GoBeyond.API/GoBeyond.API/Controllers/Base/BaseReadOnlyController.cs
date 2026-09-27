using GoBeyond.Core.DTOs.Common;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Services.Base;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers.Base;

/// <summary>
/// Generički kontroler za čitanje (BaseCRUD obrazac s nastave): GET lista sa SearchObject-om
/// (<c>{ items, totalCount }</c>) i GET po ID-u. Čitanje šifarnika je anonimno (registracija puni dropdown-e).
/// </summary>
[ApiController]
public abstract class BaseReadOnlyController<TModel, TSearch>(IReadService<TModel, TSearch> service) : ControllerBase
    where TSearch : BaseSearchObject
{
    [HttpGet]
    [AllowAnonymous]
    public virtual Task<PagedResult<TModel>> Get([FromQuery] TSearch searchObject, CancellationToken cancellationToken) =>
        service.GetAsync(searchObject, cancellationToken);

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    public virtual Task<TModel> GetById(int id, CancellationToken cancellationToken) =>
        service.GetByIdAsync(id, cancellationToken);
}

using GoBeyond.Core.DTOs.Common;
using GoBeyond.Core.SearchObjects;

namespace GoBeyond.Infrastructure.Services.Base;

/// <summary>Generički servis za čitanje (lista sa SearchObject-om + dohvat po ID-u).</summary>
public interface IReadService<TModel, in TSearch> where TSearch : BaseSearchObject
{
    Task<PagedResult<TModel>> GetAsync(TSearch search, CancellationToken cancellationToken = default);
    Task<TModel> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>Generički CRUD servis (dodavanje, izmjena, brisanje).</summary>
public interface ICRUDService<TModel, in TSearch, in TInsert, in TUpdate> : IReadService<TModel, TSearch>
    where TSearch : BaseSearchObject
{
    Task<TModel> InsertAsync(TInsert request, CancellationToken cancellationToken = default);
    Task<TModel> UpdateAsync(int id, TUpdate request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

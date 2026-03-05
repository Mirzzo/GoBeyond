using GoBeyond.Core.DTOs.Common;

namespace GoBeyond.Infrastructure.Interfaces;

public interface IReadOnlyService<TModel, in TSearch>
{
    Task<PagedResult<TModel>> GetAsync(TSearch search, CancellationToken cancellationToken = default);
    Task<TModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}

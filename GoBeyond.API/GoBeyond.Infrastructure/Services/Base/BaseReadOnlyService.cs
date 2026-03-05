using GoBeyond.Core.DTOs.Common;
using GoBeyond.Core.Entities;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Services.Base;

public abstract class BaseReadOnlyService<TDb, TModel, TSearch>(GoBeyondDbContext dbContext)
    where TDb : BaseEntity
    where TSearch : BaseSearchObject
{
    protected GoBeyondDbContext DbContext { get; } = dbContext;

    public virtual async Task<PagedResult<TModel>> GetAsync(TSearch search, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(DbContext.Set<TDb>().AsNoTracking(), search);
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((search.Page - 1) * search.PageSize)
            .Take(search.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TModel>(
            items.Select(MapToModel).ToArray(),
            totalCount,
            search.Page,
            search.PageSize);
    }

    public virtual async Task<TModel?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<TDb>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        return entity is null ? default : MapToModel(entity);
    }

    protected virtual IQueryable<TDb> ApplyFilter(IQueryable<TDb> query, TSearch search)
    {
        return query;
    }

    protected abstract TModel MapToModel(TDb entity);
}

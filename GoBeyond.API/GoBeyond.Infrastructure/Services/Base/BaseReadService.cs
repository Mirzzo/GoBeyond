using GoBeyond.Core.DTOs.Common;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Exceptions;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Services.Base;

/// <summary>
/// Bazna implementacija čitanja: filtriranje (ApplyFilter), sortiranje (ApplyOrder),
/// paginacija (Page/PageSize iz SearchObject-a) i mapiranje entiteta u model (MapToModel).
/// </summary>
public abstract class BaseReadService<TEntity, TModel, TSearch>(GoBeyondDbContext db)
    : IReadService<TModel, TSearch>
    where TEntity : BaseEntity
    where TSearch : BaseSearchObject
{
    protected GoBeyondDbContext Db { get; } = db;

    protected virtual string NotFoundMessage => "Stavka nije pronađena.";

    public virtual async Task<PagedResult<TModel>> GetAsync(TSearch search, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter(Db.Set<TEntity>().AsNoTracking(), search);
        var totalCount = await query.CountAsync(cancellationToken);
        var entities = await ApplyOrder(query)
            .Skip((search.Page - 1) * search.PageSize)
            .Take(search.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TModel> { Items = entities.Select(MapToModel).ToList(), TotalCount = totalCount };
    }

    public virtual async Task<TModel> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await Db.Set<TEntity>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                     ?? throw new NotFoundException(NotFoundMessage);
        return MapToModel(entity);
    }

    protected virtual IQueryable<TEntity> ApplyFilter(IQueryable<TEntity> query, TSearch search) => query;

    protected virtual IQueryable<TEntity> ApplyOrder(IQueryable<TEntity> query) => query.OrderBy(x => x.Id);

    protected abstract TModel MapToModel(TEntity entity);
}

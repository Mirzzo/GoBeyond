using GoBeyond.Core.Entities;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Services.Base;

public abstract class BaseService<TDb, TModel, TInsert, TUpdate, TSearch>(GoBeyondDbContext dbContext)
    : BaseReadOnlyService<TDb, TModel, TSearch>(dbContext)
    where TDb : BaseEntity, new()
    where TSearch : BaseSearchObject
{
    public virtual async Task<TModel> InsertAsync(TInsert request, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(request);
        DbContext.Set<TDb>().Add(entity);
        await DbContext.SaveChangesAsync(cancellationToken);
        return MapToModel(entity);
    }

    public virtual async Task<TModel?> UpdateAsync(int id, TUpdate request, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<TDb>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return default;
        }

        MapToEntity(request, entity);
        await DbContext.SaveChangesAsync(cancellationToken);
        return MapToModel(entity);
    }

    public virtual async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<TDb>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        DbContext.Set<TDb>().Remove(entity);
        await DbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    protected abstract TDb MapToEntity(TInsert request);
    protected virtual void MapToEntity(TUpdate request, TDb entity)
    {
    }
}

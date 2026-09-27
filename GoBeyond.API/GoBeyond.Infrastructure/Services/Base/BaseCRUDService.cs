using GoBeyond.Core.Entities;
using GoBeyond.Core.Exceptions;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Services.Base;

/// <summary>
/// Bazna CRUD implementacija. Izvedene klase definišu mapiranje (MapInsert/MapUpdate) i
/// opcione "hook" metode (BeforeInsert/BeforeUpdate/IsInUse) za poslovne provjere.
/// Brisanje zapisa koji se koristi u drugim tabelama vraća 409.
/// </summary>
public abstract class BaseCRUDService<TEntity, TModel, TSearch, TInsert, TUpdate>(GoBeyondDbContext db)
    : BaseReadService<TEntity, TModel, TSearch>(db), ICRUDService<TModel, TSearch, TInsert, TUpdate>
    where TEntity : BaseEntity, new()
    where TSearch : BaseSearchObject
{
    public const string InUseMessage = "Stavka se ne može obrisati jer je u upotrebi.";

    public virtual async Task<TModel> InsertAsync(TInsert request, CancellationToken cancellationToken = default)
    {
        var entity = new TEntity();
        await BeforeInsertAsync(request, cancellationToken);
        MapInsert(request, entity);
        Db.Set<TEntity>().Add(entity);
        await Db.SaveChangesAsync(cancellationToken);
        return MapToModel(entity);
    }

    public virtual async Task<TModel> UpdateAsync(int id, TUpdate request, CancellationToken cancellationToken = default)
    {
        var entity = await Db.Set<TEntity>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                     ?? throw new NotFoundException(NotFoundMessage);
        await BeforeUpdateAsync(entity, request, cancellationToken);
        MapUpdate(request, entity);
        await Db.SaveChangesAsync(cancellationToken);
        return MapToModel(entity);
    }

    public virtual async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await Db.Set<TEntity>().FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                     ?? throw new NotFoundException(NotFoundMessage);
        if (await IsInUseAsync(entity, cancellationToken))
            throw new ConflictException(InUseMessage);

        Db.Set<TEntity>().Remove(entity);
        try
        {
            await Db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Rezervna zaštita: strani ključ sa DeleteBehavior.Restrict (npr. istovremeno dodana referenca).
            throw new ConflictException(InUseMessage);
        }
    }

    protected abstract void MapInsert(TInsert request, TEntity entity);

    protected abstract void MapUpdate(TUpdate request, TEntity entity);

    protected virtual Task BeforeInsertAsync(TInsert request, CancellationToken cancellationToken) => Task.CompletedTask;

    protected virtual Task BeforeUpdateAsync(TEntity entity, TUpdate request, CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>Da li se zapis koristi u drugim tabelama (tada se ne smije obrisati).</summary>
    protected virtual Task<bool> IsInUseAsync(TEntity entity, CancellationToken cancellationToken) => Task.FromResult(false);
}

using GoBeyond.Core.Entities;
using GoBeyond.Core.Exceptions;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Base;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Services.ReferenceData;

/// <summary>
/// Zajednička logika za šifarnike sa jedinstvenim nazivom: pretraga po nazivu,
/// sortiranje po nazivu i provjera duplikata prije dodavanja/izmjene.
/// </summary>
public abstract class ReferenceDataService<TEntity, TModel, TUpsert>(GoBeyondDbContext db)
    : BaseCRUDService<TEntity, TModel, ReferenceSearchObject, TUpsert, TUpsert>(db)
    where TEntity : BaseEntity, INamedEntity, new()
{
    protected abstract string GetName(TUpsert request);

    protected override IQueryable<TEntity> ApplyFilter(IQueryable<TEntity> query, ReferenceSearchObject search)
    {
        if (!string.IsNullOrWhiteSpace(search.Name))
        {
            var name = search.Name.Trim();
            query = query.Where(x => x.Name.Contains(name));
        }
        return query;
    }

    protected override IQueryable<TEntity> ApplyOrder(IQueryable<TEntity> query) => query.OrderBy(x => x.Name);

    protected override Task BeforeInsertAsync(TUpsert request, CancellationToken cancellationToken) =>
        EnsureUniqueNameAsync(GetName(request), null, cancellationToken);

    protected override Task BeforeUpdateAsync(TEntity entity, TUpsert request, CancellationToken cancellationToken) =>
        EnsureUniqueNameAsync(GetName(request), entity.Id, cancellationToken);

    private async Task EnsureUniqueNameAsync(string name, int? currentId, CancellationToken cancellationToken)
    {
        var trimmed = name.Trim();
        var exists = await Db.Set<TEntity>().AnyAsync(x => x.Name == trimmed && x.Id != currentId, cancellationToken);
        if (exists)
            throw new ValidationException("name", $"Stavka s nazivom \"{trimmed}\" već postoji.");
    }
}

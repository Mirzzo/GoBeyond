namespace GoBeyond.Infrastructure.Interfaces;

public interface ICrudService<TModel, in TSearch, in TInsert, in TUpdate>
    : IReadOnlyService<TModel, TSearch>
{
    Task<TModel> InsertAsync(TInsert request, CancellationToken cancellationToken = default);
    Task<TModel?> UpdateAsync(int id, TUpdate request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}

using GoBeyond.Core.DTOs.Reference;
using GoBeyond.Core.Entities;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Base;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Services.ReferenceData;

public interface IFitnessLevelService
    : ICRUDService<FitnessLevelDto, ReferenceSearchObject, FitnessLevelUpsertRequest, FitnessLevelUpsertRequest>;


public sealed class FitnessLevelService(GoBeyondDbContext db)
    : ReferenceDataService<FitnessLevel, FitnessLevelDto, FitnessLevelUpsertRequest>(db), IFitnessLevelService
{
    protected override string NotFoundMessage => "Nivo fizičke spreme nije pronađen.";

    protected override Task<bool> IsInUseAsync(FitnessLevel entity, CancellationToken cancellationToken) =>
        Db.ClientProfiles.AnyAsync(x => x.FitnessLevelId == entity.Id, cancellationToken);

    protected override string GetName(FitnessLevelUpsertRequest request) => request.Name;

    /// <summary>Nivoi se prikazuju po redoslijedu (početnik → napredni), ne abecedno.</summary>
    protected override IQueryable<FitnessLevel> ApplyOrder(IQueryable<FitnessLevel> query) =>
        query.OrderBy(x => x.SortOrder).ThenBy(x => x.Name);

    protected override FitnessLevelDto MapToModel(FitnessLevel entity) => new()
    {
        Id = entity.Id, Name = entity.Name, Description = entity.Description, SortOrder = entity.SortOrder
    };

    protected override void MapInsert(FitnessLevelUpsertRequest request, FitnessLevel entity) => MapUpdate(request, entity);

    protected override void MapUpdate(FitnessLevelUpsertRequest request, FitnessLevel entity)
    {
        entity.Name = request.Name.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.SortOrder = request.SortOrder;
    }
}

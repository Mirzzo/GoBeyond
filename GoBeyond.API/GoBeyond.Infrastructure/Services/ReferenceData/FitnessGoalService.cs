using GoBeyond.Core.DTOs.Reference;
using GoBeyond.Core.Entities;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Base;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Services.ReferenceData;

public interface IFitnessGoalService
    : ICRUDService<FitnessGoalDto, ReferenceSearchObject, FitnessGoalUpsertRequest, FitnessGoalUpsertRequest>;


public sealed class FitnessGoalService(GoBeyondDbContext db)
    : ReferenceDataService<FitnessGoal, FitnessGoalDto, FitnessGoalUpsertRequest>(db), IFitnessGoalService
{
    protected override string NotFoundMessage => "Fitness cilj nije pronađen.";

    protected override async Task<bool> IsInUseAsync(FitnessGoal entity, CancellationToken cancellationToken) =>
        await Db.MentorSpecializations.AnyAsync(x => x.FitnessGoalId == entity.Id, cancellationToken) ||
        await Db.ClientProfiles.AnyAsync(x => x.FitnessGoalId == entity.Id, cancellationToken);

    protected override string GetName(FitnessGoalUpsertRequest request) => request.Name;

    protected override FitnessGoalDto MapToModel(FitnessGoal entity) =>
        new() { Id = entity.Id, Name = entity.Name, Description = entity.Description };

    protected override void MapInsert(FitnessGoalUpsertRequest request, FitnessGoal entity) => MapUpdate(request, entity);

    protected override void MapUpdate(FitnessGoalUpsertRequest request, FitnessGoal entity)
    {
        entity.Name = request.Name.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
    }
}

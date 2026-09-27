using GoBeyond.Core.DTOs.Reference;
using GoBeyond.Core.Entities;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Base;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Services.ReferenceData;

public interface ITrainingTypeService
    : ICRUDService<TrainingTypeDto, ReferenceSearchObject, TrainingTypeUpsertRequest, TrainingTypeUpsertRequest>;


public sealed class TrainingTypeService(GoBeyondDbContext db)
    : ReferenceDataService<TrainingType, TrainingTypeDto, TrainingTypeUpsertRequest>(db), ITrainingTypeService
{
    protected override string NotFoundMessage => "Vrsta treninga nije pronađena.";

    protected override async Task<bool> IsInUseAsync(TrainingType entity, CancellationToken cancellationToken) =>
        await Db.MentorProfiles.AnyAsync(x => x.TrainingTypeId == entity.Id, cancellationToken) ||
        await Db.ClientProfiles.AnyAsync(x => x.PreferredTrainingTypeId == entity.Id, cancellationToken);

    protected override string GetName(TrainingTypeUpsertRequest request) => request.Name;

    protected override TrainingTypeDto MapToModel(TrainingType entity) =>
        new() { Id = entity.Id, Name = entity.Name, Description = entity.Description };

    protected override void MapInsert(TrainingTypeUpsertRequest request, TrainingType entity) => MapUpdate(request, entity);

    protected override void MapUpdate(TrainingTypeUpsertRequest request, TrainingType entity)
    {
        entity.Name = request.Name.Trim();
        entity.Description = request.Description.Trim();
    }
}

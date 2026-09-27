using GoBeyond.Core.DTOs.Reference;
using GoBeyond.Core.Entities;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Base;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Services.ReferenceData;

public interface IGenderService
    : ICRUDService<GenderDto, ReferenceSearchObject, GenderUpsertRequest, GenderUpsertRequest>;


public sealed class GenderService(GoBeyondDbContext db)
    : ReferenceDataService<Gender, GenderDto, GenderUpsertRequest>(db), IGenderService
{
    protected override string NotFoundMessage => "Spol nije pronađen.";

    protected override Task<bool> IsInUseAsync(Gender entity, CancellationToken cancellationToken) =>
        Db.Users.AnyAsync(x => x.GenderId == entity.Id, cancellationToken);

    protected override string GetName(GenderUpsertRequest request) => request.Name;

    protected override GenderDto MapToModel(Gender entity) => new() { Id = entity.Id, Name = entity.Name };

    protected override void MapInsert(GenderUpsertRequest request, Gender entity) => MapUpdate(request, entity);

    protected override void MapUpdate(GenderUpsertRequest request, Gender entity) => entity.Name = request.Name.Trim();
}

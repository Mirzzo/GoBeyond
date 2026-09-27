using GoBeyond.API.Controllers.Base;
using GoBeyond.Core.DTOs.Reference;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Services.ReferenceData;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers;

[Route("api/fitness-levels")]
public sealed class FitnessLevelsController(IFitnessLevelService service)
    : BaseCRUDController<FitnessLevelDto, ReferenceSearchObject, FitnessLevelUpsertRequest, FitnessLevelUpsertRequest>(service);

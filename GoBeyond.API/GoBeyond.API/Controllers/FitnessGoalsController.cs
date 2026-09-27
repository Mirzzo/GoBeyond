using GoBeyond.API.Controllers.Base;
using GoBeyond.Core.DTOs.Reference;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Services.ReferenceData;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers;

[Route("api/fitness-goals")]
public sealed class FitnessGoalsController(IFitnessGoalService service)
    : BaseCRUDController<FitnessGoalDto, ReferenceSearchObject, FitnessGoalUpsertRequest, FitnessGoalUpsertRequest>(service);

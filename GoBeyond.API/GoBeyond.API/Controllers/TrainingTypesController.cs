using GoBeyond.API.Controllers.Base;
using GoBeyond.Core.DTOs.Reference;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Services.ReferenceData;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers;

[Route("api/training-types")]
public sealed class TrainingTypesController(ITrainingTypeService service)
    : BaseCRUDController<TrainingTypeDto, ReferenceSearchObject, TrainingTypeUpsertRequest, TrainingTypeUpsertRequest>(service);

using GoBeyond.API.Controllers.Base;
using GoBeyond.Core.DTOs.Reference;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Services.ReferenceData;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers;

[Route("api/genders")]
public sealed class GendersController(IGenderService service)
    : BaseCRUDController<GenderDto, ReferenceSearchObject, GenderUpsertRequest, GenderUpsertRequest>(service);

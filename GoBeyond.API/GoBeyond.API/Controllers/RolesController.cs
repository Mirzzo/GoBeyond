using GoBeyond.API.Extensions;
using GoBeyond.Core.DTOs.Common;
using GoBeyond.Core.Enums;
using GoBeyond.Infrastructure.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers;

/// <summary>Uloge su sistemski enum (vezan za autorizaciju); ovdje se samo nude za dropdown.</summary>
[ApiController]
[Route("api/roles")]
[Authorize(Policy = Policies.AdminOnly)]
public sealed class RolesController : ControllerBase
{
    [HttpGet]
    public IEnumerable<RoleDto> Get() =>
        Enum.GetValues<UserRole>().Select(role => new RoleDto(role.ToString(), DomainTexts.RoleName(role)));
}

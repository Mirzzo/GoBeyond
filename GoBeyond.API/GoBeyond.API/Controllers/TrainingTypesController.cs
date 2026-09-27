using GoBeyond.Core.DTOs;
using GoBeyond.Core.Entities;
using GoBeyond.Infrastructure.Database;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.API.Controllers;

[ApiController]
[Route("api/training-types")]
public class TrainingTypesController(GoBeyondDbContext dbContext) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet]
    [HttpGet("/api/admin/training-types")]
    public async Task<IReadOnlyList<TrainingTypeDto>> Get([FromQuery] string? search, CancellationToken cancellationToken)
    {
        var query = dbContext.TrainingTypes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(x => x.Name.ToLower().Contains(term) || x.Description.ToLower().Contains(term));
        }

        return await query.OrderBy(x => x.Name)
            .Select(x => new TrainingTypeDto(x.Id, x.Name, x.Description))
            .ToListAsync(cancellationToken);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost("/api/admin/training-types")]
    public async Task<TrainingTypeDto> Create([FromBody] UpsertTrainingTypeRequestDto request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        await EnsureNameAvailableAsync(name, null, cancellationToken);
        var type = new TrainingType { Name = name, Description = request.Description.Trim() };
        dbContext.TrainingTypes.Add(type);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new TrainingTypeDto(type.Id, type.Name, type.Description);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPut("/api/admin/training-types/{id:int}")]
    public async Task<TrainingTypeDto> Update(int id, [FromBody] UpsertTrainingTypeRequestDto request, CancellationToken cancellationToken)
    {
        var type = await dbContext.TrainingTypes.FindAsync([id], cancellationToken)
            ?? throw new InvalidOperationException("Training type not found.");
        var name = request.Name.Trim();
        await EnsureNameAvailableAsync(name, id, cancellationToken);
        type.Name = name;
        type.Description = request.Description.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        return new TrainingTypeDto(type.Id, type.Name, type.Description);
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("/api/admin/training-types/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var type = await dbContext.TrainingTypes.FindAsync([id], cancellationToken)
            ?? throw new InvalidOperationException("Training type not found.");
        if (await dbContext.MentorProfiles.AnyAsync(x => x.TrainingTypeId == id, cancellationToken))
            throw new InvalidOperationException("Training type is assigned to mentors and cannot be deleted.");
        dbContext.TrainingTypes.Remove(type);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private async Task EnsureNameAvailableAsync(string name, int? exceptId, CancellationToken cancellationToken)
    {
        if (await dbContext.TrainingTypes.AnyAsync(x => x.Id != exceptId && x.Name.ToLower() == name.ToLower(), cancellationToken))
            throw new InvalidOperationException("Training type name already exists.");
    }
}

using GoBeyond.Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers;

[Authorize]
[ApiController]
[Route("api/files")]
public class FilesController(IWebHostEnvironment environment) : ControllerBase
{
    [HttpPost("upload")]
    [RequestSizeLimit(10_000_000)]
    public async Task<UploadedFileDto> Upload([FromForm] IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            throw new InvalidOperationException("Uploaded file is empty.");
        }

        var uploadsPath = Path.Combine(environment.ContentRootPath, "wwwroot", "uploads");
        Directory.CreateDirectory(uploadsPath);

        var safeFileName = $"{Guid.NewGuid():N}_{Path.GetFileName(file.FileName)}";
        var storedPath = Path.Combine(uploadsPath, safeFileName);

        await using var stream = System.IO.File.Create(storedPath);
        await file.CopyToAsync(stream, cancellationToken);

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        return new UploadedFileDto(file.FileName, $"{baseUrl}/uploads/{safeFileName}");
    }
}

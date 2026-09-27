using GoBeyond.Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers;

[Authorize]
[ApiController]
[Route("api/files")]
public class FilesController(IWebHostEnvironment environment) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("mentor-certificate")]
    [RequestSizeLimit(10_000_000)]
    [Consumes("multipart/form-data")]
    public Task<UploadedFileDto> UploadMentorCertificate(IFormFile file, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (file.Length == 0 || file.Length > 10_000_000 ||
            !new[] { ".pdf", ".jpg", ".jpeg", ".png" }.Contains(extension) ||
            !new[] { "application/pdf", "image/jpeg", "image/png" }.Contains(file.ContentType.ToLowerInvariant()))
            throw new InvalidOperationException("Certificate must be a PDF, JPG or PNG file up to 10 MB.");

        return SaveFileAsync(file, "certificates", cancellationToken);
    }

    [HttpPost("upload")]
    [RequestSizeLimit(10_000_000)]
    [Consumes("multipart/form-data")]
    public async Task<UploadedFileDto> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            throw new InvalidOperationException("Uploaded file is empty.");
        }

        return await SaveFileAsync(file, "uploads", cancellationToken);
    }

    private async Task<UploadedFileDto> SaveFileAsync(IFormFile file, string folder, CancellationToken cancellationToken)
    {
        var uploadsPath = Path.Combine(environment.ContentRootPath, "wwwroot", folder);
        Directory.CreateDirectory(uploadsPath);

        var safeFileName = $"{Guid.NewGuid():N}_{Path.GetFileName(file.FileName)}";
        var storedPath = Path.Combine(uploadsPath, safeFileName);

        await using var stream = System.IO.File.Create(storedPath);
        await file.CopyToAsync(stream, cancellationToken);

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        return new UploadedFileDto(file.FileName, $"{baseUrl}/{folder}/{safeFileName}");
    }
}

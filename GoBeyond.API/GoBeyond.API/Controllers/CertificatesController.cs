using System.Text;
using GoBeyond.API.Extensions;
using GoBeyond.Infrastructure.Services.Mentors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GoBeyond.API.Controllers;

/// <summary>Sigurno preuzimanje certifikata mentora (fajlovi nisu javno dostupni kao statički fajlovi).</summary>
[ApiController]
[Route("api/certificates")]
[Authorize(Policy = Policies.MentorOrAdmin)]
[ForbiddenMessage(CertificateFileService.Forbidden)] // klijent: ista poruka kao za tuđi certifikat (ugovor §5)
public sealed class CertificatesController(ICertificateFileService certificateFiles) : ControllerBase
{
    /// <summary>Administrator ili mentor vlasnik; ostali 403, nepostojeći certifikat 404.</summary>
    [HttpGet("{id:int}/file")]
    public async Task<IActionResult> GetFile(int id, CancellationToken cancellationToken)
    {
        var file = await certificateFiles.GetAsync(id, User.GetUserId(), User.GetRole(), cancellationToken);
        Response.Headers.ContentDisposition = InlineDisposition(file.FileName);
        return PhysicalFile(file.PhysicalPath, file.ContentType);
    }

    /// <summary>inline; filename="ascii-naziv"; filename*=UTF-8''originalni-naziv (dijakritici se ne gube).</summary>
    private static string InlineDisposition(string fileName)
    {
        var ascii = new StringBuilder(fileName.Length);
        foreach (var c in fileName)
            ascii.Append(c is >= ' ' and <= '~' && c is not ('"' or '\\') ? c : '_');
        return $"inline; filename=\"{ascii}\"; filename*=UTF-8''{Uri.EscapeDataString(fileName)}";
    }
}

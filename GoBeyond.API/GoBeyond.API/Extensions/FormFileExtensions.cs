using GoBeyond.Core.Files;

namespace GoBeyond.API.Extensions;

public static class FormFileExtensions
{
    /// <summary>IFormFile (ASP.NET) → FileUpload (Core), da servisi ne zavise od web sloja.</summary>
    public static FileUpload ToFileUpload(this IFormFile file) =>
        new(file.FileName, file.ContentType, file.Length, file.OpenReadStream);

    public static IReadOnlyList<FileUpload> ToFileUploads(this IEnumerable<IFormFile>? files) =>
        files?.Select(ToFileUpload).ToList() ?? [];
}

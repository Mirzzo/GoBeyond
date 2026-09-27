using GoBeyond.Core.Exceptions;
using GoBeyond.Core.Files;
using GoBeyond.Infrastructure.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GoBeyond.Infrastructure.Services.Files;

public enum UploadKind
{
    /// <summary>jpg / jpeg / png</summary>
    Image,

    /// <summary>pdf / jpg / jpeg / png</summary>
    Certificate
}

public interface IFileStorageService
{
    /// <summary>Validira (ekstenzija, veličina, stvarni sadržaj) i snima fajl u wwwroot/uploads/{category}/{guid}.{ext}. Vraća relativni URL.</summary>
    Task<string> SaveAsync(FileUpload file, string category, UploadKind kind, string fieldName, CancellationToken cancellationToken = default);

    /// <summary>Validacija bez snimanja (npr. prije kreiranja korisnika).</summary>
    void Validate(FileUpload file, UploadKind kind, string fieldName);

    /// <summary>Briše ranije uploadovani fajl (seed fajlovi se nikad ne brišu).</summary>
    void Delete(string? relativeUrl);
}

public sealed class FileStorageService(IHostEnvironment environment, IOptions<UploadOptions> options) : IFileStorageService
{
    private const string UploadsFolder = "uploads";

    private string WebRoot => Path.Combine(environment.ContentRootPath, "wwwroot");

    public void Validate(FileUpload file, UploadKind kind, string fieldName)
    {
        var settings = options.Value;
        var allowed = kind == UploadKind.Image ? settings.ImageExtensions : settings.CertificateExtensions;
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedText = string.Join(", ", allowed);
        var maxMb = settings.MaxFileSizeBytes / (1024 * 1024);

        if (file.Length <= 0)
            throw new ValidationException(fieldName, $"Fajl \"{file.FileName}\" je prazan.");
        if (file.Length > settings.MaxFileSizeBytes)
            throw new ValidationException(fieldName, $"Fajl \"{file.FileName}\" je prevelik. Najveća dozvoljena veličina je {maxMb} MB.");
        if (!allowed.Contains(extension))
            throw new ValidationException(fieldName, $"Format fajla \"{file.FileName}\" nije dozvoljen. Dozvoljeni formati: {allowedText}.");

        using var stream = file.OpenReadStream();
        if (!HasValidSignature(stream, extension))
            throw new ValidationException(fieldName, $"Sadržaj fajla \"{file.FileName}\" ne odgovara formatu {extension}. Dozvoljeni formati: {allowedText}.");
    }

    public async Task<string> SaveAsync(FileUpload file, string category, UploadKind kind, string fieldName,
        CancellationToken cancellationToken = default)
    {
        Validate(file, kind, fieldName);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var folder = Path.Combine(WebRoot, UploadsFolder, category);
        Directory.CreateDirectory(folder);
        var fileName = $"{Guid.NewGuid():N}{extension}";

        await using (var target = File.Create(Path.Combine(folder, fileName)))
        await using (var source = file.OpenReadStream())
        {
            await source.CopyToAsync(target, cancellationToken);
        }

        return $"/{UploadsFolder}/{category}/{fileName}";
    }

    public void Delete(string? relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl) || !relativeUrl.StartsWith($"/{UploadsFolder}/", StringComparison.Ordinal))
            return;

        var root = Path.GetFullPath(Path.Combine(WebRoot, UploadsFolder));
        var path = Path.GetFullPath(Path.Combine(WebRoot, relativeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(path))
            File.Delete(path);
    }

    /// <summary>Provjera "magic bytes" potpisa da ekstenzija odgovara stvarnom sadržaju.</summary>
    private static bool HasValidSignature(Stream stream, string extension)
    {
        Span<byte> header = stackalloc byte[8];
        var read = stream.Read(header);
        if (read < 4) return false;

        return extension switch
        {
            ".png" => read >= 8 && header[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".jpg" or ".jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".pdf" => header[..4].SequenceEqual("%PDF"u8),
            _ => false
        };
    }
}

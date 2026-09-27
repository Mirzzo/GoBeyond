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
    /// <summary>Javni fajl (wwwroot/uploads/{category}/{guid}.ext, dostupan po nepogodivom GUID URL-u). Vraća "/uploads/...".</summary>
    Task<string> SavePublicAsync(FileUpload file, string category, UploadKind kind, string fieldName, CancellationToken cancellationToken = default);

    /// <summary>Privatni fajl (Uploads:PrivateRoot, izvan wwwroot). Vraća internu lokaciju "private://...".</summary>
    Task<string> SavePrivateAsync(FileUpload file, string category, UploadKind kind, string fieldName, CancellationToken cancellationToken = default);

    /// <summary>Validacija (ekstenzija, veličina, stvarni sadržaj) bez snimanja.</summary>
    void Validate(FileUpload file, UploadKind kind, string fieldName);

    /// <summary>Fizička putanja privatnog fajla za zadanu lokaciju, ili null ako fajl ne postoji / lokacija nije dozvoljena.</summary>
    string? ResolvePrivatePath(string location);

    /// <summary>Briše uploadovani fajl (javni ili privatni); seed fajlovi se nikad ne brišu.</summary>
    void Delete(string? location);
}

public sealed class FileStorageService(IHostEnvironment environment, IOptions<UploadOptions> options) : IFileStorageService
{
    private const string UploadsFolder = "uploads";

    private string WebRoot => Path.GetFullPath(Path.Combine(environment.ContentRootPath, "wwwroot"));
    private string PublicUploadsRoot => Path.Combine(WebRoot, UploadsFolder);
    private string PrivateRoot => Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.Value.PrivateRoot));
    private string SeedRoot => Path.GetFullPath(Path.Combine(environment.ContentRootPath, options.Value.SeedFilesRoot));

    public void Validate(FileUpload file, UploadKind kind, string fieldName)
    {
        var settings = options.Value;
        var allowed = kind == UploadKind.Image ? settings.ImageExtensions : settings.CertificateExtensions;
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedText = string.Join(", ", allowed);

        if (file.Length <= 0)
            throw new ValidationException(fieldName, $"Fajl \"{file.FileName}\" je prazan.");
        if (file.Length > settings.MaxFileSizeBytes)
            throw new ValidationException(fieldName, $"Fajl \"{file.FileName}\" je prevelik. Najveća dozvoljena veličina je {settings.MaxFileSizeText}.");
        if (!allowed.Contains(extension))
            throw new ValidationException(fieldName, $"Format fajla \"{file.FileName}\" nije dozvoljen. Dozvoljeni formati: {allowedText}.");

        using var stream = file.OpenReadStream();
        if (!HasValidSignature(stream, extension))
            throw new ValidationException(fieldName, $"Sadržaj fajla \"{file.FileName}\" ne odgovara formatu {extension}. Dozvoljeni formati: {allowedText}.");
    }

    public async Task<string> SavePublicAsync(FileUpload file, string category, UploadKind kind, string fieldName,
        CancellationToken cancellationToken = default)
    {
        var relative = await SaveAsync(file, PublicUploadsRoot, category, kind, fieldName, cancellationToken);
        return FileLocations.PublicPrefix + relative;
    }

    public async Task<string> SavePrivateAsync(FileUpload file, string category, UploadKind kind, string fieldName,
        CancellationToken cancellationToken = default)
    {
        var relative = await SaveAsync(file, PrivateRoot, category, kind, fieldName, cancellationToken);
        return FileLocations.PrivateScheme + relative;
    }

    public string? ResolvePrivatePath(string location)
    {
        foreach (var candidate in PrivateCandidates(location))
            if (candidate is not null && File.Exists(candidate)) return candidate;
        return null;
    }

    public void Delete(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)) return;

        string? path = null;
        if (location.StartsWith(FileLocations.PrivateScheme, StringComparison.Ordinal))
            path = SafeCombine(PrivateRoot, location[FileLocations.PrivateScheme.Length..]);
        else if (location.StartsWith(FileLocations.PublicPrefix, StringComparison.Ordinal))
            path = SafeCombine(PublicUploadsRoot, location[FileLocations.PublicPrefix.Length..]);

        if (path is not null && File.Exists(path)) File.Delete(path);
    }

    /// <summary>
    /// Mogući fizički fajlovi za lokaciju certifikata. Stari javni formati ("/uploads/certificates/...",
    /// "/seed/certificates/...") iz baze prije prelaska na privatno skladište i dalje rade.
    /// </summary>
    private IEnumerable<string?> PrivateCandidates(string location)
    {
        if (location.StartsWith(FileLocations.PrivateScheme, StringComparison.Ordinal))
        {
            yield return SafeCombine(PrivateRoot, location[FileLocations.PrivateScheme.Length..]);
        }
        else if (location.StartsWith(FileLocations.SeedScheme, StringComparison.Ordinal))
        {
            yield return SafeCombine(SeedRoot, location[FileLocations.SeedScheme.Length..]);
        }
        else if (location.StartsWith(FileLocations.PublicPrefix, StringComparison.Ordinal))
        {
            var relative = location[FileLocations.PublicPrefix.Length..];
            yield return SafeCombine(PrivateRoot, relative);
            yield return SafeCombine(PublicUploadsRoot, relative);
        }
        else if (location.StartsWith(FileLocations.LegacySeedPrefix, StringComparison.Ordinal))
        {
            yield return SafeCombine(SeedRoot, location[FileLocations.LegacySeedPrefix.Length..]);
        }
    }

    private async Task<string> SaveAsync(FileUpload file, string root, string category, UploadKind kind, string fieldName,
        CancellationToken cancellationToken)
    {
        Validate(file, kind, fieldName);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{Guid.NewGuid():N}{extension}";
        Directory.CreateDirectory(Path.Combine(root, category));

        await using (var target = File.Create(Path.Combine(root, category, fileName)))
        await using (var source = file.OpenReadStream())
        {
            await source.CopyToAsync(target, cancellationToken);
        }

        return $"{category}/{fileName}";
    }

    /// <summary>Spaja korijen i relativnu putanju; odbija putanje koje izlaze iz korijena (npr. "../").</summary>
    private static string? SafeCombine(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative)) return null;
        var full = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        return full.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) ? full : null;
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

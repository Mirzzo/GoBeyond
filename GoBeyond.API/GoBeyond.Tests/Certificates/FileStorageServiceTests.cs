using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Infrastructure.Services.Files;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace GoBeyond.Tests.Certificates;

/// <summary>Razrješavanje lokacija privatnih fajlova (novi i stari formati, zaštita od "../").</summary>
public sealed class FileStorageServiceTests : IDisposable
{
    private readonly string _contentRoot = Path.Combine(Path.GetTempPath(), "gobeyond-fs-" + Guid.NewGuid().ToString("N"));
    private readonly FileStorageService _storage;

    public FileStorageServiceTests()
    {
        Create("private-files/certificates/a.pdf");
        Create("SeedFiles/certificates/seed.pdf");
        Create("wwwroot/uploads/certificates/legacy.pdf");
        Create("appsettings.secret.json");
        _storage = new FileStorageService(new TestEnvironment(_contentRoot),
            Options.Create(new UploadOptions { PrivateRoot = "private-files", SeedFilesRoot = "SeedFiles" }));
    }

    public void Dispose() => Directory.Delete(_contentRoot, recursive: true);

    [Theory]
    [InlineData("private://certificates/a.pdf", "private-files/certificates/a.pdf")]
    [InlineData("seed://certificates/seed.pdf", "SeedFiles/certificates/seed.pdf")]
    [InlineData("/seed/certificates/seed.pdf", "SeedFiles/certificates/seed.pdf")]              // stari seed format
    [InlineData("/uploads/certificates/legacy.pdf", "wwwroot/uploads/certificates/legacy.pdf")] // stari upload (fallback)
    public void ResolvePrivatePath_FindsFileForEveryLocationFormat(string location, string expected)
    {
        Assert.Equal(Path.GetFullPath(Path.Combine(_contentRoot, expected)), _storage.ResolvePrivatePath(location));
    }

    [Theory]
    [InlineData("private://../appsettings.secret.json")]
    [InlineData("seed://../../appsettings.secret.json")]
    [InlineData("private://certificates/ne-postoji.pdf")]
    [InlineData("C:/Windows/win.ini")]
    [InlineData("")]
    public void ResolvePrivatePath_RejectsTraversalUnknownAndMissingFiles(string location)
    {
        Assert.Null(_storage.ResolvePrivatePath(location));
    }

    [Fact]
    public void Delete_NeverDeletesSeedFiles()
    {
        _storage.Delete("seed://certificates/seed.pdf");
        Assert.True(File.Exists(Path.Combine(_contentRoot, "SeedFiles/certificates/seed.pdf")));

        _storage.Delete("private://certificates/a.pdf");
        Assert.False(File.Exists(Path.Combine(_contentRoot, "private-files/certificates/a.pdf")));
    }

    private void Create(string relative)
    {
        var path = Path.Combine(_contentRoot, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
    }

    private sealed class TestEnvironment(string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "GoBeyond.Tests";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

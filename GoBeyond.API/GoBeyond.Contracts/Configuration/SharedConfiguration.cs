using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.FileProviders;

namespace GoBeyond.Contracts.Configuration;

/// <summary>
/// Jedno mjesto za konfiguraciju: appsettings.Shared.json (korijen repozitorija, kopira se uz oba servisa).
/// Fajl se dodaje kao PRVI (najslabiji) izvor, pa ga environment varijable (npr. iz .env / docker-compose)
/// i argumenti komandne linije mogu pregaziti, npr. Payments__SecretKey ili ConnectionStrings__MainDb.
/// </summary>
public static class SharedConfiguration
{
    public const string FileName = "appsettings.Shared.json";

    public static IConfigurationBuilder AddSharedConfiguration(this IConfigurationBuilder builder)
    {
        builder.Sources.Insert(0, new JsonConfigurationSource
        {
            Path = FileName,
            Optional = false,
            ReloadOnChange = false,
            FileProvider = new PhysicalFileProvider(AppContext.BaseDirectory)
        });
        return builder;
    }
}

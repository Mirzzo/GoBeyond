using System.Text;
using System.Text.RegularExpressions;

namespace GoBeyond.Contracts.Configuration;

/// <summary>Rezultat učitavanja .env fajla: putanja (null ako fajl nije pronađen), broj postavljenih i preskočenih varijabli.</summary>
public sealed record DotEnvLoadResult(string? Path, int Loaded, int Skipped);

/// <summary>
/// Lokalno pokretanje (dotnet run, Visual Studio, VS Code) čita isti root <c>.env</c> kao docker-compose, pa je
/// konfiguracija i dalje na jednom mjestu (appsettings.Shared.json + .env). Poziva se na samom početku Program.cs,
/// prije nego konfiguracija pročita environment varijable. Vrijednosti iz .env postaju environment varijable
/// procesa, ali NIKAD ne pregaze varijablu koja je već postavljena (docker-compose environment/env_file,
/// launchSettings.json, sistemske varijable imaju prednost).
/// Format (podskup docker-compose .env pravila): <c>KLJUC=vrijednost</c> po liniji; prazne linije i linije koje
/// počinju sa <c>#</c> se preskaču; opcioni prefiks <c>export </c>; dijeli se na prvom <c>=</c> (ostali <c>=</c> su
/// dio vrijednosti); vrijednost u "..." podržava \n, \t, \r, \" i \\; vrijednost u '...' je doslovna; kod vrijednosti
/// bez navodnika komentar počinje sa " #"; prazna vrijednost se ignoriše; kod ponovljenog ključa važi zadnja linija.
/// </summary>
public static partial class DotEnvFile
{
    public const string FileName = ".env";

    /// <summary>Ako je postavljena na "1"/"true", .env se ne učitava (npr. integracijski testovi ne smiju vidjeti lokalne Stripe ključeve).</summary>
    public const string SkipVariable = "GOBEYOND_SKIP_DOTENV";

    /// <summary>
    /// Traži .env u trenutnom direktoriju pa u folderu aplikacije i njihovim roditeljima (do korijena repozitorija,
    /// tj. foldera sa .git) i učitava ga u environment varijable procesa koje još nisu postavljene.
    /// </summary>
    public static DotEnvLoadResult Load()
    {
        if (IsSkipped()) return new DotEnvLoadResult(null, 0, 0);
        var path = Find([Environment.CurrentDirectory, AppContext.BaseDirectory]);
        return path is null ? new DotEnvLoadResult(null, 0, 0) : Apply(path);
    }

    /// <summary>Upisuje vrijednosti iz zadanog .env fajla u environment varijable procesa koje još nisu postavljene.</summary>
    public static DotEnvLoadResult Apply(string path)
    {
        int loaded = 0, skipped = 0;
        foreach (var (key, value) in Parse(File.ReadAllText(path, Encoding.UTF8)))
        {
            if (Environment.GetEnvironmentVariable(key) is not null)
            {
                skipped++;
                continue;
            }
            Environment.SetEnvironmentVariable(key, value);
            loaded++;
        }
        return new DotEnvLoadResult(path, loaded, skipped);
    }

    /// <summary>Prvi .env na putu od početnog direktorija prema gore; pretraga staje na korijenu repozitorija (folder sa .git).</summary>
    public static string? Find(IEnumerable<string> startDirectories)
    {
        foreach (var start in startDirectories.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, FileName);
                if (File.Exists(candidate)) return candidate;

                var git = Path.Combine(directory.FullName, ".git");
                if (Directory.Exists(git) || File.Exists(git)) break;
            }
        }
        return null;
    }

    /// <summary>Parsira sadržaj .env fajla u parove ključ/vrijednost (zadnja vrijednost ponovljenog ključa pobjeđuje).</summary>
    public static IReadOnlyDictionary<string, string> Parse(string content)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim().TrimStart('﻿');
            if (line.Length == 0 || line[0] == '#') continue;
            if (line.StartsWith("export ", StringComparison.Ordinal)) line = line["export ".Length..].TrimStart();

            var separator = line.IndexOf('=');
            if (separator <= 0) continue;

            var key = line[..separator].Trim();
            if (!KeyPattern().IsMatch(key)) continue;

            var value = ParseValue(line[(separator + 1)..].Trim());
            if (value.Length == 0)
            {
                // Prazna vrijednost (npr. "Payments__WebhookSecret=") znači "nije postavljeno".
                values.Remove(key);
                continue;
            }
            values[key] = value;
        }
        return values;
    }

    private static string ParseValue(string value)
    {
        if (value.Length >= 2 && value[0] is '"' or '\'')
        {
            var quote = value[0];
            var end = quote == '"' ? FindClosingDoubleQuote(value) : value.IndexOf('\'', 1);
            if (end > 0)
            {
                var inner = value[1..end];
                return quote == '"' ? Unescape(inner) : inner;
            }
        }

        var comment = value.IndexOf(" #", StringComparison.Ordinal);
        return (comment >= 0 ? value[..comment] : value).Trim();
    }

    private static int FindClosingDoubleQuote(string value)
    {
        for (var i = 1; i < value.Length; i++)
        {
            if (value[i] == '\\') i++;
            else if (value[i] == '"') return i;
        }
        return -1;
    }

    private static string Unescape(string value)
    {
        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '\\' || i + 1 == value.Length)
            {
                builder.Append(value[i]);
                continue;
            }

            var next = value[++i];
            switch (next)
            {
                case 'n': builder.Append('\n'); break;
                case 't': builder.Append('\t'); break;
                case 'r': builder.Append('\r'); break;
                case '"' or '\\': builder.Append(next); break;
                default: builder.Append('\\').Append(next); break; // nepoznat escape ostaje doslovan
            }
        }
        return builder.ToString();
    }

    private static bool IsSkipped() =>
        Environment.GetEnvironmentVariable(SkipVariable) is { } skip &&
        (skip == "1" || skip.Equals("true", StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_.]*$")]
    private static partial Regex KeyPattern();
}

using GoBeyond.Contracts.Configuration;
using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Tests.TestInfrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GoBeyond.Tests.Configuration;

/// <summary>Lokalno pokretanje čita root .env (isti fajl kao docker-compose) bez pregaženja postojećih varijabli.</summary>
public sealed class DotEnvFileTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "gobeyond-dotenv-" + Guid.NewGuid().ToString("N"));
    private readonly string _prefix = "GOBEYOND_TEST_" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant() + "_";
    private readonly List<string> _variables = [];

    public DotEnvFileTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        foreach (var variable in _variables) Environment.SetEnvironmentVariable(variable, null);
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Parse_SkipsCommentsBlankLinesAndInvalidLines()
    {
        var values = DotEnvFile.Parse("""
            # komentar
               # uvučeni komentar

            Payments__SecretKey=sk_test_abc
            bez_znaka_jednakosti
            =bez_kljuca
            1NEISPRAVAN=x
            """);

        Assert.Equal(new Dictionary<string, string> { ["Payments__SecretKey"] = "sk_test_abc" }, values);
    }

    [Fact]
    public void Parse_KeepsEqualsSignsInsideValuesAndTrimsWhitespace()
    {
        var values = DotEnvFile.Parse("  ConnectionStrings__MainDb = Server=sqlserver,1433;Database=210020;Password=a=b  \r\n");

        Assert.Equal("Server=sqlserver,1433;Database=210020;Password=a=b", values["ConnectionStrings__MainDb"]);
    }

    [Fact]
    public void Parse_HandlesQuotesExportAndInlineComments()
    {
        var values = DotEnvFile.Parse("""
            DOUBLE="vrijednost sa # i = znakovima" # komentar
            SINGLE='doslovno \n bez escape-a'
            ESCAPED="red1\nred2 \"navodnici\" \\ kraj"
            export EXPORTED=da
            UNQUOTED=abc#123 # komentar iza razmaka
            UNKNOWN_ESCAPE="C:\data"
            KNOWN_ESCAPE="a\tb"
            """);

        Assert.Equal("vrijednost sa # i = znakovima", values["DOUBLE"]);
        Assert.Equal(@"doslovno \n bez escape-a", values["SINGLE"]);
        Assert.Equal("red1\nred2 \"navodnici\" \\ kraj", values["ESCAPED"]);
        Assert.Equal("da", values["EXPORTED"]);
        Assert.Equal("abc#123", values["UNQUOTED"]);
        Assert.Equal(@"C:\data", values["UNKNOWN_ESCAPE"]); // nepoznat escape ostaje doslovan
        Assert.Equal("a\tb", values["KNOWN_ESCAPE"]);
    }

    [Fact]
    public void Parse_EmptyValueMeansNotSetAndLastDuplicateWins()
    {
        var values = DotEnvFile.Parse("""
            Payments__WebhookSecret=
            EMPTY_QUOTED=""
            DUP=prvi
            DUP=drugi
            """);

        Assert.False(values.ContainsKey("Payments__WebhookSecret"));
        Assert.False(values.ContainsKey("EMPTY_QUOTED"));
        Assert.Equal("drugi", values["DUP"]);
    }

    [Fact]
    public void Parse_IgnoresUtf8ByteOrderMark()
    {
        var values = DotEnvFile.Parse("\uFEFFFIRST=1\nSECOND=2");

        Assert.Equal("1", values["FIRST"]);
        Assert.Equal("2", values["SECOND"]);
    }

    [Fact]
    public void Apply_SetsMissingVariablesButNeverOverridesExistingOnes()
    {
        var existing = Var("EXISTING");
        var fresh = Var("FRESH");
        Environment.SetEnvironmentVariable(existing, "iz-dockera");
        var path = WriteEnv(_root, $"{existing}=iz-env-fajla\n{fresh}=novo\n");

        var result = DotEnvFile.Apply(path);

        Assert.Equal("iz-dockera", Environment.GetEnvironmentVariable(existing));
        Assert.Equal("novo", Environment.GetEnvironmentVariable(fresh));
        Assert.Equal((path, 1, 1), (result.Path, result.Loaded, result.Skipped));
    }

    [Fact]
    public void Find_WalksUpFromProjectOutputToRepositoryRoot()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
        var path = WriteEnv(_root, "A=1");
        var output = Directory.CreateDirectory(Path.Combine(_root, "GoBeyond.API", "GoBeyond.API", "bin", "Debug", "net9.0")).FullName;

        Assert.Equal(path, DotEnvFile.Find([output]));
    }

    [Fact]
    public void Find_StopsAtRepositoryRootAndTriesNextStartDirectory()
    {
        // .env iznad korijena repozitorija (folder sa .git) se ne koristi.
        WriteEnv(_root, "A=1");
        var repository = Directory.CreateDirectory(Path.Combine(_root, "repo")).FullName;
        Directory.CreateDirectory(Path.Combine(repository, ".git"));
        var project = Directory.CreateDirectory(Path.Combine(repository, "src")).FullName;
        var other = Directory.CreateDirectory(Path.Combine(_root, "other")).FullName;
        var otherEnv = WriteEnv(other, "B=2");

        Assert.Null(DotEnvFile.Find([project]));
        Assert.Equal(otherEnv, DotEnvFile.Find([project, other]));
    }

    [Fact]
    public void ApiTestHost_DoesNotLoadTheDevelopersDotEnv()
    {
        // Root .env može sadržavati prave Stripe TEST ključeve; integracijski testovi ih ne smiju koristiti.
        using var factory = new GoBeyondApiFactory();

        var payments = factory.Services.GetRequiredService<IOptions<PaymentOptions>>().Value;

        Assert.Equal("1", Environment.GetEnvironmentVariable(DotEnvFile.SkipVariable));
        Assert.False(payments.IsConfigured);
    }

    private string Var(string name)
    {
        var variable = _prefix + name;
        _variables.Add(variable);
        return variable;
    }

    private static string WriteEnv(string directory, string content)
    {
        var path = Path.Combine(directory, DotEnvFile.FileName);
        File.WriteAllText(path, content);
        return path;
    }
}

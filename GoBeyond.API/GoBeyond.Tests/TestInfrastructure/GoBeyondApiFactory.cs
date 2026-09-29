using System.Net.Http.Headers;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Infrastructure.BackgroundServices;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GoBeyond.Tests.TestInfrastructure;

/// <summary>
/// Pravi API (Program, middleware, autentifikacija, kontroleri) u memoriji: SQLite umjesto SQL Servera,
/// test podaci umjesto seed-a, bez RabbitMQ/lifecycle pozadinskih servisa i sa privremenim privatnim folderom.
/// </summary>
public sealed class GoBeyondApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Program.cs učitava root .env (lokalni Stripe ključevi i sl.). Testovi ga ne smiju vidjeti, pa se to isključuje
    /// prije prvog pokretanja Program-a (statički konstruktor se izvršava prije kreiranja bilo koje instance).
    /// </summary>
    static GoBeyondApiFactory() => Environment.SetEnvironmentVariable(GoBeyond.Contracts.Configuration.DotEnvFile.SkipVariable, "1");

    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public string PrivateRoot { get; } = Path.Combine(Path.GetTempPath(), "gobeyond-tests-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?> { ["Uploads:PrivateRoot"] = PrivateRoot }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<GoBeyondDbContext>>();
            services.RemoveAll<GoBeyondDbContext>();
            services.AddScoped(_ => SqliteTestDbContext.Create(_connection));

            services.RemoveAll<IDatabaseInitializer>();
            services.AddScoped<IDatabaseInitializer, TestDataInitializer>();

            foreach (var descriptor in services.Where(x =>
                         x.ImplementationType == typeof(OutboxDispatcher) ||
                         x.ImplementationType == typeof(SubscriptionLifecycleService)).ToList())
                services.Remove(descriptor);
        });
    }

    /// <summary>HttpClient prijavljen kao zadani korisnik (JWT izdat istim servisom kao pri loginu).</summary>
    public HttpClient ClientFor(string? username)
    {
        var client = CreateClient();
        if (username is null) return client;

        using var scope = Services.CreateScope();
        var user = scope.ServiceProvider.GetRequiredService<GoBeyondDbContext>().Users.AsNoTracking().Single(x => x.Username == username);
        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>().CreateAccessToken(user, Guid.NewGuid()).Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public T Query<T>(Func<GoBeyondDbContext, T> query)
    {
        using var scope = Services.CreateScope();
        return query(scope.ServiceProvider.GetRequiredService<GoBeyondDbContext>());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing) return;
        _connection.Dispose();
        if (Directory.Exists(PrivateRoot)) Directory.Delete(PrivateRoot, recursive: true);
    }

    /// <summary>Minimalni podaci: admin, dva mentora, klijent i certifikati (seed, legacy i nepostojeći fajl).</summary>
    private sealed class TestDataInitializer(GoBeyondDbContext db) : IDatabaseInitializer
    {
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
            if (await db.Users.AnyAsync(cancellationToken)) return;

            var gender = new Gender { Name = "Muško" };
            var type = new TrainingType { Name = "Weightlifting", Description = "Utezi" };
            var level = new FitnessLevel { Name = "Početnik", SortOrder = 1 };
            var goal = new FitnessGoal { Name = "Snaga" };

            User NewUser(string username, UserRole role) => new()
            {
                FirstName = "Test", LastName = username, Username = username, Email = $"{username}@test.ba",
                DateOfBirth = new DateOnly(1990, 1, 1), Gender = gender, PasswordHash = "x", Role = role
            };

            MentorProfile NewMentor(string username) => new()
            {
                User = NewUser(username, UserRole.Mentor), TrainingType = type, Bio = new string('b', 60),
                YearsOfExperience = 5, MonthlyPrice = 20, Status = MentorApprovalStatus.Approved
            };

            var mentorA = NewMentor(TestUsers.MentorA);
            var mentorB = NewMentor(TestUsers.MentorB);
            mentorA.Certificates.Add(new MentorCertificate { FileName = "certifikat-trener.pdf", FileUrl = "seed://certificates/mentor-certifikat.pdf" });
            mentorA.Certificates.Add(new MentorCertificate { FileName = "diploma.png", FileUrl = "seed://certificates/mia-diploma.png" });
            mentorA.Certificates.Add(new MentorCertificate { FileName = "stari-format.pdf", FileUrl = "/seed/certificates/lejla-certifikat.pdf" });
            mentorA.Certificates.Add(new MentorCertificate { FileName = "nestao.pdf", FileUrl = "seed://certificates/ne-postoji.pdf" });
            mentorB.Certificates.Add(new MentorCertificate { FileName = "certifikat-b.pdf", FileUrl = "seed://certificates/dino-certifikat.pdf" });

            db.AddRange(
                NewUser(TestUsers.Admin, UserRole.Admin),
                mentorA,
                mentorB,
                new ClientProfile
                {
                    User = NewUser(TestUsers.Client, UserRole.Client), WeightKg = 80, HeightCm = 180,
                    FitnessLevel = level, FitnessGoal = goal, TrainingExperienceYears = 1
                });
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}

public static class TestUsers
{
    public const string Admin = "admin";
    public const string MentorA = "mentor.a";
    public const string MentorB = "mentor.b";
    public const string Client = "client";
}

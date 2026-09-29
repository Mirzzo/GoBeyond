using System.Data.Common;
using System.Net;
using GoBeyond.Core.Entities;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.ReferenceData;
using GoBeyond.Tests.TestInfrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GoBeyond.Tests.Security;

/// <summary>
/// Predugačak tekst pretrage (npr. 5000 znakova) ne smije oboriti upit: SQL Server odbija LIKE uzorak duži od 4000 znakova
/// (greška 8152 → 500). Pojam pretrage se centralno skraćuje na 100 znakova u NormalizeSearch.
/// </summary>
public sealed class SearchLengthTests(GoBeyondApiFactory factory) : IClassFixture<GoBeyondApiFactory>
{
    private static readonly string LongTerm = new('a', 5000);

    [Fact]
    public void NormalizeSearch_TrimsAndCapsTo100Characters()
    {
        Assert.Equal(100, LongTerm.NormalizeSearch()!.Length);
        Assert.Equal("abc", "  abc  ".NormalizeSearch());
        Assert.Null("   ".NormalizeSearch());
        Assert.Null(((string?)null).NormalizeSearch());
    }

    [Fact]
    public void NormalizeSearch_DoesNotSplitSurrogatePair()
    {
        var term = new string('a', 99) + "😀" + "b";

        Assert.Equal(new string('a', 99), term.NormalizeSearch());
    }

    [Fact]
    public async Task ReferenceDataNameFilter_SendsCappedTermToDatabase()
    {
        using var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var capture = new StringParameterCapture();
        await using var db = new SqliteTestDbContext(new DbContextOptionsBuilder<GoBeyondDbContext>()
            .UseSqlite(connection).AddInterceptors(capture).Options);
        await db.Database.EnsureCreatedAsync();
        db.Genders.Add(new Gender { Name = "Muško" });
        await db.SaveChangesAsync();
        capture.Values.Clear();

        var result = await new GenderService(db).GetAsync(new ReferenceSearchObject { Name = LongTerm });

        Assert.Empty(result.Items);
        Assert.NotEmpty(capture.Values);
        Assert.All(capture.Values, value => Assert.True(value.Length <= 102, $"Parametar ima {value.Length} znakova."));
    }

    [Theory]
    [InlineData(null, "/api/mentors?search=")]
    [InlineData(null, "/api/genders?name=")]
    [InlineData(null, "/api/training-types?name=")]
    [InlineData(null, "/api/fitness-goals?name=")]
    [InlineData(null, "/api/fitness-levels?name=")]
    [InlineData(TestUsers.Admin, "/api/admin/users?search=")]
    [InlineData(TestUsers.Admin, "/api/admin/mentors?search=")]
    [InlineData(TestUsers.Admin, "/api/admin/mentor-requests?search=")]
    [InlineData(TestUsers.Admin, "/api/admin/clients?search=")]
    [InlineData(TestUsers.Admin, "/api/admin/subscriptions?search=")]
    [InlineData(TestUsers.Admin, "/api/admin/announcements?search=")]
    [InlineData(TestUsers.Admin, "/api/admin/reports/mentors?search=")]
    [InlineData(TestUsers.Admin, "/api/admin/reports/clients?search=")]
    [InlineData(TestUsers.MentorA, "/api/mentors/me/collaboration-requests?search=")]
    [InlineData(TestUsers.MentorA, "/api/mentors/me/subscribers?search=")]
    [InlineData(TestUsers.MentorA, "/api/training-plans?search=")]
    [InlineData(TestUsers.Client, "/api/notifications?search=")]
    [InlineData(TestUsers.Client, "/api/messages/threads?search=")]
    public async Task EverySearchEndpoint_AcceptsVeryLongTerm(string? username, string url)
    {
        var response = await factory.ClientFor(username).GetAsync(url + LongTerm);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed class StringParameterCapture : DbCommandInterceptor
    {
        public List<string> Values { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Values.AddRange(command.Parameters.Cast<DbParameter>().Select(x => x.Value).OfType<string>());
            return ValueTask.FromResult(result);
        }
    }
}

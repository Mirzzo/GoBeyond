using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using GoBeyond.Tests.TestInfrastructure;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Tests.Security;

/// <summary>
/// Mentorski profil je vidljiv i dostupan za saradnju samo dok njegov korisnik ima ulogu Mentor: nakon što administrator
/// promijeni ulogu (npr. u Client), profil nestaje iz kataloga, detalja, sličnih mentora, preporuka, admin liste i izvještaja,
/// a nova saradnja se ne može započeti. Vraćanjem uloge Mentor profil je ponovo vidljiv.
/// </summary>
public sealed class MentorRoleVisibilityTests(GoBeyondApiFactory factory) : IClassFixture<GoBeyondApiFactory>
{
    [Fact]
    public async Task MentorDemotedToClient_IsHiddenEverywhere_AndRestoredWhenRoleReturns()
    {
        var (userId, mentorProfileId) = await PromoteToMentorAsync("vis_owner");
        var client = await SecurityTestApi.RegisterClientAsync(factory, "vis_client");
        var asClient = SecurityTestApi.WithToken(factory, client.AccessToken);
        Assert.Contains(mentorProfileId, await VisibleIdsAsync(asClient));

        var demote = await factory.ClientFor(TestUsers.Admin).PutAsJsonAsync($"/api/admin/users/{userId}",
            SecurityTestApi.AdminUpdateBody(factory, "vis_owner", "Client"));
        Assert.Equal(HttpStatusCode.OK, demote.StatusCode);

        Assert.DoesNotContain(mentorProfileId, await VisibleIdsAsync(asClient));
        var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/mentors/{mentorProfileId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/mentors/{mentorProfileId}/reviews")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/mentors/{mentorProfileId}/similar")).StatusCode);

        var subscribe = await asClient.PostAsJsonAsync("/api/subscriptions", new { mentorProfileId, questionnaire = Questionnaire() });
        Assert.Equal(HttpStatusCode.NotFound, subscribe.StatusCode);

        var promoteAgain = await factory.ClientFor(TestUsers.Admin).PutAsJsonAsync($"/api/admin/users/{userId}",
            SecurityTestApi.AdminUpdateBody(factory, "vis_owner", "Mentor"));
        Assert.Equal(HttpStatusCode.OK, promoteAgain.StatusCode);

        Assert.Contains(mentorProfileId, await VisibleIdsAsync(asClient));
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/mentors/{mentorProfileId}")).StatusCode);
    }

    [Fact]
    public async Task MentorDemotedToAdmin_IsNotCountedInReports()
    {
        var (userId, mentorProfileId) = await PromoteToMentorAsync("vis_admin");
        var admin = factory.ClientFor(TestUsers.Admin);
        var countBefore = await MentorCountAsync(admin);

        var toAdmin = await admin.PutAsJsonAsync($"/api/admin/users/{userId}", SecurityTestApi.AdminUpdateBody(factory, "vis_admin", "Admin"));
        Assert.Equal(HttpStatusCode.OK, toAdmin.StatusCode);

        var report = await admin.GetFromJsonAsync<JsonObject>("/api/admin/reports/mentors?search=vis_admin");
        Assert.DoesNotContain(report!["items"]!.AsArray(), x => (int)x!["mentorProfileId"]! == mentorProfileId);
        Assert.Equal(countBefore - 1, await MentorCountAsync(admin));
        var adminList = await admin.GetFromJsonAsync<JsonArray>("/api/admin/mentors");
        Assert.DoesNotContain(adminList!, x => (int)x!["mentorProfileId"]! == mentorProfileId);
    }

    [Fact]
    public async Task RoleChangeAwayFromMentor_WithOpenCollaboration_IsRejected()
    {
        var (userId, mentorProfileId) = await PromoteToMentorAsync("vis_busy");
        var client = await SecurityTestApi.RegisterClientAsync(factory, "vis_busy_client");
        var subscribe = await SecurityTestApi.WithToken(factory, client.AccessToken)
            .PostAsJsonAsync("/api/subscriptions", new { mentorProfileId, questionnaire = Questionnaire() });
        Assert.Equal(HttpStatusCode.OK, subscribe.StatusCode);

        var demote = await factory.ClientFor(TestUsers.Admin).PutAsJsonAsync($"/api/admin/users/{userId}",
            SecurityTestApi.AdminUpdateBody(factory, "vis_busy", "Client"));

        Assert.Equal(HttpStatusCode.BadRequest, demote.StatusCode);
        Assert.Contains("role", (await SecurityTestApi.ErrorAsync(demote)).Errors!.Keys);
        var catalog = await factory.CreateClient().GetFromJsonAsync<JsonArray>("/api/mentors");
        Assert.Contains(catalog!, x => (int)x!["mentorProfileId"]! == mentorProfileId);
    }

    /// <summary>ID-evi mentora koje klijent vidi: katalog, preporuke, slični mentori i admin lista moraju se slagati.</summary>
    private async Task<List<int>> VisibleIdsAsync(HttpClient asClient)
    {
        var anonymous = factory.CreateClient();
        var catalog = (await anonymous.GetFromJsonAsync<JsonArray>("/api/mentors"))!.Select(x => (int)x!["mentorProfileId"]!).ToList();

        var recommended = (await asClient.GetFromJsonAsync<JsonArray>("/api/recommendations/mentors?take=20"))!
            .Select(x => (int)x!["mentor"]!["mentorProfileId"]!).ToList();
        var anchor = factory.Query(db => db.MentorProfiles.AsNoTracking().Single(x => x.User.Username == TestUsers.MentorA).Id);
        var similar = (await anonymous.GetFromJsonAsync<JsonArray>($"/api/mentors/{anchor}/similar?take=20"))!
            .Select(x => (int)x!["mentorProfileId"]!).ToList();
        var adminList = (await factory.ClientFor(TestUsers.Admin).GetFromJsonAsync<JsonArray>("/api/admin/mentors"))!
            .Select(x => (int)x!["mentorProfileId"]!).ToList();

        // Sve liste moraju reći isto (element je vidljiv u svima ili ni u jednoj).
        var all = catalog.Except([anchor]).ToHashSet();
        Assert.True(all.SetEquals(recommended.Except([anchor])), "Preporuke se ne slažu sa katalogom.");
        Assert.True(all.SetEquals(similar), "Slični mentori se ne slažu sa katalogom.");
        Assert.True(catalog.ToHashSet().SetEquals(adminList), "Admin lista mentora se ne slaže sa katalogom.");
        return catalog;
    }

    private async Task<int> MentorCountAsync(HttpClient admin) =>
        (int)(await admin.GetFromJsonAsync<JsonObject>("/api/admin/reports/overview"))!["mentorCount"]!;

    private async Task<(int UserId, int MentorProfileId)> PromoteToMentorAsync(string username)
    {
        var session = await SecurityTestApi.RegisterClientAsync(factory, username);
        var promote = await factory.ClientFor(TestUsers.Admin).PutAsJsonAsync($"/api/admin/users/{session.User.Id}",
            SecurityTestApi.AdminUpdateBody(factory, username, "Mentor", withMentor: true));
        Assert.Equal(HttpStatusCode.OK, promote.StatusCode);
        var mentorProfileId = factory.Query(db => db.MentorProfiles.AsNoTracking().Single(x => x.UserId == session.User.Id).Id);
        return (session.User.Id, mentorProfileId);
    }

    private static object Questionnaire() => new
    {
        primaryGoal = "Snaga", timeCommitment = "3 sata", healthIssues = "Nema", medications = "Nema",
        weeklySessions = "3 puta", outsideActivity = "Šetnja"
    };
}

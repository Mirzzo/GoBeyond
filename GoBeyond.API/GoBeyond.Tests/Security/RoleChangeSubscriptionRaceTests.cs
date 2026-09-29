using System.Data.Common;
using GoBeyond.Core.DTOs.Admin;
using GoBeyond.Core.DTOs.Profile;
using GoBeyond.Core.DTOs.Subscriptions;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Security;
using GoBeyond.Infrastructure.Services.Admin;
using GoBeyond.Infrastructure.Services.Users;
using GoBeyond.Tests.Subscriptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GoBeyond.Tests.Security;

/// <summary>
/// Promjena uloge mentora i otvaranje pretplate kod tog mentora u istom trenutku: ili se uloga ne mijenja (vidi se nova pretplata),
/// ili se pretplata ne otvara (korisnik više nije mentor). Nikad oboje. Oba redoslijeda se reprodukuju deterministički, svaki
/// zahtjev na svojoj konekciji.
/// </summary>
public sealed class RoleChangeSubscriptionRaceTests : IDisposable
{
    private const string OpenCollaborationsError = "Uloga se ne može promijeniti dok korisnik ima aktivne ili započete saradnje. Prvo ih otkažite.";

    private readonly SubscriptionTestDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task MentorDemotedJustBeforeTheSubscriptionIsWritten_SubscriptionIsNotOpened()
    {
        var demotion = new BeforeFirstTransaction(() => _db.RunAsync(DemoteMentorAsync));
        await using var db = _db.CreateContext(demotion);

        var error = await Assert.ThrowsAsync<NotFoundException>(() => _db.Subscriptions(db).CreateAsync(_db.ClientUser.Id, SubscribeRequest()));

        Assert.True(demotion.Fired);
        Assert.Equal(DomainTexts.MentorNotFound, error.Message);
        Assert.Equal(UserRole.Client, await MentorRoleAsync());
        Assert.Equal(0, await OpenSubscriptionCountAsync());
    }

    [Fact]
    public async Task MentorDemotedWhileTheSubscriptionIsBeingWritten_DemotionIsRefused()
    {
        Task? demotion = null;
        var reachedFirstWrite = new SignalBeforeFirstUpdate();
        var subscribing = new BeforeCommit(async () =>
        {
            // Pretplata je upisana, ali još nije potvrđena: promjena uloge kreće sada i stiže do svog prvog upisa.
            demotion = Task.Run(async () =>
            {
                await using var adminDb = _db.CreateContext(reachedFirstWrite);
                await DemoteMentorAsync(adminDb);
            });
            await Task.WhenAny(reachedFirstWrite.Reached.Task, demotion);
        });
        await using var db = _db.CreateContext(subscribing);

        var subscription = await _db.Subscriptions(db).CreateAsync(_db.ClientUser.Id, SubscribeRequest());

        var error = await Assert.ThrowsAsync<ValidationException>(() => demotion!);
        Assert.Equal([OpenCollaborationsError], error.Errors["role"]);
        Assert.Equal(SubscriptionStatus.PendingPayment, subscription.Status);
        Assert.Equal(UserRole.Mentor, await MentorRoleAsync());
    }

    private CreateSubscriptionRequest SubscribeRequest() => new()
    {
        MentorProfileId = _db.MentorProfileId, Questionnaire = SubscriptionTestDatabase.QuestionnaireRequest()
    };

    /// <summary>Administrator mijenja ulogu prvog mentora u Klijent.</summary>
    private async Task DemoteMentorAsync(GoBeyondDbContext db)
    {
        var (genderId, levelId, goalId) = (await db.Genders.Select(x => x.Id).FirstAsync(), await db.FitnessLevels.Select(x => x.Id).FirstAsync(),
            await db.FitnessGoals.Select(x => x.Id).FirstAsync());
        var mentor = _db.MentorUser;
        await new AdminUserService(db, new UserAccountValidator(db), new PasswordHasher(), _db.Workflow(db)).UpdateUserAsync(adminUserId: 0, mentor.Id,
            new AdminUpdateUserRequest
            {
                FirstName = mentor.FirstName, LastName = mentor.LastName, Username = mentor.Username, Email = mentor.Email,
                DateOfBirth = mentor.DateOfBirth, GenderId = genderId, Role = UserRole.Client,
                Client = new ClientProfileRequest { WeightKg = 60, HeightCm = 170, FitnessLevelId = levelId, TrainingExperienceYears = 1, FitnessGoalId = goalId }
            });
    }

    private Task<UserRole> MentorRoleAsync() => _db.RunAsync(db => db.Users.Where(x => x.Id == _db.MentorUser.Id).Select(x => x.Role).SingleAsync());

    private Task<int> OpenSubscriptionCountAsync() => _db.RunAsync(db => db.Subscriptions
        .CountAsync(x => x.ClientProfileId == _db.ClientProfileId && QueryExtensions.OpenStatuses.Contains(x.Status)));

    /// <summary>Jednom, neposredno prije prve transakcije ovog zahtjeva, izvrši drugi zahtjev do kraja.</summary>
    private sealed class BeforeFirstTransaction(Func<Task> otherRequest) : DbTransactionInterceptor
    {
        private int _fired;
        public bool Fired => _fired == 1;

        public override async ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(DbConnection connection,
            TransactionStartingEventData eventData, InterceptionResult<DbTransaction> result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _fired, 1) == 0) await otherRequest();
            return result;
        }
    }

    /// <summary>Jednom, neposredno prije potvrde transakcije ovog zahtjeva, pokrene drugi zahtjev.</summary>
    private sealed class BeforeCommit(Func<Task> otherRequest) : DbTransactionInterceptor
    {
        private int _fired;

        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Exchange(ref _fired, 1) == 0) await otherRequest();
            return result;
        }
    }

    /// <summary>Javlja kad zahtjev stigne do svog prvog UPDATE-a (koji čeka dok druga transakcija drži bazu za upis).</summary>
    private sealed class SignalBeforeFirstUpdate : DbCommandInterceptor
    {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.StartsWith("UPDATE", StringComparison.Ordinal)) Reached.TrySetResult();
            return ValueTask.FromResult(result);
        }
    }
}

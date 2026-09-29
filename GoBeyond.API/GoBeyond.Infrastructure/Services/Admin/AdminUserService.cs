using GoBeyond.Core.DTOs.Admin;
using GoBeyond.Core.DTOs.Auth;
using GoBeyond.Core.DTOs.Common;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Core.Exceptions;
using GoBeyond.Core.SearchObjects;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Security;
using GoBeyond.Infrastructure.Services.Auth;
using GoBeyond.Infrastructure.Services.Subscriptions;
using GoBeyond.Infrastructure.Services.Users;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Infrastructure.Services.Admin;

public interface IAdminUserService
{
    Task<List<AdminUserDto>> GetUsersAsync(AdminUserSearchObject search, CancellationToken cancellationToken = default);
    Task<AdminUserDetailDto> GetUserAsync(int id, CancellationToken cancellationToken = default);
    Task<AdminUserDetailDto> UpdateUserAsync(int adminUserId, int id, AdminUpdateUserRequest request, CancellationToken cancellationToken = default);
    Task<MessageResponse> ResetPasswordAsync(int id, ResetPasswordRequest request, CancellationToken cancellationToken = default);
    Task<AdminUserDto> BlockAsync(int adminUserId, int id, CancellationToken cancellationToken = default);
    Task<AdminUserDto> UnblockAsync(int id, CancellationToken cancellationToken = default);
    Task<AdminDeleteUserResponse> DeleteAsync(int adminUserId, int id, CancellationToken cancellationToken = default);
}

public sealed class AdminUserService(
    GoBeyondDbContext db,
    IUserAccountValidator accountValidator,
    IPasswordHasher passwordHasher,
    ISubscriptionWorkflow workflow) : IAdminUserService
{
    public const string DeleteRefundFailed = "Brisanje nije moguće: povrat uplate klijentu nije uspio. Pokušajte ponovo.";

    public async Task<List<AdminUserDto>> GetUsersAsync(AdminUserSearchObject search, CancellationToken cancellationToken = default)
    {
        var query = db.Users.AsNoTracking().Where(x => !x.IsDeleted);
        if (search.Search.NormalizeSearch() is { } term)
            query = query.Where(x => x.FirstName.Contains(term) || x.LastName.Contains(term) ||
                                     (x.FirstName + " " + x.LastName).Contains(term) ||
                                     x.Username.Contains(term) || x.Email.Contains(term));
        if (search.Role is { } role) query = query.Where(x => x.Role == role);
        if (search.IsActive is { } isActive) query = query.Where(x => x.IsActive == isActive);

        var users = await query.OrderBy(x => x.FirstName).ThenBy(x => x.LastName).ToListAsync(cancellationToken);
        return users.Select(ProfileMapper.ToAdminUser).ToList();
    }

    public async Task<AdminUserDetailDto> GetUserAsync(int id, CancellationToken cancellationToken = default) =>
        ProfileMapper.ToAdminUserDetail(await LoadAsync(id, cancellationToken));

    public async Task<AdminUserDetailDto> UpdateUserAsync(int adminUserId, int id, AdminUpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await LoadAsync(id, cancellationToken);
        var roleChanged = user.Role != request.Role;

        var errors = new ValidationErrorCollector();
        await accountValidator.ValidateAccountAsync(errors, request, id, cancellationToken);
        if (roleChanged && id == adminUserId)
            errors.Add("role", "Ne možete promijeniti vlastitu ulogu.");

        var needsMentor = request.Role == UserRole.Mentor && user.MentorProfile is null;
        var needsClient = request.Role == UserRole.Client && user.ClientProfile is null;
        if (needsMentor && request.Mentor is null)
            errors.Add("mentor", "Za ulogu Mentor unesite mentorske podatke (vrsta treninga, biografija, iskustvo, cijena i specijalizacije).");
        if (needsClient && request.Client is null)
            errors.Add("client", "Za ulogu Klijent unesite klijentske podatke (težina, visina, nivo spreme, iskustvo i cilj).");
        if (request.Mentor is not null)
            await accountValidator.ValidateMentorAsync(errors, request.Mentor.TrainingTypeId, request.Mentor.SpecializationIds, "mentor.", cancellationToken);
        if (request.Client is not null)
            await accountValidator.ValidateClientAsync(errors, request.Client.FitnessLevelId, request.Client.FitnessGoalId,
                request.Client.PreferredTrainingTypeId, "client.", cancellationToken);

        // Zaključava se profil, pa korisnik (UserSessions), istim redoslijedom kao kod izmjene vlastitog profila i odobravanja
        // mentora, pa nema deadlock-a. Mentorski profil je zaključan do kraja izmjene: pretplata koja se kod ovog mentora upravo
        // otvara (SubscriptionService) se ili vidi u provjeri ispod, ili se nakon promjene uloge ne otvara. Zaključan korisnik se
        // ne preplete sa promjenom lozinke, blokiranjem ili brisanjem istog korisnika.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await LockProfilesAsync(user, cancellationToken);
        await LockAndRefreshAsync(user, cancellationToken);
        if (roleChanged && await HasOpenCollaborationsAsync(user, cancellationToken))
            errors.Add("role", "Uloga se ne može promijeniti dok korisnik ima aktivne ili započete saradnje. Prvo ih otkažite.");
        errors.ThrowIfAny();

        ProfileUpdater.ApplyAccount(user, request);

        if (request.Mentor is not null)
        {
            if (user.MentorProfile is null)
            {
                // Mentor kojeg kreira administrator je odmah odobren.
                user.MentorProfile = new MentorProfile { Status = MentorApprovalStatus.Approved, ReviewedAt = DateTime.UtcNow };
            }
            ProfileUpdater.ApplyMentor(user.MentorProfile, request.Mentor);
        }
        if (request.Client is not null)
        {
            user.ClientProfile ??= new ClientProfile();
            ProfileUpdater.ApplyClient(user.ClientProfile, request.Client);
        }

        if (roleChanged)
        {
            // Mentorski profil ostaje (vraćanjem uloge Mentor je ponovo vidljiv), ali dok uloga nije Mentor
            // profil se ne prikazuje i ne može se ugovoriti saradnja (QueryExtensions.Visible).
            user.Role = request.Role;
            await db.EndSessionsAsync(user, keepSessionId: null, cancellationToken);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await accountValidator.ThrowIfAccountTakenAsync(request, id, cancellationToken);
            throw;
        }
        await transaction.CommitAsync(cancellationToken);
        return await GetUserAsync(id, cancellationToken);
    }

    public async Task<MessageResponse> ResetPasswordAsync(int id, ResetPasswordRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockAndLoadAsync(id, cancellationToken);
        user.PasswordHash = passwordHasher.Hash(request.NewPassword);
        await db.EndSessionsAsync(user, keepSessionId: null, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new MessageResponse($"Lozinka korisnika {user.FullName} je uspješno resetovana.");
    }

    public async Task<AdminUserDto> BlockAsync(int adminUserId, int id, CancellationToken cancellationToken = default)
    {
        if (id == adminUserId) throw new ValidationException("Ne možete blokirati vlastiti nalog.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockAndLoadAsync(id, cancellationToken);
        user.IsActive = false;
        await db.EndSessionsAsync(user, keepSessionId: null, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ProfileMapper.ToAdminUser(user);
    }

    public async Task<AdminUserDto> UnblockAsync(int id, CancellationToken cancellationToken = default)
    {
        // Obrisan korisnik se ne vraća: IsDeleted filter ga ne pronalazi, ni kad je obrisan dok je zahtjev čekao na zaključavanje.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var user = await LockAndLoadAsync(id, cancellationToken);
        user.IsActive = true;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ProfileMapper.ToAdminUser(user);
    }

    /// <summary>
    /// Soft delete. Za mentora: sve započete/aktivne saradnje se prekidaju (klijent dobija obavijest,
    /// više se ništa ne naplaćuje ni obnavlja), a planovi ostaju dostupni klijentima samo za čitanje.
    /// Uplate za neprihvaćene zahtjeve (AwaitingMentor) se vraćaju; ako povrat ne uspije, brisanje se ne izvršava. Naplata
    /// osporena kod banke klijenta se ne vraća (Disputed), a odgovor tada nosi upozorenje za administratora.
    /// </summary>
    public async Task<AdminDeleteUserResponse> DeleteAsync(int adminUserId, int id, CancellationToken cancellationToken = default)
    {
        if (id == adminUserId) throw new ValidationException("Ne možete obrisati vlastiti nalog.");
        var now = DateTime.UtcNow;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Otvorene pretplate se zaključavaju (redom po Id-u) prije čitanja, pa istovremeni confirm ili prihvatanje ne može
        // pregaziti otkazivanje. Korisnik se zaključava poslije pretplata, istim redoslijedom kao prelazi pretplata (pretplata,
        // pa korisnik kojem ide obavijest), a prije svojih refresh tokena (UserSessions).
        var subscriptionIds = await db.Subscriptions
            .Where(x => QueryExtensions.OpenStatuses.Contains(x.Status) &&
                        (x.MentorProfile.UserId == id || x.ClientProfile.UserId == id))
            .OrderBy(x => x.Id)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
        foreach (var subscriptionId in subscriptionIds)
            await db.LockSubscriptionAsync(subscriptionId, cancellationToken);
        var user = await LockAndLoadAsync(id, cancellationToken);

        var subscriptions = await db.Subscriptions
            .Include(x => x.ClientProfile).ThenInclude(x => x.User)
            .Include(x => x.MentorProfile).ThenInclude(x => x.User)
            .Include(x => x.Payments)
            .Where(x => subscriptionIds.Contains(x.Id) && QueryExtensions.OpenStatuses.Contains(x.Status))
            .ToListAsync(cancellationToken);

        RefundOutcome refunds = default;
        foreach (var subscription in subscriptions)
        {
            var mentorRemoved = subscription.MentorProfile.UserId == id;
            // Uplate neprihvaćenog zahtjeva (AwaitingMentor) se vraćaju i klijent to vidi u obavijesti. Neuspio povrat prekida
            // brisanje (transakcija se poništava); ponovni pokušaj koristi isti Stripe Idempotency-Key, pa se već izvršeni
            // povrat ne ponavlja. Mentor dobija obavijest samo o zahtjevu koji je vidio (ne o neplaćenoj PendingPayment).
            refunds = refunds.Plus(await workflow.CancelAsync(subscription,
                mentorRemoved ? DomainTexts.MentorRemovedReason : DomainTexts.ClientRemovedReason,
                now, notifyClient: mentorRemoved, notifyMentor: !mentorRemoved, DeleteRefundFailed, cancellationToken));
        }

        user.IsDeleted = true;
        user.IsActive = false;
        await db.EndSessionsAsync(user, keepSessionId: null, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Osporenu naplatu Stripe ne vraća, pa administrator dobija upozorenje (kao kod admin otkazivanja pretplate).
        var currency = subscriptions.Select(x => x.Currency).FirstOrDefault() ?? string.Empty;
        return new AdminDeleteUserResponse($"Korisnik {user.FullName} je obrisan.", refunds.DisputeWarning(currency));
    }

    /// <summary>
    /// Zaključava korisnika u tekućoj transakciji (UserSessions) i tek onda ga učitava, pa izmjena vidi zadnje stanje (npr.
    /// brisanje ili promjenu lozinke koja je upravo završila). Obrisan korisnik se ne pronalazi.
    /// </summary>
    private async Task<User> LockAndLoadAsync(int id, CancellationToken cancellationToken)
    {
        await db.LockUserAsync(id, cancellationToken);
        return await db.Users.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
               ?? throw new NotFoundException(DomainTexts.UserNotFound);
    }

    /// <summary>Zaključava postojeće profile korisnika (UPDATE bez promjene) do kraja tekuće transakcije.</summary>
    private async Task LockProfilesAsync(User user, CancellationToken cancellationToken)
    {
        if (user.ClientProfile is not null)
            await db.ClientProfiles.Where(x => x.Id == user.ClientProfile.Id)
                .ExecuteUpdateAsync(x => x.SetProperty(c => c.FitnessLevelId, c => c.FitnessLevelId), cancellationToken);
        if (user.MentorProfile is not null)
            await db.LockMentorProfileAsync(user.MentorProfile.Id, cancellationToken);
    }

    /// <summary>Isto za korisnika učitanog prije transakcije (sa profilima): nakon zaključavanja se osvježava iz baze.</summary>
    private async Task LockAndRefreshAsync(User user, CancellationToken cancellationToken)
    {
        await db.LockUserAsync(user.Id, cancellationToken);
        await db.Entry(user).ReloadAsync(cancellationToken);
        if (user.IsDeleted) throw new NotFoundException(DomainTexts.UserNotFound);
    }

    private async Task<User> LoadAsync(int id, CancellationToken cancellationToken) =>
        await db.Users.WithProfile().AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)
        ?? throw new NotFoundException(DomainTexts.UserNotFound);

    private Task<bool> HasOpenCollaborationsAsync(User user, CancellationToken cancellationToken) =>
        db.Subscriptions.AnyAsync(x => QueryExtensions.OpenStatuses.Contains(x.Status) &&
                                       (x.MentorProfile.UserId == user.Id || x.ClientProfile.UserId == user.Id), cancellationToken);
}

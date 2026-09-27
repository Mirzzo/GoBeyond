using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Infrastructure.Common;
using GoBeyond.Infrastructure.Configuration;
using GoBeyond.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoBeyond.Infrastructure.Database;

/// <summary>
/// Seed šifarnika i demo podataka (API ugovor, sekcija 10).
/// Šifarnik se puni samo ako je prazan, a demo podaci samo u bazu bez korisnika.
/// Svi datumi su relativni u odnosu na trenutak seedanja (demo uvijek izgleda aktuelno),
/// a pseudo-slučajne vrijednosti koriste fiksni seed (podaci su uvijek isti).
/// Sadržaj (tekstovi, planovi, poruke) je u ostalim DatabaseSeeder.*.cs fajlovima.
/// </summary>
public sealed partial class DatabaseSeeder(
    GoBeyondDbContext db,
    IPasswordHasher passwordHasher,
    IOptions<PaymentOptions> paymentOptions,
    ILogger<DatabaseSeeder> logger) : IDatabaseSeeder
{
    private const string DemoPassword = "test";
    private const int RandomSeed = 210020;
    private const int ActivityDays = 60;

    private readonly Dictionary<string, User> _users = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MentorProfile> _mentors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ClientProfile> _clients = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Subscription> _subscriptions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TrainingPlan> _plans = new(StringComparer.Ordinal);

    private Dictionary<string, TrainingType> _trainingTypes = new();
    private Dictionary<string, FitnessGoal> _fitnessGoals = new();
    private Dictionary<string, FitnessLevel> _fitnessLevels = new();
    private Dictionary<string, Gender> _genders = new();

    private Random _random = new(RandomSeed);
    private DateTime _now;
    private DateTime _today;
    private string _currency = string.Empty;
    private string _passwordHash = string.Empty;

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedReferenceDataAsync(cancellationToken);

        if (await db.Users.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Baza već sadrži korisnike, demo podaci se ne dodaju.");
            return;
        }

        // Execution strategy dozvoljava vlastitu transakciju i kad je uključen EnableRetryOnFailure.
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(ct => SeedDemoDataAsync(ct), cancellationToken);
    }

    private async Task SeedDemoDataAsync(CancellationToken cancellationToken)
    {
        // Eventualni ponovni pokušaj uvijek kreće od čistog stanja.
        db.ChangeTracker.Clear();
        ResetState();
        await LoadReferenceDataAsync(cancellationToken);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        SeedUsers();
        await db.SaveChangesAsync(cancellationToken);

        SeedSubscriptions();
        SeedTrainingPlans();
        await db.SaveChangesAsync(cancellationToken);

        // Treba im Id plana i dana (sesije, snapshot plana u napretku).
        SeedTrainingSessions();
        SeedProgressEntries();
        SeedMessages();
        SeedNotifications();
        SeedAnnouncements();
        SeedUserActivity();
        await db.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        LogSummary();
    }

    private void ResetState()
    {
        _now = DateTime.UtcNow;
        _today = _now.Date;
        _random = new Random(RandomSeed);
        var currency = paymentOptions.Value.Currency;
        _currency = string.IsNullOrWhiteSpace(currency) ? "usd" : currency.Trim();

        // PBKDF2 je spor, pa svi demo nalozi dijele isti hash lozinke "test".
        if (_passwordHash.Length == 0) _passwordHash = passwordHasher.Hash(DemoPassword);

        _users.Clear();
        _mentors.Clear();
        _clients.Clear();
        _subscriptions.Clear();
        _plans.Clear();
    }

    // ---------------------------------------------------------------- šifarnici

    private async Task SeedReferenceDataAsync(CancellationToken cancellationToken)
    {
        var added = 0;

        if (!await db.TrainingTypes.AnyAsync(cancellationToken))
        {
            db.TrainingTypes.AddRange(TrainingTypeSeeds.Select(x => new TrainingType { Name = x.Name, Description = x.Description }));
            added += TrainingTypeSeeds.Length;
        }

        if (!await db.FitnessGoals.AnyAsync(cancellationToken))
        {
            db.FitnessGoals.AddRange(FitnessGoalSeeds.Select(x => new FitnessGoal { Name = x.Name, Description = x.Description }));
            added += FitnessGoalSeeds.Length;
        }

        if (!await db.FitnessLevels.AnyAsync(cancellationToken))
        {
            db.FitnessLevels.AddRange(FitnessLevelSeeds.Select(x =>
                new FitnessLevel { Name = x.Name, Description = x.Description, SortOrder = x.SortOrder }));
            added += FitnessLevelSeeds.Length;
        }

        if (!await db.Genders.AnyAsync(cancellationToken))
        {
            db.Genders.AddRange(GenderSeeds.Select(x => new Gender { Name = x }));
            added += GenderSeeds.Length;
        }

        if (added == 0) return;

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Šifarnici su popunjeni ({Count} zapisa).", added);
    }

    private async Task LoadReferenceDataAsync(CancellationToken cancellationToken)
    {
        _trainingTypes = await db.TrainingTypes.ToDictionaryAsync(x => x.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);
        _fitnessGoals = await db.FitnessGoals.ToDictionaryAsync(x => x.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);
        _fitnessLevels = await db.FitnessLevels.ToDictionaryAsync(x => x.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);
        _genders = await db.Genders.ToDictionaryAsync(x => x.Name, StringComparer.OrdinalIgnoreCase, cancellationToken);
    }

    /// <summary>Stavke šifarnika se traže po nazivu (Id-evi zavise od baze).</summary>
    private static int IdOf<T>(Dictionary<string, T> items, string name) where T : BaseEntity =>
        items.TryGetValue(name, out var item)
            ? item.Id
            : throw new InvalidOperationException($"Šifarnik {typeof(T).Name} ne sadrži stavku \"{name}\".");

    // ---------------------------------------------------------------- korisnici

    private void SeedUsers()
    {
        AddUser("admin", "Adnan", "Hodžić", UserRole.Admin, GenderMale, age: 38, createdDay: -420, "+387 61 100 200");
        AddUser("desktop", "Emina", "Karić", UserRole.Admin, GenderFemale, age: 33, createdDay: -415, "+387 62 345 678");

        foreach (var seed in MentorSeeds) AddMentor(seed);
        foreach (var seed in ClientSeeds) AddClient(seed);
    }

    private User AddUser(string username, string firstName, string lastName, UserRole role, string gender,
        int age, int createdDay, string? phoneNumber)
    {
        var user = new User
        {
            FirstName = firstName,
            LastName = lastName,
            Username = username,
            Email = $"{username}@gobeyond.ba",
            PhoneNumber = phoneNumber,
            DateOfBirth = new DateOnly(_today.Year - age, _random.Next(1, 13), _random.Next(1, 29)),
            GenderId = IdOf(_genders, gender),
            PasswordHash = _passwordHash,
            Role = role,
            ProfileImageUrl = $"/seed/avatars/{username}.png",
            IsActive = true,
            IsDeleted = false,
            CreatedAt = D(createdDay)
        };

        db.Users.Add(user);
        _users.Add(username, user);
        return user;
    }

    private void AddMentor(MentorSeed seed)
    {
        var user = AddUser(seed.Username, seed.FirstName, seed.LastName, UserRole.Mentor, seed.Gender,
            seed.Age, seed.CreatedDay, seed.PhoneNumber);
        var isApproved = seed.Status == MentorApprovalStatus.Approved;
        DateTime? reviewedAt = seed.ReviewedDay is { } reviewedDay ? D(reviewedDay) : null;

        var profile = new MentorProfile
        {
            User = user,
            TrainingTypeId = IdOf(_trainingTypes, seed.TrainingType),
            Nickname = seed.Nickname,
            Bio = Normalize(seed.Bio),
            YearsOfExperience = seed.YearsOfExperience,
            MonthlyPrice = seed.MonthlyPrice,
            Status = seed.Status,
            RejectionReason = seed.RejectionReason,
            ReviewedAt = reviewedAt
        };

        foreach (var goal in seed.Specializations)
        {
            profile.Specializations.Add(new MentorSpecialization { FitnessGoalId = IdOf(_fitnessGoals, goal) });
        }

        foreach (var certificate in seed.Certificates)
        {
            profile.Certificates.Add(new MentorCertificate
            {
                FileName = certificate.FileName,
                FileUrl = $"/seed/certificates/{certificate.File}",
                UploadedAt = user.CreatedAt,
                IsVerified = isApproved,
                VerifiedAt = isApproved ? reviewedAt : null
            });
        }

        user.MentorProfile = profile;
        _mentors.Add(seed.Username, profile);
    }

    private void AddClient(ClientSeed seed)
    {
        var user = AddUser(seed.Username, seed.FirstName, seed.LastName, UserRole.Client, seed.Gender,
            seed.Age, seed.CreatedDay, seed.PhoneNumber);

        if (seed.BlockedLastLoginDay is { } lastLoginDay)
        {
            user.IsActive = false;
            user.LastLoginAt = D(lastLoginDay);
        }

        var profile = new ClientProfile
        {
            User = user,
            WeightKg = seed.WeightKg,
            HeightCm = seed.HeightCm,
            FitnessLevelId = IdOf(_fitnessLevels, seed.FitnessLevel),
            TrainingExperienceYears = seed.TrainingExperienceYears,
            FitnessGoalId = IdOf(_fitnessGoals, seed.FitnessGoal),
            GoalDescription = seed.GoalDescription,
            PreferredTrainingTypeId = seed.PreferredTrainingType is { } type ? IdOf(_trainingTypes, type) : null
        };

        user.ClientProfile = profile;
        _clients.Add(seed.Username, profile);
    }

    // ---------------------------------------------------------------- pretplate, uplate, recenzije

    private void SeedSubscriptions()
    {
        foreach (var seed in SubscriptionSeeds)
        {
            var subscription = AddSubscription(seed);
            if (seed.Review is { } review) AddReview(subscription, review);
        }
    }

    private Subscription AddSubscription(SubscriptionSeed seed)
    {
        var mentor = _mentors[seed.Mentor];
        var createdAt = D(seed.CreatedDay);

        // Sve osim PendingPayment je plaćeno par minuta nakon kreiranja.
        DateTime? paidAt = seed.Status == SubscriptionStatus.PendingPayment ? null : createdAt.AddMinutes(6);

        // Mentor prihvata zahtjev par sati nakon uplate (ili narednih dana), period traje do EndDay.
        DateTime? startDate = null;
        DateTime? endDate = null;
        if (seed.Period is { } period)
        {
            startDate = D(period.Start).AddHours(period.Start == seed.CreatedDay ? 3 : 0);
            endDate = startDate.Value.AddDays(period.End - period.Start);
        }

        var subscription = new Subscription
        {
            ClientProfile = _clients[seed.Client],
            MentorProfile = mentor,
            Status = seed.Status,
            Price = mentor.MonthlyPrice,
            Currency = _currency,
            CreatedAt = createdAt,
            PaidAt = paidAt,
            AcceptedAt = startDate,
            StartDate = startDate,
            EndDate = endDate,
            CancelledAt = seed.CancelledDay is { } cancelledDay ? D(cancelledDay) : null,
            StatusReason = seed.StatusReason,
            ExpiryReminderSentAt = seed.Status == SubscriptionStatus.Expired ? endDate?.AddDays(-3) : null,
            Questionnaire = new Questionnaire
            {
                PrimaryGoal = seed.Questionnaire.PrimaryGoal,
                TimeCommitment = seed.Questionnaire.TimeCommitment,
                HealthIssues = seed.Questionnaire.HealthIssues,
                Medications = seed.Questionnaire.Medications,
                WeeklySessions = seed.Questionnaire.WeeklySessions,
                OutsideActivity = seed.Questionnaire.OutsideActivity
            }
        };

        if (paidAt is { } initialPaidAt)
        {
            DateTime? refundedAt = seed.RefundedDay is { } refundedDay ? D(refundedDay) : null;
            AddPayment(subscription, seed.Key, 1, PaymentPurpose.Initial, initialPaidAt, refundedAt);
            for (var i = 0; i < seed.RenewalDays.Length; i++)
            {
                AddPayment(subscription, seed.Key, i + 2, PaymentPurpose.Renewal, D(seed.RenewalDays[i]), null);
            }
        }

        db.Subscriptions.Add(subscription);
        _subscriptions.Add(seed.Key, subscription);
        return subscription;
    }

    private static void AddPayment(Subscription subscription, string key, int number, PaymentPurpose purpose,
        DateTime paidAt, DateTime? refundedAt)
    {
        subscription.Payments.Add(new Payment
        {
            Amount = subscription.Price,
            Currency = subscription.Currency,
            // Namjerno bez prefiksa "pi_" da se nikad ne pomiješa sa stvarnim Stripe uplatama.
            StripePaymentIntentId = $"seed_pi_{key}_{number}",
            Purpose = purpose,
            Status = refundedAt is null ? PaymentStatus.Succeeded : PaymentStatus.Refunded,
            CreatedAt = paidAt,
            PaidAt = paidAt,
            RefundedAt = refundedAt
        });
    }

    private void AddReview(Subscription subscription, ReviewSeed review)
    {
        var endedAt = subscription.CancelledAt ?? subscription.EndDate ?? subscription.CreatedAt;
        db.Reviews.Add(new Review
        {
            Subscription = subscription,
            ClientProfile = subscription.ClientProfile,
            MentorProfile = subscription.MentorProfile,
            Rating = review.Rating,
            Comment = review.Comment,
            CreatedAt = NotFuture(endedAt.AddDays(review.DaysAfterEnd).AddHours(2))
        });
    }

    // ---------------------------------------------------------------- trening planovi

    private void SeedTrainingPlans()
    {
        AddPlan("client_lejla", TrainingPlanStatus.Archived, version: 3,
            createdDay: -184, publishedDay: -183, updatedDay: -120, HybridWeek(TarikHybridProfile));
        AddPlan("client_mentor", TrainingPlanStatus.Published, version: 2,
            createdDay: -79, publishedDay: -78, updatedDay: -10, ClientMentorPlan(), lastUpdateNotifiedDay: -10);
        AddPlan("mobile_amir", TrainingPlanStatus.Published, version: 1,
            createdDay: -125, publishedDay: -124, updatedDay: -124, MobileAmirPlan());

        // Nacrt: mentor je popunio tek prva tri dana, plan još nije objavljen.
        var adnaDraft = StrengthWeek(AdnaStrengthProfile);
        AddPlan("adna_mentor", TrainingPlanStatus.Draft, version: 1,
            createdDay: -3, publishedDay: null, updatedDay: -2,
            adnaDraft with { Days = adnaDraft.Days.Where(x => x.DayOfWeek <= 3).ToList() });

        AddPlan("belma_mentor", TrainingPlanStatus.Archived, version: 1,
            createdDay: -100, publishedDay: -99, updatedDay: -70, StrengthWeek(BelmaStrengthProfile));
        AddPlan("nedim_kenan", TrainingPlanStatus.Published, version: 1,
            createdDay: -40, publishedDay: -39, updatedDay: -39, HybridWeek(NedimHybridProfile));
        AddPlan("lamija_lejla", TrainingPlanStatus.Published, version: 1,
            createdDay: -15, publishedDay: -14, updatedDay: -14, HybridWeek(LamijaHybridProfile));
    }

    private void AddPlan(string subscriptionKey, TrainingPlanStatus status, int version, int createdDay,
        int? publishedDay, int updatedDay, PlanContent content, int? lastUpdateNotifiedDay = null)
    {
        var subscription = _subscriptions[subscriptionKey];

        // Plan nastaje tek nakon što mentor prihvati saradnju.
        var createdAt = Max(D(createdDay), (subscription.AcceptedAt ?? subscription.CreatedAt).AddHours(1));
        DateTime? publishedAt = publishedDay is { } published ? Max(D(published), createdAt.AddHours(1)) : null;

        var plan = new TrainingPlan
        {
            Subscription = subscription,
            MentorProfile = subscription.MentorProfile,
            ClientProfile = subscription.ClientProfile,
            MotivationalQuote = content.Quote,
            Status = status,
            Version = version,
            CreatedAt = createdAt,
            PublishedAt = publishedAt,
            UpdatedAt = Max(D(updatedDay), publishedAt ?? createdAt),
            LastUpdateNotifiedAt = lastUpdateNotifiedDay is { } notifiedDay ? D(notifiedDay) : null
        };

        foreach (var day in content.Days)
        {
            plan.Days.Add(new DayPlan
            {
                DayOfWeek = day.DayOfWeek,
                TrainingDurationMinutes = day.TrainingMinutes,
                TrainingDescription = Normalize(day.Training),
                NutritionDurationMinutes = day.NutritionMinutes,
                NutritionDescription = Normalize(day.Nutrition)
            });
        }

        db.TrainingPlans.Add(plan);
        _plans.Add(subscriptionKey, plan);
    }

    // ---------------------------------------------------------------- odrađeni treninzi

    private void SeedTrainingSessions()
    {
        foreach (var (planKey, seed) in SessionSeeds) AddSessions(_plans[planKey], seed);
    }

    private void AddSessions(TrainingPlan plan, SessionSeed seed)
    {
        var daysOfPlan = plan.Days.ToDictionary(x => x.DayOfWeek);
        var trainingDates = new List<DateTime>();
        var restDates = new List<DateTime>();

        for (var offset = seed.FromDay; offset <= seed.ToDay; offset++)
        {
            var date = _today.AddDays(offset);
            var planDay = BosnianCalendar.ToPlanDay(date.DayOfWeek);
            if (!daysOfPlan.ContainsKey(planDay)) continue;
            (seed.TrainingDays.Contains(planDay) ? trainingDates : restDates).Add(date);
        }

        var dates = PickRandom(trainingDates, seed.Count - seed.RestDayCount)
            .Concat(PickRandom(restDates, seed.RestDayCount))
            .OrderBy(x => x)
            .ToList();

        var sessions = new List<TrainingSession>(dates.Count);
        for (var i = 0; i < dates.Count; i++)
        {
            var date = dates[i];
            var dayPlan = daysOfPlan[BosnianCalendar.ToPlanDay(date.DayOfWeek)];
            var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            var hour = isWeekend ? _random.Next(9, 12) : _random.Next(17, 21);

            // Broj ponavljanja blago raste kroz period (napredak klijenta).
            var trend = dates.Count > 1 ? seed.Trend * i / (dates.Count - 1) : 0;
            var repetitions = seed.BaseRepetitions[dayPlan.DayOfWeek - 1] + trend + _random.Next(-6, 7);

            sessions.Add(new TrainingSession
            {
                TrainingPlan = plan,
                DayPlan = dayPlan,
                ClientProfile = plan.ClientProfile,
                CompletedAt = date.AddHours(hour).AddMinutes(_random.Next(0, 60)),
                Repetitions = Math.Max(1, repetitions)
            });
        }

        AttachNotes(sessions, seed.Notes);
        db.TrainingSessions.AddRange(sessions);
    }

    /// <summary>Nasumično (ali deterministički) zadržava traženi broj datuma.</summary>
    private List<DateTime> PickRandom(List<DateTime> source, int count)
    {
        var picked = new List<DateTime>(source);
        while (picked.Count > Math.Max(0, count)) picked.RemoveAt(_random.Next(picked.Count));
        return picked;
    }

    /// <summary>Bilješka ide na trening odgovarajućeg dana u sedmici koji je najbliži zadanom datumu.</summary>
    private void AttachNotes(List<TrainingSession> sessions, IEnumerable<SessionNote> notes)
    {
        foreach (var note in notes)
        {
            var target = D(note.Day);
            var session = sessions
                .Where(x => x.Note is null && (note.PlanDay is null || x.DayPlan.DayOfWeek == note.PlanDay))
                .OrderBy(x => Math.Abs((x.CompletedAt - target).TotalHours))
                .FirstOrDefault();

            if (session is not null) session.Note = note.Text;
        }
    }

    // ---------------------------------------------------------------- mjesečni napredak

    private void SeedProgressEntries()
    {
        // client: prva tri mjeseca kod Lejle (hibrid), zadnja tri kod Harisa (utezi).
        AddProgress("client", ClientProgressSeeds,
            month => month < 3 ? _plans["client_lejla"] : _plans["client_mentor"]);

        // mobile: prvi mjesec bez plana (saradnja sa Selmom je otkazana), zatim plan kod Amira.
        AddProgress("mobile", MobileProgressSeeds,
            month => month == 0 ? null : _plans["mobile_amir"]);
    }

    /// <summary>Unosi za zadnjih N mjeseci; zadnji unos je za tekući mjesec.</summary>
    private void AddProgress(string username, ProgressSeed[] entries, Func<int, TrainingPlan?> planForMonth)
    {
        var client = _clients[username];
        var currentMonth = new DateTime(_today.Year, _today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        for (var i = 0; i < entries.Length; i++)
        {
            var monthStart = currentMonth.AddMonths(i - (entries.Length - 1));
            var createdAt = monthStart.AddDays(23 + i % 3).AddHours(19).AddMinutes(10 * i);
            if (createdAt > _now) createdAt = Max(monthStart, _now.AddMinutes(-30));

            var plan = planForMonth(i);
            var entry = entries[i];
            db.ProgressEntries.Add(new ProgressEntry
            {
                ClientProfile = client,
                Year = monthStart.Year,
                Month = monthStart.Month,
                PhotoUrl = $"/seed/progress/{username}-{i + 1}.png",
                WeightKg = entry.WeightKg,
                Measurements = entry.Measurements,
                Strength = entry.Strength,
                Conditioning = entry.Conditioning,
                TrainingPlanId = plan?.Id,
                PlanSnapshotJson = plan is null ? null : PlanMapper.ToSnapshotJson(plan),
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            });
        }
    }

    // ---------------------------------------------------------------- aktivnost (heartbeat)

    private void SeedUserActivity()
    {
        foreach (var user in _users.Values)
        {
            if (ActivityPatternFor(user) is not { } pattern) continue;

            UserActivity? last = null;
            for (var offset = 1 - ActivityDays; offset <= 0; offset++)
            {
                if (!IsActiveDay(pattern, offset) || CreateActivity(user, offset, pattern) is not { } activity) continue;
                db.UserActivities.Add(activity);
                last = activity;
            }

            if (last is not null) user.LastLoginAt = last.LastHeartbeatAt.AddSeconds(-last.ActiveSeconds);
        }
    }

    private static ActivityPattern? ActivityPatternFor(User user) => user switch
    {
        _ when !CanLogIn(user) => null,
        { Role: UserRole.Admin } => new ActivityPattern(0.9, 20, 60) { WeekdaysOnly = true },
        { Username: "mentor" } => new ActivityPattern(0.97, 45, 120),
        { Role: UserRole.Mentor } => new ActivityPattern(0.8, 30, 120),
        { Username: "client" or "mobile" } => new ActivityPattern(0.93, 10, 45) { ActiveToday = true },
        // Neaktivna klijentica, da job za neaktivnost ima koga obavijestiti.
        { Username: "belma.causevic" } => new ActivityPattern(0.5, 5, 40) { LastActiveDay = -20 },
        _ => new ActivityPattern(0.5, 5, 40)
    };

    private bool IsActiveDay(ActivityPattern pattern, int offset)
    {
        var day = _today.AddDays(offset);
        if (pattern.WeekdaysOnly && day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;

        if (pattern.LastActiveDay is { } lastActiveDay)
        {
            if (offset > lastActiveDay) return false;
            if (offset == lastActiveDay) return true;
        }

        if (pattern.ActiveToday && offset == 0) return true;
        return _random.NextDouble() < pattern.DailyChance;
    }

    private UserActivity? CreateActivity(User user, int offset, ActivityPattern pattern)
    {
        var dayStart = _today.AddDays(offset);
        var seconds = _random.Next(pattern.MinMinutes, pattern.MaxMinutes + 1) * 60 + _random.Next(0, 60);
        DateTime lastHeartbeat;

        if (offset < 0)
        {
            lastHeartbeat = dayStart.AddHours(_random.Next(10, 23)).AddMinutes(_random.Next(0, 60));
        }
        else
        {
            // Danas: ne više od pola vremena od ponoći i zadnji heartbeat prije "sada".
            var elapsedSeconds = (int)(_now - dayStart).TotalSeconds;
            if (elapsedSeconds < 600) return null;
            seconds = Math.Min(seconds, elapsedSeconds / 2);
            lastHeartbeat = _now.AddSeconds(-Math.Min(elapsedSeconds / 4, _random.Next(120, 1200)));
        }

        return new UserActivity
        {
            User = user,
            Day = DateOnly.FromDateTime(dayStart),
            ActiveSeconds = seconds,
            LastHeartbeatAt = lastHeartbeat
        };
    }

    /// <summary>Blokirani i obrisani korisnici te mentori koji nisu odobreni ne mogu se prijaviti.</summary>
    private static bool CanLogIn(User user) =>
        user is { IsActive: true, IsDeleted: false } &&
        (user.Role != UserRole.Mentor || user.MentorProfile?.Status == MentorApprovalStatus.Approved);

    // ---------------------------------------------------------------- pomoćne metode

    /// <summary>Trenutak x dana od sada (negativno = prošlost).</summary>
    private DateTime D(double days) => _now.AddDays(days);

    /// <summary>Tačno vrijeme (UTC) na dan x dana od danas; nikad u budućnosti.</summary>
    private DateTime DayAt(int days, int hour, int minute) =>
        NotFuture(_today.AddDays(days).AddHours(hour).AddMinutes(minute));

    private DateTime NotFuture(DateTime value) => value <= _now ? value : _now.AddMinutes(-1);

    private static DateTime Max(DateTime first, DateTime second) => first > second ? first : second;

    /// <summary>Jednoobrazni prelomi reda bez obzira na line endings izvornog fajla.</summary>
    private static string Normalize(string text) => text.ReplaceLineEndings("\n").Trim();

    private int Tracked<T>() where T : class => db.ChangeTracker.Entries<T>().Count();

    private void LogSummary() =>
        logger.LogInformation(
            "Demo podaci su dodani: {Users} korisnika, {Subscriptions} pretplata, {Payments} uplata, {Plans} planova, " +
            "{Sessions} treninga, {Progress} unosa napretka, {Messages} poruka, {Notifications} obavijesti, {Activities} dana aktivnosti.",
            Tracked<User>(), Tracked<Subscription>(), Tracked<Payment>(), Tracked<TrainingPlan>(),
            Tracked<TrainingSession>(), Tracked<ProgressEntry>(), Tracked<Message>(), Tracked<Notification>(),
            Tracked<UserActivity>());

    // ---------------------------------------------------------------- modeli seed podataka

    private sealed record MentorSeed
    {
        public required string Username { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string Gender { get; init; }
        public required int Age { get; init; }
        public required string PhoneNumber { get; init; }
        public required int CreatedDay { get; init; }
        public required string TrainingType { get; init; }
        public string? Nickname { get; init; }
        public required int YearsOfExperience { get; init; }
        public required decimal MonthlyPrice { get; init; }
        public required MentorApprovalStatus Status { get; init; }
        public int? ReviewedDay { get; init; }
        public string? RejectionReason { get; init; }
        public required string[] Specializations { get; init; }
        public required CertificateSeed[] Certificates { get; init; }
        public required string Bio { get; init; }
    }

    /// <param name="File">Fajl u /seed/certificates.</param>
    /// <param name="FileName">Originalni naziv koji je mentor uploadao.</param>
    private sealed record CertificateSeed(string File, string FileName);

    private sealed record ClientSeed
    {
        public required string Username { get; init; }
        public required string FirstName { get; init; }
        public required string LastName { get; init; }
        public required string Gender { get; init; }
        public required int Age { get; init; }
        public string? PhoneNumber { get; init; }
        public required int CreatedDay { get; init; }
        public required decimal WeightKg { get; init; }
        public required decimal HeightCm { get; init; }
        public required string FitnessLevel { get; init; }
        public required int TrainingExperienceYears { get; init; }
        public required string FitnessGoal { get; init; }
        public required string GoalDescription { get; init; }
        public string? PreferredTrainingType { get; init; }

        /// <summary>Postavljeno = korisnik je blokiran (IsActive = false) i zadnji put se prijavio tog dana.</summary>
        public int? BlockedLastLoginDay { get; init; }
    }

    private sealed record SubscriptionSeed
    {
        public required string Key { get; init; }
        public required string Client { get; init; }
        public required string Mentor { get; init; }
        public required SubscriptionStatus Status { get; init; }

        /// <summary>Dan kreiranja (i uplate, osim za PendingPayment).</summary>
        public required int CreatedDay { get; init; }

        /// <summary>Početak i kraj saradnje (dani od sada); null dok mentor ne prihvati.</summary>
        public (int Start, int End)? Period { get; init; }

        public int[] RenewalDays { get; init; } = [];
        public int? CancelledDay { get; init; }
        public int? RefundedDay { get; init; }
        public string? StatusReason { get; init; }
        public required QuestionnaireSeed Questionnaire { get; init; }
        public ReviewSeed? Review { get; init; }
    }

    private sealed record QuestionnaireSeed(
        string PrimaryGoal,
        string TimeCommitment,
        string HealthIssues,
        string Medications,
        string WeeklySessions,
        string OutsideActivity);

    private sealed record ReviewSeed(int Rating, int DaysAfterEnd, string Comment);

    /// <param name="TrainingDays">Dani plana (1–7) na koje klijent uglavnom trenira.</param>
    /// <param name="RestDayCount">Koliko od ukupnog broja otpada na dane odmora/aktivnog oporavka.</param>
    /// <param name="BaseRepetitions">Očekivani broj ponavljanja po danu plana (indeks 0 = ponedjeljak).</param>
    private sealed record SessionSeed(
        int FromDay,
        int ToDay,
        int[] TrainingDays,
        int Count,
        int RestDayCount,
        int[] BaseRepetitions,
        int Trend,
        SessionNote[] Notes);

    /// <param name="PlanDay">Dan plana na koji se bilješka odnosi (null = bilo koji).</param>
    private sealed record SessionNote(int Day, int? PlanDay, string Text);

    private sealed record ProgressSeed(decimal WeightKg, string Measurements, string Strength, string Conditioning);

    private sealed record ActivityPattern(double DailyChance, int MinMinutes, int MaxMinutes)
    {
        public bool WeekdaysOnly { get; init; }
        public bool ActiveToday { get; init; }
        public int? LastActiveDay { get; init; }
    }
}

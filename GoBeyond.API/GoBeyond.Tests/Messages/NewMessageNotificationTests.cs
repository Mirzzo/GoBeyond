using GoBeyond.Core.DTOs.Communication;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;
using GoBeyond.Infrastructure.Database;
using GoBeyond.Infrastructure.Services.Messages;
using GoBeyond.Infrastructure.Services.Notifications;
using GoBeyond.Tests.TestInfrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace GoBeyond.Tests.Messages;

/// <summary>
/// NewMessage obavijest: naslov bez padeža ("Nova poruka: {ime}"), jedna nepročitana obavijest po pošiljaocu (i kad je
/// postojeća u ranijem obliku), a otvaranje niti je označava pročitanom, pa broj nepročitanih obavijesti odgovara porukama.
/// Pošiljalac se prepoznaje po Id-u, pa se obavijesti dvije osobe sa istim imenom i prezimenom ne spajaju.
/// </summary>
public sealed class NewMessageNotificationTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly GoBeyondDbContext _db;
    private readonly MessageService _service;
    private readonly User _mentor;
    private readonly User _client;
    private readonly Subscription _subscription;

    public NewMessageNotificationTests()
    {
        _connection.Open();
        _db = SqliteTestDbContext.Create(_connection);
        _db.Database.EnsureCreated();
        _service = new MessageService(_db, new NotificationSender(_db));

        var gender = new Gender { Name = "Muško" };
        var type = new TrainingType { Name = "Weightlifting", Description = "Utezi" };
        var level = new FitnessLevel { Name = "Početnik", SortOrder = 1 };
        var goal = new FitnessGoal { Name = "Snaga" };

        _mentor = NewUser("Haris", "Mehmedović", "haris", UserRole.Mentor, gender);
        var mentorProfile = new MentorProfile
        {
            User = _mentor, TrainingType = type, Bio = new string('b', 60), YearsOfExperience = 5, MonthlyPrice = 20,
            Status = MentorApprovalStatus.Approved
        };
        _client = NewUser("Tarik", "Hodžić", "tarik", UserRole.Client, gender);
        var clientProfile = new ClientProfile
        {
            User = _client, WeightKg = 80, HeightCm = 180, FitnessLevel = level, FitnessGoal = goal, TrainingExperienceYears = 1
        };
        _subscription = new Subscription
        {
            ClientProfile = clientProfile, MentorProfile = mentorProfile, Status = SubscriptionStatus.Active, Price = 20,
            Currency = "usd", PaidAt = DateTime.UtcNow.AddDays(-5), AcceptedAt = DateTime.UtcNow.AddDays(-5),
            StartDate = DateTime.UtcNow.AddDays(-5), EndDate = DateTime.UtcNow.AddDays(25), CreatedAt = DateTime.UtcNow.AddDays(-5)
        };
        _db.AddRange(mentorProfile, clientProfile, _subscription);
        _db.SaveChanges();
    }

    public void Dispose() => _connection.Dispose();

    private static User NewUser(string first, string last, string username, UserRole role, Gender gender) => new()
    {
        FirstName = first, LastName = last, Username = username, Email = $"{username}@test.ba",
        DateOfBirth = new DateOnly(1990, 1, 1), Gender = gender, PasswordHash = "x", Role = role
    };

    private Task<MessageDto> MentorSendsAsync(string content) =>
        _service.SendAsync(_mentor.Id, UserRole.Mentor, _subscription.Id, new SendMessageRequest { Content = content });

    private void AddUnreadNotification(User recipient, string title, string body)
    {
        _db.Notifications.Add(new Notification
        {
            UserId = recipient.Id, Type = NotificationType.NewMessage, Title = title, Body = body,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });
        _db.SaveChanges();
    }

    private Task<List<Notification>> NotificationsOfAsync(User user) =>
        _db.Notifications.AsNoTracking().Where(x => x.UserId == user.Id).OrderBy(x => x.Id).ToListAsync();

    private Task<int> UnreadCountAsync(User user) => new NotificationService(_db).GetUnreadCountAsync(user.Id);

    /// <summary>Drugi klijent sa istim imenom i prezimenom kao prvi, sa aktivnom saradnjom kod istog mentora.</summary>
    private (User Client, Subscription Subscription) AddClientWithTheSameName()
    {
        var twin = new User
        {
            FirstName = _client.FirstName, LastName = _client.LastName, Username = "tarik.hodzic", Email = "tarik.hodzic@test.ba",
            DateOfBirth = new DateOnly(1994, 6, 1), GenderId = _client.GenderId, PasswordHash = "x", Role = UserRole.Client
        };
        var profile = new ClientProfile
        {
            User = twin, WeightKg = 75, HeightCm = 178, FitnessLevelId = _subscription.ClientProfile.FitnessLevelId,
            FitnessGoalId = _subscription.ClientProfile.FitnessGoalId, TrainingExperienceYears = 2
        };
        var subscription = new Subscription
        {
            ClientProfile = profile, MentorProfileId = _subscription.MentorProfileId, Status = SubscriptionStatus.Active, Price = 20,
            Currency = "usd", PaidAt = DateTime.UtcNow.AddDays(-3), AcceptedAt = DateTime.UtcNow.AddDays(-3),
            StartDate = DateTime.UtcNow.AddDays(-3), EndDate = DateTime.UtcNow.AddDays(27), CreatedAt = DateTime.UtcNow.AddDays(-3)
        };
        _db.AddRange(profile, subscription);
        _db.SaveChanges();
        return (twin, subscription);
    }

    private Task<MessageDto> ClientSendsAsync(User client, Subscription subscription, string content) =>
        _service.SendAsync(client.Id, UserRole.Client, subscription.Id, new SendMessageRequest { Content = content });

    [Fact]
    public async Task SendAsync_TitleNamesTheSenderWithoutDecliningTheName()
    {
        await MentorSendsAsync("Pozdrav, plan stiže danas.");

        var notification = Assert.Single(await NotificationsOfAsync(_client));
        Assert.Equal(NotificationType.NewMessage, notification.Type);
        Assert.Equal("Nova poruka: Haris Mehmedović", notification.Title);
        Assert.Equal("Pozdrav, plan stiže danas.", notification.Body);
        Assert.Equal(_mentor.Id, notification.SenderUserId);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public async Task SendAsync_TwiceFromTheSameSender_UpdatesTheOneUnreadNotification()
    {
        await MentorSendsAsync("Prva poruka");
        await MentorSendsAsync("Druga poruka");

        var notification = Assert.Single(await NotificationsOfAsync(_client));
        Assert.Equal("Nova poruka: Haris Mehmedović", notification.Title);
        Assert.Equal("Druga poruka", notification.Body);
    }

    [Theory]
    [InlineData("Nova poruka: Haris Mehmedović", "Stara poruka")] // bez Id-a pošiljaoca (nastala prije te kolone)
    [InlineData("Nova poruka od Haris Mehmedović", "Stara poruka")]
    [InlineData("Nova poruka", "Haris Mehmedović: Stara poruka")]
    public async Task SendAsync_WithAnUnreadNotificationInAnEarlierFormat_UpdatesItInsteadOfAddingAnother(string title, string body)
    {
        AddUnreadNotification(_client, title, body);

        await MentorSendsAsync("Nova poruka o planu");

        var notification = Assert.Single(await NotificationsOfAsync(_client));
        Assert.False(notification.IsRead);
        Assert.Equal("Nova poruka: Haris Mehmedović", notification.Title);
        Assert.Equal("Nova poruka o planu", notification.Body);
        Assert.Equal(_mentor.Id, notification.SenderUserId);
    }

    [Fact]
    public async Task SendAsync_DoesNotReuseAnotherSendersEarlierFormatNotification()
    {
        AddUnreadNotification(_client, "Nova poruka", "Lejla Mujić: Poruka druge mentorice");

        await MentorSendsAsync("Poruka od Harisa");

        var notifications = await NotificationsOfAsync(_client);
        Assert.Equal(2, notifications.Count);
        Assert.Equal("Lejla Mujić: Poruka druge mentorice", notifications[0].Body);
        Assert.Equal("Nova poruka: Haris Mehmedović", notifications[1].Title);
    }

    [Fact]
    public async Task GetThreadAsync_MarksTheNewMessageNotificationFromTheOtherPartyRead()
    {
        await MentorSendsAsync("Prva poruka");
        await MentorSendsAsync("Druga poruka");
        Assert.Equal(1, await UnreadCountAsync(_client));

        await _service.GetThreadAsync(_client.Id, UserRole.Client, _subscription.Id);

        Assert.Equal(0, await UnreadCountAsync(_client));
        Assert.True(Assert.Single(await NotificationsOfAsync(_client)).IsRead);
    }

    [Fact]
    public async Task GetThreadAsync_ForMentor_MarksTheClientsNewMessageNotificationRead()
    {
        await _service.SendAsync(_client.Id, UserRole.Client, _subscription.Id, new SendMessageRequest { Content = "Imam pitanje." });
        Assert.Equal(1, await UnreadCountAsync(_mentor));

        await _service.GetThreadAsync(_mentor.Id, UserRole.Mentor, _subscription.Id);

        Assert.Equal(0, await UnreadCountAsync(_mentor));
    }

    [Theory]
    [InlineData("Nova poruka: Haris Mehmedović", "Stara poruka")] // bez Id-a pošiljaoca (nastala prije te kolone)
    [InlineData("Nova poruka od Haris Mehmedović", "Stara poruka")]
    [InlineData("Nova poruka", "Haris Mehmedović: Stara poruka")]
    public async Task GetThreadAsync_MarksAnEarlierFormatNotificationFromTheOtherPartyRead(string title, string body)
    {
        AddUnreadNotification(_client, title, body);

        await _service.GetThreadAsync(_client.Id, UserRole.Client, _subscription.Id);

        Assert.Equal(0, await UnreadCountAsync(_client));
    }

    [Fact]
    public async Task GetThreadAsync_LeavesOtherNotificationsUnread()
    {
        await MentorSendsAsync("Poruka od Harisa");
        await _service.SendAsync(_client.Id, UserRole.Client, _subscription.Id, new SendMessageRequest { Content = "Odgovor" });
        AddUnreadNotification(_client, "Nova poruka: Lejla Mujić", "Poruka druge mentorice");
        _db.Notifications.Add(new Notification
        {
            UserId = _client.Id, Type = NotificationType.PlanPublished, Title = "Trening plan je objavljen", Body = "Plan je spreman."
        });
        await _db.SaveChangesAsync();

        await _service.GetThreadAsync(_client.Id, UserRole.Client, _subscription.Id);

        var unread = (await NotificationsOfAsync(_client)).Where(x => !x.IsRead).Select(x => x.Title).ToList();
        Assert.Equal(["Nova poruka: Lejla Mujić", "Trening plan je objavljen"], unread);
        Assert.Equal(1, await UnreadCountAsync(_mentor));
    }

    [Fact]
    public async Task GetThreadAsync_WhileAnotherThreadWithTheSameSenderHasUnreadMessages_KeepsTheNotificationUnread()
    {
        // Ranija (istekla) saradnja sa istim mentorom ima nepročitanu poruku; nova poruka stiže u aktivnu nit.
        var expired = new Subscription
        {
            ClientProfileId = _subscription.ClientProfileId, MentorProfileId = _subscription.MentorProfileId,
            Status = SubscriptionStatus.Expired, Price = 20, Currency = "usd", PaidAt = DateTime.UtcNow.AddDays(-60),
            AcceptedAt = DateTime.UtcNow.AddDays(-60), EndDate = DateTime.UtcNow.AddDays(-30), CreatedAt = DateTime.UtcNow.AddDays(-60)
        };
        _db.Subscriptions.Add(expired);
        _db.Messages.Add(new Message
        {
            Subscription = expired, SenderUserId = _mentor.Id, Content = "Stara poruka", SentAt = DateTime.UtcNow.AddDays(-31)
        });
        await _db.SaveChangesAsync();
        await MentorSendsAsync("Nova poruka o planu");

        await _service.GetThreadAsync(_client.Id, UserRole.Client, expired.Id);
        Assert.Equal(1, await UnreadCountAsync(_client));

        await _service.GetThreadAsync(_client.Id, UserRole.Client, _subscription.Id);
        Assert.Equal(0, await UnreadCountAsync(_client));
    }

    [Fact]
    public async Task SendAsync_FromTwoClientsWithTheSameName_KeepsOneNotificationPerSender()
    {
        var (twin, twinSubscription) = AddClientWithTheSameName();

        await ClientSendsAsync(twin, twinSubscription, "Poruka druge osobe istog imena.");
        await ClientSendsAsync(_client, _subscription, "Poruka prve osobe.");

        var notifications = await NotificationsOfAsync(_mentor);
        Assert.Equal([((int?)twin.Id, "Poruka druge osobe istog imena."), (_client.Id, "Poruka prve osobe.")],
            notifications.Select(x => (x.SenderUserId, x.Body)));
        Assert.All(notifications, x => Assert.Equal(("Nova poruka: Tarik Hodžić", false), (x.Title, x.IsRead)));
    }

    [Fact]
    public async Task GetThreadAsync_LeavesTheNotificationOfAnotherSenderWithTheSameNameUnread()
    {
        var (twin, twinSubscription) = AddClientWithTheSameName();
        await ClientSendsAsync(twin, twinSubscription, "Poruka druge osobe istog imena.");
        await ClientSendsAsync(_client, _subscription, "Poruka prve osobe.");
        Assert.Equal(2, await UnreadCountAsync(_mentor));

        await _service.GetThreadAsync(_mentor.Id, UserRole.Mentor, _subscription.Id);

        var unread = Assert.Single(await NotificationsOfAsync(_mentor), x => !x.IsRead);
        Assert.Equal(((int?)twin.Id, "Poruka druge osobe istog imena."), (unread.SenderUserId, unread.Body));
        Assert.Equal(1, await UnreadCountAsync(_mentor));

        await _service.GetThreadAsync(_mentor.Id, UserRole.Mentor, twinSubscription.Id);
        Assert.Equal(0, await UnreadCountAsync(_mentor));
    }
}

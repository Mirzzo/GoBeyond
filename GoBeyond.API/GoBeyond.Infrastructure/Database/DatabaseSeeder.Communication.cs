using System.Globalization;
using GoBeyond.Core.Entities;
using GoBeyond.Core.Enums;

namespace GoBeyond.Infrastructure.Database;

// Seed podaci: poruke mentor ↔ klijent, obavijesti i sistemske objave.
public sealed partial class DatabaseSeeder
{
    // ---------------------------------------------------------------- poruke

    private void SeedMessages()
    {
        var haris = _users["mentor"];
        var tarik = _users["client"];
        var amir = _users["amir.salihovic"];
        var amina = _users["mobile"];
        var lejla = _users["lejla.mujic"];
        var emir = _users["emir.basic"];

        var tarikHaris = _subscriptions["client_mentor"];
        AddMessage(tarikHaris, haris, DayAt(-20, 17, 40),
            "Tarik, pogledao sam tvoj zadnji unos napretka – struk je opet manji, a čučanj ide gore. Odličan posao! " +
            "Od sljedeće sedmice čučanj radimo 4×6 sa 105 kg.");
        AddMessage(tarikHaris, tarik, DayAt(-20, 19, 5),
            "Hvala, Haris! Osjećam se jače nego ikad. Jedino me desno koljeno malo steže na zadnjoj seriji čučnja. " +
            "Trebam li se zbog toga brinuti?");
        AddMessage(tarikHaris, haris, DayAt(-19, 8, 30),
            "Dobro je da si javio. Ove sedmice spusti čučanj na 100 kg i na zagrijavanju dodaj elastičnu traku iznad " +
            "koljena: bočni hod 2×15 i čučanj s trakom 2×15. Snimi mi jednu seriju sa strane da pogledam tehniku.");
        AddMessage(tarikHaris, tarik, DayAt(-17, 20, 45),
            "Snimio sam seriju. Primijetio sam da mi koljena bježe prema unutra na zadnja dva ponavljanja. " +
            "S trakom na zagrijavanju je već bolje.");
        AddMessage(tarikHaris, haris, DayAt(-16, 9, 10),
            "Tačno to sam i ja vidio. Nastavi s trakom i u prvoj seriji napravi pauzu od 2 sekunde na dnu. " +
            "Ako do petka ne bude stezanja, vraćamo se na 105 kg.");
        AddMessage(tarikHaris, tarik, DayAt(-10, 18, 20),
            "Koljeno je potpuno u redu, 105 kg 4×6 je prošlo bez problema! Još nešto – subotom se pred trening " +
            "osjećam prazno. Da povećam kalorije?");
        AddMessage(tarikHaris, haris, DayAt(-10, 21, 0),
            "Bravo! Ažurirao sam plan (verzija 2): subota sada ima 40 g ugljikohidrata više i dodatni obrok prije " +
            "treninga, a zagrijavanje s trakom je stalni dio utorka. Javi kako ti sjeda.");
        var tarikLast = AddMessage(tarikHaris, tarik, DayAt(-3, 19, 30),
            "Subota je sad top, energije ima na pretek. Ove sedmice sam izvukao i mrtvo dizanje 150 kg × 3 – novi rekord! 🙌");
        var harisLast = AddMessage(tarikHaris, haris, DayAt(-1, 20, 15),
            "Svaka čast, Tarik! 150 kg je ozbiljna brojka. Sljedeća sedmica je lakša (deload): iste vježbe, ali sa 60% " +
            "težine i serijom manje. Nakon toga krećemo u novi blok. Samo nastavi ovako! 💪",
            isRead: false);
        NotifyNewMessage(haris, tarikLast, isRead: true);
        NotifyNewMessage(tarik, harisLast, isRead: false);

        var aminaAmir = _subscriptions["mobile_amir"];
        AddMessage(aminaAmir, amina, DayAt(-12, 19, 10),
            "Amire, uradila sam 10 punih sklekova s poda zaredom, bez koljena! Ne mogu vjerovati 😄");
        AddMessage(aminaAmir, amir, DayAt(-12, 20, 0),
            "Bravo, Amina! To je ogroman napredak – prije četiri mjeseca radila si sklekove s klupe. " +
            "Od petka zgib uz gumu radimo s tanjom gumom.");
        AddMessage(aminaAmir, amina, DayAt(-6, 13, 25),
            "Imam pitanje za ishranu: ovog vikenda idem na svadbu. Kako da se uklopim u plan?");
        AddMessage(aminaAmir, amir, DayAt(-6, 14, 5),
            "Uživaj bez grižnje savjesti! Taj dan pojedi lagan doručak bogat proteinima, pij dosta vode i ne preskači " +
            "subotnju šetnju. Jedan dan ne kvari rezultat – bitno je šta radiš većinu sedmice.");
        var amirLast = AddMessage(aminaAmir, amir, DayAt(-2, 18, 30),
            "Vidio sam tvoju bilješku s treninga – prvi puni zgib! 🎉 Od ove sedmice svaki trening počinješ s 5×1 punim " +
            "zgibom. Ne zaboravi krajem mjeseca unijeti napredak i novu fotografiju.",
            isRead: false);
        NotifyNewMessage(amina, amirLast, isRead: false);

        // Emir je pisao odmah nakon uplate, Haris još nije odgovorio.
        var emirHaris = _subscriptions["emir_mentor"];
        var emirMessage = AddMessage(emirHaris, emir, (emirHaris.PaidAt ?? emirHaris.CreatedAt).AddMinutes(40),
            "Pozdrav Haris, upravo sam uplatio pretplatu. Prije sam radio kalisteniku kod kuće s Amirom i skinuo 4 kg, " +
            "a sada bih želio preći u teretanu. Imam povišen pritisak (pod terapijom), pa bih volio da krenemo postepeno. " +
            "Hvala unaprijed!",
            isRead: false);
        NotifyNewMessage(haris, emirMessage, isRead: false);

        var tarikLejla = _subscriptions["client_lejla"];
        AddMessage(tarikLejla, lejla, DayAt(-182, 9, 15),
            "Tarik, dobro došao! Plan za prvu sedmicu je objavljen. S trčanjem kreni polako – tempo treba biti takav " +
            "da možeš pričati. Javi se ako nešto nije jasno.");
        AddMessage(tarikLejla, tarik, DayAt(-182, 20, 40),
            "Hvala, Lejla! Sve je jasno. Samo me zanima mogu li intervale raditi na traci u teretani?");
        AddMessage(tarikLejla, lejla, DayAt(-181, 8, 5),
            "Naravno! Na traci stavi nagib od 1% da bude sličnije trčanju napolju. Sretno na prvom treningu! ⚡");
    }

    private Message AddMessage(Subscription subscription, User sender, DateTime sentAt, string content, bool isRead = true)
    {
        var message = new Message
        {
            Subscription = subscription,
            SenderUserId = sender.Id,
            SenderUser = sender,
            Content = content,
            SentAt = sentAt,
            IsRead = isRead
        };
        db.Messages.Add(message);
        return message;
    }

    private void NotifyNewMessage(User recipient, Message message, bool isRead)
    {
        var preview = message.Content.Length <= 120 ? message.Content : message.Content[..117].TrimEnd() + "...";
        Notify(recipient, NotificationType.NewMessage, "Nova poruka",
            $"{message.SenderUser.FullName}: {preview}", message.SentAt, isRead);
    }

    // ---------------------------------------------------------------- obavijesti

    private void SeedNotifications()
    {
        var haris = _users["mentor"];
        Notify(haris, NotificationType.MentorApproved, "Mentorski nalog je odobren",
            "Čestitamo! Administrator je odobrio vaš mentorski nalog. Vaš profil je sada vidljiv klijentima " +
            "i možete primati zahtjeve za saradnju.",
            _mentors["mentor"].ReviewedAt ?? haris.CreatedAt, isRead: true);
        NotifyCollaborationRequest(_subscriptions["emir_mentor"]);
        NotifyCollaborationRequest(_subscriptions["sara_mentor"]);

        // Tarik (client): aktivna saradnja s Harisom.
        var tarikHaris = _subscriptions["client_mentor"];
        var tarikPlan = _plans["client_mentor"];
        NotifyRequestAccepted(tarikHaris, isRead: true);
        NotifyPlanPublished(tarikPlan, isRead: true);
        NotifyPayment(tarikHaris, isRead: true);
        Notify(_users["client"], NotificationType.PlanUpdated, "Trening plan je ažuriran",
            $"Haris Mehmedović je ažurirao vaš trening plan (verzija {tarikPlan.Version}). " +
            "Pogledajte izmjene za subotu i novo zagrijavanje prije čučnja.",
            tarikPlan.UpdatedAt, isRead: false);

        // Amina (mobile): aktivna saradnja s Amirom.
        var aminaAmir = _subscriptions["mobile_amir"];
        NotifyRequestAccepted(aminaAmir, isRead: true);
        NotifyPlanPublished(_plans["mobile_amir"], isRead: true);
        NotifyPayment(aminaAmir, isRead: false);

        var harunDino = _subscriptions["harun_dino"];
        var refund = harunDino.Payments.First();
        Notify(_users["harun.begovic"], NotificationType.RequestRejected, "Zahtjev za saradnju je odbijen",
            $"Dino Kurtović je odbio vaš zahtjev za saradnju. Razlog: {harunDino.StatusReason} " +
            $"Uplata od {Money(refund.Amount)} vraćena je na vašu karticu.",
            refund.RefundedAt ?? harunDino.CreatedAt, isRead: false);

        NotifyRequestAccepted(_subscriptions["adna_mentor"], isRead: false);
        NotifyPlanPublished(_plans["lamija_lejla"], isRead: true);
    }

    private void NotifyCollaborationRequest(Subscription subscription)
    {
        var client = subscription.ClientProfile.User;
        Notify(subscription.MentorProfile.User, NotificationType.NewCollaborationRequest, "Novi zahtjev za saradnju",
            $"{client.FullName} vam je {ByGender(client, "poslao", "poslala")} zahtjev za saradnju i " +
            $"{ByGender(client, "uplatio", "uplatila")} pretplatu od {Money(subscription.Price)}. " +
            "Pregledajte upitnik i prihvatite ili odbijte zahtjev.",
            subscription.PaidAt ?? subscription.CreatedAt, isRead: false);
    }

    private void NotifyRequestAccepted(Subscription subscription, bool isRead)
    {
        var mentor = subscription.MentorProfile.User;
        var acceptedAt = subscription.AcceptedAt ?? subscription.CreatedAt;
        Notify(subscription.ClientProfile.User, NotificationType.RequestAccepted, "Zahtjev za saradnju je prihvaćen",
            $"{mentor.FullName} je {ByGender(mentor, "prihvatio", "prihvatila")} vaš zahtjev za saradnju. " +
            $"Pretplata je aktivna do {FormatDate(acceptedAt.AddDays(30))}, a trening plan stiže uskoro.",
            acceptedAt, isRead);
    }

    private void NotifyPlanPublished(TrainingPlan plan, bool isRead)
    {
        var mentor = plan.MentorProfile.User;
        Notify(plan.ClientProfile.User, NotificationType.PlanPublished, "Trening plan je objavljen",
            $"{mentor.FullName} je {ByGender(mentor, "objavio", "objavila")} vaš trening plan. " +
            "Otvorite sekciju Plan i pogledajte raspored treninga i ishrane za sedmicu.",
            plan.PublishedAt ?? plan.CreatedAt, isRead);
    }

    /// <summary>Obavijest o zadnjoj (najnovijoj) uspješnoj uplati pretplate.</summary>
    private void NotifyPayment(Subscription subscription, bool isRead)
    {
        var payment = subscription.Payments.OrderBy(x => x.PaidAt).Last();
        Notify(subscription.ClientProfile.User, NotificationType.PaymentSucceeded, "Uplata je uspješna",
            $"Uplata od {Money(payment.Amount)} za produženje saradnje s mentorom {subscription.MentorProfile.User.FullName} " +
            $"je uspješno obrađena. Pretplata traje do {FormatDate(subscription.EndDate ?? subscription.CreatedAt)}",
            payment.PaidAt ?? payment.CreatedAt, isRead);
    }

    private void Notify(User user, NotificationType type, string title, string body, DateTime createdAt, bool isRead,
        Announcement? announcement = null)
    {
        db.Notifications.Add(new Notification
        {
            UserId = user.Id,
            Title = title,
            Body = body,
            Type = type,
            IsRead = isRead,
            CreatedAt = createdAt,
            Announcement = announcement
        });
    }

    // ---------------------------------------------------------------- sistemske objave

    private void SeedAnnouncements()
    {
        var admin = _users["admin"];
        var recipients = _users.Values.Where(CanLogIn).ToList();

        var welcome = AddAnnouncement(admin, "Dobrodošli na GoBeyond",
            "GoBeyond vas povezuje s provjerenim mentorima za trening s utezima, kalisteniku i hibridni trening. " +
            "Pratite svoj sedmični plan, bilježite napredak svakog mjeseca i razgovarajte s mentorom direktno u aplikaciji. " +
            "Želimo vam puno uspjeha na putu ka vašim ciljevima!",
            targetRole: null, D(-30));
        for (var i = 0; i < recipients.Count; i++)
        {
            NotifyAnnouncement(recipients[i], welcome, isRead: i % 2 == 0);
        }

        var mentorTools = AddAnnouncement(admin, "Novi alati za mentore",
            "Od danas možete razmjenjivati poruke s klijentima direktno u aplikaciji, za sve aktivne saradnje i zahtjeve " +
            "koji čekaju vaš odgovor. Svaka izmjena objavljenog plana sada dobija novu verziju, a klijent automatski " +
            "dobija obavijest o ažuriranju. Uz mjesečni napredak klijenti čuvaju i snimak plana koji su tada koristili, " +
            "pa lakše pratite šta daje rezultate.",
            UserRole.Mentor, D(-7));
        foreach (var mentor in recipients.Where(x => x.Role == UserRole.Mentor))
        {
            NotifyAnnouncement(mentor, mentorTools, isRead: false);
        }
    }

    private Announcement AddAnnouncement(User author, string title, string content, UserRole? targetRole, DateTime createdAt)
    {
        var announcement = new Announcement
        {
            CreatedByUserId = author.Id,
            Title = title,
            Content = content,
            TargetRole = targetRole,
            CreatedAt = createdAt
        };
        db.Announcements.Add(announcement);
        return announcement;
    }

    private void NotifyAnnouncement(User user, Announcement announcement, bool isRead) =>
        Notify(user, NotificationType.Announcement, announcement.Title, announcement.Content,
            announcement.CreatedAt, isRead, announcement);

    // ---------------------------------------------------------------- formatiranje

    private string Money(decimal amount) =>
        $"{amount.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',')} {_currency.ToUpperInvariant()}";

    private static string FormatDate(DateTime value) => value.ToString("dd.MM.yyyy.", CultureInfo.InvariantCulture);

    private string ByGender(User user, string male, string female) =>
        user.GenderId == IdOf(_genders, GenderFemale) ? female : male;
}

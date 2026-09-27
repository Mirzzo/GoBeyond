using GoBeyond.Core.Enums;
using GoBeyond.Infrastructure.Common;

namespace GoBeyond.Infrastructure.Database;

// Seed podaci: šifarnici, korisnici, pretplate (upitnici i recenzije), odrađeni treninzi i napredak.
public sealed partial class DatabaseSeeder
{
    private const string TypeWeightlifting = "Weightlifting";
    private const string TypeCalisthenics = "Calisthenics";
    private const string TypeHybrid = "Hybrid";

    private const string GoalWeightLoss = "Mršavljenje";
    private const string GoalMuscleGain = "Povećanje mišićne mase";
    private const string GoalStrength = "Snaga";
    private const string GoalEndurance = "Izdržljivost i kondicija";
    private const string GoalCompetition = "Priprema za takmičenje";
    private const string GoalHealth = "Opće zdravlje i pokretljivost";

    private const string LevelBeginner = "Početnik";
    private const string LevelRecreational = "Rekreativac";
    private const string LevelIntermediate = "Srednji nivo";
    private const string LevelAdvanced = "Napredni";

    private const string GenderMale = "Muško";
    private const string GenderFemale = "Žensko";

    // ---------------------------------------------------------------- šifarnici

    private static readonly (string Name, string Description)[] TrainingTypeSeeds =
    [
        (TypeWeightlifting,
            "Trening s utezima usmjeren na razvoj snage i mišićne mase. Temelji se na osnovnim višezglobnim vježbama " +
            "poput čučnja, mrtvog dizanja i bench pressa, uz olimpijska dizanja i postepeno povećanje opterećenja."),
        (TypeCalisthenics,
            "Trening s vlastitom težinom tijela koji razvija snagu, kontrolu i pokretljivost. Uključuje zgibove, sklekove, " +
            "propadanja i muscle-up, kao i statičke elemente poput L-sita, front levera i stoja na rukama."),
        (TypeHybrid,
            "Kombinacija treninga snage i izdržljivosti u jednom programu. Spaja rad s utezima, trčanje te kružni i " +
            "funkcionalni trening za svestranu kondiciju u teretani i van nje.")
    ];

    private static readonly (string Name, string Description)[] FitnessGoalSeeds =
    [
        (GoalWeightLoss, "Smanjenje tjelesne masti kroz umjeren kalorijski deficit, redovan trening i zdrave prehrambene navike."),
        (GoalMuscleGain, "Izgradnja mišića kroz progresivan trening s opterećenjem i dovoljan unos proteina i kalorija."),
        (GoalStrength, "Veća maksimalna snaga u osnovnim pokretima kao što su čučanj, mrtvo dizanje, bench press i zgib."),
        (GoalEndurance, "Bolja aerobna i anaerobna kondicija, brži oporavak i više energije tokom cijelog dana."),
        (GoalCompetition, "Ciljana priprema za takmičenje u powerliftingu, kalistenici, trčanju ili hibridnim utrkama."),
        (GoalHealth, "Bolje držanje, pokretljivi zglobovi i život bez bolova uz redovnu i umjerenu fizičku aktivnost.")
    ];

    private static readonly (string Name, string Description, int SortOrder)[] FitnessLevelSeeds =
    [
        (LevelBeginner, "Bez iskustva ili s vrlo malo iskustva u redovnom treningu.", 1),
        (LevelRecreational, "Povremeno trenira i poznaje osnovne vježbe, ali bez strukturiranog plana.", 2),
        (LevelIntermediate, "Redovno trenira duže od godinu dana i dobro poznaje tehniku osnovnih vježbi.", 3),
        (LevelAdvanced, "Višegodišnji strukturiran trening i visok nivo snage i kondicije.", 4)
    ];

    private static readonly string[] GenderSeeds = [GenderMale, GenderFemale];

    // ---------------------------------------------------------------- mentori

    private static readonly MentorSeed[] MentorSeeds =
    [
        new()
        {
            Username = "mentor", FirstName = "Haris", LastName = "Mehmedović", Gender = GenderMale, Age = 34,
            PhoneNumber = "+387 61 234 567", CreatedDay = -390,
            TrainingType = TypeWeightlifting, Nickname = "Čelik", YearsOfExperience = 9, MonthlyPrice = 39.99m,
            Status = MentorApprovalStatus.Approved, ReviewedDay = -388,
            Specializations = [GoalMuscleGain, GoalStrength, GoalCompetition],
            Certificates = [new("mentor-certifikat.pdf", "certifikat-trener-snage.pdf")],
            Bio = """
                Zdravo! Ja sam Haris, trener snage iz Sarajeva, i već devet godina pomažem ljudima da postanu jači – u teretani i van nje.

                Moja priča počela je sasvim obično: kao šesnaestogodišnjak bio sam mršav, nesiguran i stalno sam preskakao treninge. Sve se promijenilo kad sam prvi put pravilno naučio čučanj i mrtvo dizanje. Shvatio sam da snaga nije samo broj na šipki, nego osjećaj da možeš više nego što si mislio.

                Danas radim s početnicima, rekreativcima i takmičarima u powerliftingu. Svaki plan pravim od nule, prema tvom iskustvu, rasporedu i ciljevima. Fokus mi je na tehnici, postepenoj progresiji i ishrani koja se uklapa u stvarni život – bez gladovanja i bez čudotvornih suplemenata.

                Šta dobijaš: sedmični plan treninga i ishrane, mjesečno praćenje napretka i odgovor na poruke u roku od 24 sata.

                Ako si spreman raditi, ja sam spreman voditi te korak po korak.

                – Haris 💪
                """
        },
        new()
        {
            Username = "dino.kurtovic", FirstName = "Dino", LastName = "Kurtović", Gender = GenderMale, Age = 31,
            PhoneNumber = "+387 62 118 904", CreatedDay = -330,
            TrainingType = TypeWeightlifting, Nickname = "Tenk", YearsOfExperience = 6, MonthlyPrice = 29.99m,
            Status = MentorApprovalStatus.Approved, ReviewedDay = -328,
            Specializations = [GoalStrength, GoalWeightLoss],
            Certificates = [new("dino-certifikat.pdf", "certifikat-personalni-trener.pdf")],
            Bio = """
                Pozdrav, ja sam Dino. Prije osam godina imao sam 118 kilograma, loše navike i nimalo samopouzdanja. Teretana mi je bila zadnje mjesto na koje sam želio ići.

                Prvi trener kojeg sam upoznao nije mi dao dijetu iz časopisa. Naučio me kako da jedem normalnu hranu u pravim količinama i kako da dižem teško, ali pametno. Za godinu i po skinuo sam 30 kilograma, a u mrtvom dizanju sam prešao 200 kg. Tada sam odlučio da želim to isto raditi za druge.

                Danas imam šest godina iskustva kao personalni trener i posebno volim raditi s ljudima koji žele smršati, a da pritom ne izgube snagu. Treninzi su kratki, jasni i zasnovani na osnovnim vježbama s utezima. Ishranu prilagođavam tvom poslu, porodici i ukusu – bez zabrana, uz dosta proteina i povrća.

                Ako ti treba neko ko će biti iskren, strpljiv i uporan zajedno s tobom, tu sam.

                – Dino, AKA Tenk
                """
        },
        new()
        {
            Username = "amir.salihovic", FirstName = "Amir", LastName = "Salihović", Gender = GenderMale, Age = 29,
            PhoneNumber = "+387 61 772 310", CreatedDay = -360,
            TrainingType = TypeCalisthenics, Nickname = "Bar Master", YearsOfExperience = 7, MonthlyPrice = 24.99m,
            Status = MentorApprovalStatus.Approved, ReviewedDay = -358,
            Specializations = [GoalWeightLoss, GoalEndurance, GoalHealth],
            Certificates = [new("amir-certifikat.pdf", "certifikat-kalistenika.pdf")],
            Bio = """
                Ćao! Ja sam Amir i već sedam godina živim kalisteniku. Sve je počelo na staroj šipki iza moje zgrade na Alipašinom Polju, gdje nisam mogao uraditi nijedan zgib. Danas radim muscle-up, front lever i stoj na rukama, ali još uvijek najviše volim trenutak kad klijent uradi svoj prvi zgib.

                Kalistenika je za mene sloboda: ne treba ti teretana, skupa oprema ni puno vremena. Dovoljni su šipka, pod i malo discipline. Radim s ljudima koji žele smršati, popraviti kondiciju ili se jednostavno bolje osjećati u svom tijelu.

                Moji planovi su postepeni – od sklekova s klupe i australijskih zgibova do naprednih elemenata. Svaki trening ima jasno zagrijavanje, glavni dio i istezanje, a ishrana je jednostavna i održiva. Redovno pratim tvoj napredak i mijenjam plan čim vidim da si spreman za sljedeći korak.

                Vidimo se na šipki!

                – Amir 🤸
                """
        },
        new()
        {
            Username = "selma.delic", FirstName = "Selma", LastName = "Delić", Gender = GenderFemale, Age = 28,
            PhoneNumber = "+387 65 441 209", CreatedDay = -270,
            TrainingType = TypeCalisthenics, Nickname = "Flow", YearsOfExperience = 4, MonthlyPrice = 19.99m,
            Status = MentorApprovalStatus.Approved, ReviewedDay = -268,
            Specializations = [GoalHealth, GoalWeightLoss],
            Certificates = [new("selma-certifikat.pdf", "certifikat-trener-kalistenike.pdf")],
            Bio = """
                Zdravo, ja sam Selma! Prije nego što sam otkrila kalisteniku, godinama sam radila za računarom i stalno imala bolove u vratu i leđima. Joga mi je pomogla, ali tek kad sam počela trenirati s vlastitom težinom, osjetila sam da sam i jaka i pokretljiva u isto vrijeme.

                Već četiri godine vodim žene i muškarce svih uzrasta kroz treninge koji spajaju snagu, pokretljivost i kontrolu disanja. Posebno volim raditi s početnicima i s onima koji se vraćaju treningu nakon pauze, trudnoće ili povrede.

                Kod mene nema forsiranja ni poređenja s drugima. Plan pravim tako da ga možeš raditi kod kuće, u parku ili u teretani, za 30 do 50 minuta. Uz trening dobijaš jednostavne prijedloge za ishranu i dnevne navike koje zaista možeš održati.

                Tvoje tijelo pamti svaki mali korak – hajde da ih napravimo zajedno.

                – Selma 🌿
                """
        },
        new()
        {
            Username = "lejla.mujic", FirstName = "Lejla", LastName = "Mujić", Gender = GenderFemale, Age = 32,
            PhoneNumber = "+387 63 905 112", CreatedDay = -380,
            TrainingType = TypeHybrid, Nickname = "Hibrid", YearsOfExperience = 8, MonthlyPrice = 34.99m,
            Status = MentorApprovalStatus.Approved, ReviewedDay = -377,
            Specializations = [GoalWeightLoss, GoalEndurance, GoalMuscleGain],
            Certificates = [new("lejla-certifikat.pdf", "certifikat-funkcionalni-trening.pdf")],
            Bio = """
                Ja sam Lejla, hibridna trenerica i bivša atletičarka. Osam godina radim s ljudima koji žele biti i jaki i izdržljivi – da mogu podići težak teret, ali i istrčati 10 km bez da se ugase na pola puta.

                Nakon karijere u atletici tražila sam trening koji me neće vezati samo za stazu ili samo za teretanu. Tako sam došla do hibridnog treninga: kombinacije utega, trčanja i funkcionalnih kružnih treninga. Završila sam više Hyrox utrka i polumaraton, a danas to iskustvo prenosim na svoje klijente.

                Plan ti slažem sedmicu po sedmicu, u skladu s tvojim poslom i obavezama. Svaki mjesec pratimo kilograme, obime, snagu i kondiciju, pa tačno znamo šta radi, a šta treba promijeniti. Ishrana je fleksibilna, bogata proteinima i prilagođena danima kad treniraš jače.

                Budi spreman da se oznojiš – i da budeš ponosan na sebe.

                – Lejla ⚡
                """
        },
        new()
        {
            Username = "kenan.omerovic", FirstName = "Kenan", LastName = "Omerović", Gender = GenderMale, Age = 39,
            PhoneNumber = "+387 61 560 873", CreatedDay = -300,
            TrainingType = TypeHybrid, Nickname = "Engine", YearsOfExperience = 11, MonthlyPrice = 44.99m,
            Status = MentorApprovalStatus.Approved, ReviewedDay = -299,
            Specializations = [GoalEndurance, GoalCompetition, GoalStrength],
            Certificates = [new("kenan-certifikat.pdf", "certifikat-trener-kondicije.pdf")],
            Bio = """
                Zovem se Kenan i jedanaest godina radim kao trener kondicije. Radio sam s fudbalerima, borcima i rekreativcima koji su se spremali za svoj prvi polumaraton ili Hyrox, a nadimak Engine dobio sam jer nikad ne odustajem u zadnjem krugu.

                Vjerujem da je dobra kondicija temelj svega: bolje spavaš, brže se oporavljaš i imaš energije i za posao i za porodicu. Moj pristup se zasniva na podacima – pulsnim zonama, tempu trčanja i progresiji opterećenja – ali uvijek slušam kako se ti osjećaš.

                Ako se pripremaš za takmičenje, dobit ćeš periodizovan plan s jasnim fazama i testovima. Ako samo želiš biti jači i izdržljiviji, dobit ćeš plan koji se uklapa u tvoj dan. U oba slučaja pratim svaki tvoj trening i javljam se kad treba pojačati ili usporiti.

                Motor se gradi polako, ali kad proradi – ne staje.

                – Kenan 🔥
                """
        },
        new()
        {
            Username = "mia.juric", FirstName = "Mia", LastName = "Jurić", Gender = GenderFemale, Age = 27,
            PhoneNumber = "+387 64 223 781", CreatedDay = -3,
            TrainingType = TypeWeightlifting, Nickname = "Mia Power", YearsOfExperience = 3, MonthlyPrice = 22.00m,
            Status = MentorApprovalStatus.Pending,
            Specializations = [GoalWeightLoss, GoalStrength],
            Certificates =
            [
                new("mia-certifikat.pdf", "certifikat-personalni-trener.pdf"),
                new("mia-diploma.png", "diploma.png")
            ],
            Bio = """
                Ja sam Mia, licencirana personalna trenerica s tri godine iskustva. Radim prvenstveno sa ženama koje žele ući u teretanu bez straha i naučiti pravilnu tehniku čučnja, mrtvog dizanja i potiska.

                Vjerujem da je snaga najbolji saveznik zdravog mršavljenja. Moji planovi su jednostavni, jasni i prilagođeni tvom rasporedu, a uz njih dobijaš i savjete za ishranu bez strogih dijeta.
                """
        },
        new()
        {
            Username = "edin.softic", FirstName = "Edin", LastName = "Softić", Gender = GenderMale, Age = 30,
            PhoneNumber = "+387 62 670 455", CreatedDay = -6,
            TrainingType = TypeCalisthenics, YearsOfExperience = 5, MonthlyPrice = 18.50m,
            Status = MentorApprovalStatus.Pending,
            Specializations = [GoalEndurance],
            Certificates =
            [
                new("edin-certifikat.pdf", "certifikat-trener.pdf"),
                new("edin-licenca.png", "licenca.png")
            ],
            Bio = """
                Pozdrav, ja sam Edin. Pet godina treniram kalisteniku i vodim grupne treninge u parku. Specijalizovan sam za izdržljivost: kružne treninge s vlastitom težinom, preskakanje užeta i trčanje.

                Pridruži mi se i otkrij koliko možeš postići bez ikakve opreme – samo uz upornost i dobar plan.
                """
        },
        new()
        {
            Username = "jasmin.hodzic", FirstName = "Jasmin", LastName = "Hodžić", Gender = GenderMale, Age = 26,
            PhoneNumber = "+387 66 318 027", CreatedDay = -18,
            TrainingType = TypeHybrid, YearsOfExperience = 1, MonthlyPrice = 15.00m,
            Status = MentorApprovalStatus.Rejected, ReviewedDay = -15,
            RejectionReason = "Priloženi dokument nije važeća licenca trenera. Molimo priložite zvanični certifikat.",
            Specializations = [GoalWeightLoss],
            Certificates = [new("jasmin-certifikat.png", "potvrda-o-kursu.png")],
            Bio = """
                Zdravo, ja sam Jasmin. Godinu dana radim kao pomoćni trener u lokalnom fitness centru i završio sam online kurs funkcionalnog treninga.

                Volim kombinovati trčanje i vježbe s utezima, a posebno mi je drago kad pomognem nekome da skine višak kilograma i stekne zdrave navike.
                """
        }
    ];

    // ---------------------------------------------------------------- klijenti

    private static readonly ClientSeed[] ClientSeeds =
    [
        new()
        {
            Username = "client", FirstName = "Tarik", LastName = "Hadžić", Gender = GenderMale, Age = 27,
            PhoneNumber = "+387 61 482 915", CreatedDay = -200,
            WeightKg = 84m, HeightCm = 181m, FitnessLevel = LevelIntermediate, TrainingExperienceYears = 4,
            FitnessGoal = GoalMuscleGain, PreferredTrainingType = TypeWeightlifting,
            GoalDescription = "Želim izgraditi mišićnu masu i doći do čučnja od 120 kg, uz manje masnog tkiva oko struka."
        },
        new()
        {
            Username = "mobile", FirstName = "Amina", LastName = "Kovačević", Gender = GenderFemale, Age = 26,
            PhoneNumber = "+387 62 719 340", CreatedDay = -178,
            WeightKg = 68m, HeightCm = 167m, FitnessLevel = LevelRecreational, TrainingExperienceYears = 2,
            FitnessGoal = GoalWeightLoss, PreferredTrainingType = TypeCalisthenics,
            GoalDescription = "Želim skinuti još nekoliko kilograma, zategnuti tijelo i redovno raditi pune zgibove."
        },
        new()
        {
            Username = "emir.basic", FirstName = "Emir", LastName = "Bašić", Gender = GenderMale, Age = 35,
            PhoneNumber = "+387 61 903 562", CreatedDay = -205,
            WeightKg = 92m, HeightCm = 185m, FitnessLevel = LevelBeginner, TrainingExperienceYears = 0,
            FitnessGoal = GoalWeightLoss, PreferredTrainingType = TypeWeightlifting,
            GoalDescription = "Želim smršati 15 kilograma i naučiti pravilno trenirati u teretani."
        },
        new()
        {
            Username = "sara.hasic", FirstName = "Sara", LastName = "Hasić", Gender = GenderFemale, Age = 22,
            PhoneNumber = "+387 63 284 617", CreatedDay = -163,
            WeightKg = 61m, HeightCm = 170m, FitnessLevel = LevelIntermediate, TrainingExperienceYears = 3,
            FitnessGoal = GoalStrength, PreferredTrainingType = TypeWeightlifting,
            GoalDescription = "Želim povećati snagu u osnovnim dizanjima i nastupiti na svom prvom powerlifting takmičenju."
        },
        new()
        {
            Username = "adna.mehic", FirstName = "Adna", LastName = "Mehić", Gender = GenderFemale, Age = 23,
            PhoneNumber = "+387 62 506 183", CreatedDay = -158,
            WeightKg = 58m, HeightCm = 163m, FitnessLevel = LevelRecreational, TrainingExperienceYears = 1,
            FitnessGoal = GoalMuscleGain, PreferredTrainingType = TypeWeightlifting,
            GoalDescription = "Želim izgraditi mišiće nogu i gluteusa te dobiti više snage u gornjem dijelu tijela."
        },
        new()
        {
            Username = "belma.causevic", FirstName = "Belma", LastName = "Čaušević", Gender = GenderFemale, Age = 31,
            CreatedDay = -108,
            WeightKg = 74m, HeightCm = 172m, FitnessLevel = LevelBeginner, TrainingExperienceYears = 1,
            FitnessGoal = GoalWeightLoss, PreferredTrainingType = TypeHybrid,
            GoalDescription = "Želim skinuti kilograme nakon porodiljskog i vratiti energiju za svakodnevne obaveze."
        },
        new()
        {
            Username = "nedim.alic", FirstName = "Nedim", LastName = "Alić", Gender = GenderMale, Age = 33,
            PhoneNumber = "+387 61 347 829", CreatedDay = -70,
            WeightKg = 79m, HeightCm = 178m, FitnessLevel = LevelAdvanced, TrainingExperienceYears = 7,
            FitnessGoal = GoalEndurance, PreferredTrainingType = TypeHybrid,
            GoalDescription = "Želim istrčati polumaraton ispod 1:40 i pritom zadržati snagu u teretani."
        },
        new()
        {
            Username = "ajla.hadzic", FirstName = "Ajla", LastName = "Hadžić", Gender = GenderFemale, Age = 28,
            CreatedDay = -96,
            WeightKg = 64m, HeightCm = 168m, FitnessLevel = LevelRecreational, TrainingExperienceYears = 2,
            FitnessGoal = GoalHealth, PreferredTrainingType = TypeCalisthenics,
            GoalDescription = "Želim se riješiti bolova u leđima od sjedenja i postati pokretljivija."
        },
        new()
        {
            Username = "harun.begovic", FirstName = "Harun", LastName = "Begović", Gender = GenderMale, Age = 36,
            PhoneNumber = "+387 65 812 406", CreatedDay = -182,
            WeightKg = 88m, HeightCm = 183m, FitnessLevel = LevelIntermediate, TrainingExperienceYears = 5,
            FitnessGoal = GoalStrength, PreferredTrainingType = TypeWeightlifting,
            GoalDescription = "Želim povećati snagu u čučnju i mrtvom dizanju bez bolova u donjem dijelu leđa."
        },
        new()
        {
            Username = "lamija.softic", FirstName = "Lamija", LastName = "Softić", Gender = GenderFemale, Age = 25,
            PhoneNumber = "+387 61 655 094", CreatedDay = -118,
            WeightKg = 57m, HeightCm = 165m, FitnessLevel = LevelRecreational, TrainingExperienceYears = 2,
            FitnessGoal = GoalEndurance, PreferredTrainingType = TypeHybrid,
            GoalDescription = "Želim završiti svoju prvu Hyrox utrku i popraviti kondiciju."
        },
        new()
        {
            Username = "faruk.zukic", FirstName = "Faruk", LastName = "Zukić", Gender = GenderMale, Age = 41,
            CreatedDay = -146, BlockedLastLoginDay = -100,
            WeightKg = 95m, HeightCm = 180m, FitnessLevel = LevelBeginner, TrainingExperienceYears = 0,
            FitnessGoal = GoalWeightLoss,
            GoalDescription = "Želim smršati i vratiti se fudbalu s prijateljima nakon dugogodišnje pauze."
        }
    ];

    // ---------------------------------------------------------------- pretplate (dani su relativni: -80 = prije 80 dana)

    private static readonly SubscriptionSeed[] SubscriptionSeeds =
    [
        new()
        {
            Key = "client_lejla", Client = "client", Mentor = "lejla.mujic", Status = SubscriptionStatus.Expired,
            CreatedDay = -185, Period = (-184, -94), RenewalDays = [-154, -124],
            Questionnaire = new(
                "Želim skinuti nekoliko kilograma oko struka i popraviti kondiciju, a da pritom ne izgubim snagu.",
                "Radnim danima mogu trenirati poslije 17 h, oko sat vremena. Vikendom imam više vremena.",
                "Nemam ozbiljnih zdravstvenih problema. Ponekad me zaboli donji dio leđa nakon dugog sjedenja.",
                "Ne koristim lijekove.",
                "4–5 treninga sedmično.",
                "Radim kao programer i većinu dana sjedim. Vikendom povremeno igram mali fudbal."),
            Review = new(5, 1,
                "Lejla je odlična trenerica! Za tri mjeseca skinuo sam 4 kg, a 5 km trčim skoro dvije minute brže. " +
                "Treninzi su raznovrsni i nikad nije dosadno. Na kraju sam se htio više fokusirati na mišićnu masu, " +
                "pa sam prešao kod trenera za utege, ali Lejlu toplo preporučujem svima.")
        },
        new()
        {
            Key = "client_mentor", Client = "client", Mentor = "mentor", Status = SubscriptionStatus.Active,
            CreatedDay = -80, Period = (-79, 11), RenewalDays = [-50, -20],
            Questionnaire = new(
                "Nakon hibridnog programa želim se fokusirati na mišićnu masu i snagu, posebno na čučanj i bench press.",
                "Mogu trenirati 5 dana sedmično po 75–80 minuta, uglavnom između 18 i 20 h.",
                "Bez povreda. Desno koljeno ponekad malo škripi kod dubokog čučnja, ali ne boli.",
                "Ne koristim lijekove. Od suplemenata uzimam kreatin i protein u prahu.",
                "5 treninga sedmično i jedan dan aktivnog odmora.",
                "Sjedilački posao. Vikendom šetnja s djevojkom i povremeno mali fudbal.")
        },
        new()
        {
            Key = "mobile_selma", Client = "mobile", Mentor = "selma.delic", Status = SubscriptionStatus.Cancelled,
            CreatedDay = -170, Period = (-169, -139), CancelledDay = -150,
            StatusReason = DomainTexts.ClientCancelledReason,
            Questionnaire = new(
                "Želim početi redovno trenirati, smršati nekoliko kilograma i riješiti ukočenost u leđima.",
                "Tri do četiri puta sedmično po 40 minuta, najčešće rano ujutro prije posla.",
                "Blaga skolioza, bez bolova. Nemam drugih zdravstvenih problema.",
                "Ne koristim lijekove.",
                "3 treninga sedmično za početak.",
                "Radim u banci i puno sjedim. Svaki dan pješačim oko 20 minuta do posla."),
            Review = new(4, 1,
                "Selma je jako strpljiva i objašnjava svaku vježbu do detalja. Leđa su me prestala boljeti već nakon " +
                "tri sedmice. Otkazala sam jer mi je raspored postao pretrpan i trebao mi je jači fokus na mršavljenje, " +
                "ali je od srca preporučujem svim početnicima.")
        },
        new()
        {
            Key = "mobile_amir", Client = "mobile", Mentor = "amir.salihovic", Status = SubscriptionStatus.Active,
            CreatedDay = -125, Period = (-125, 25), RenewalDays = [-95, -65, -35, -5],
            Questionnaire = new(
                "Želim skinuti još 5–6 kilograma, zategnuti ruke i stomak i uraditi svoj prvi zgib.",
                "Mogu izdvojiti 45–55 minuta četiri puta sedmično, plus duže šetnje vikendom.",
                "Blaga skolioza, bez bolova. Ponekad me zaboli lijevo rame kod sklekova.",
                "Ne koristim lijekove, povremeno uzimam vitamin D.",
                "4 treninga sedmično i jedan dan šetnje ili bicikla.",
                "Posao u banci (sjedilački). Vikendom idem na Trebević ili vozim bicikl Wilsonovim šetalištem.")
        },
        new()
        {
            Key = "emir_amir", Client = "emir.basic", Mentor = "amir.salihovic", Status = SubscriptionStatus.Expired,
            CreatedDay = -200, Period = (-200, -170),
            Questionnaire = new(
                "Želim smršati i naviknuti se na redovan trening kod kuće, prije nego što krenem u teretanu.",
                "Oko 40 minuta, tri puta sedmično, navečer poslije posla.",
                "Povišen krvni pritisak, pod kontrolom ljekara. Koljena su osjetljiva na skokove.",
                "Jedna tableta za pritisak dnevno.",
                "3 treninga sedmično.",
                "Radim kao vozač dostave i dosta sjedim u autu. Nemam drugih sportskih aktivnosti."),
            Review = new(5, 1,
                "Amir mi je promijenio navike. Počeo sam sa sklekovima s koljena, a nakon mjesec dana radim 15 punih " +
                "sklekova i skinuo sam 4 kg. Uvijek odgovara na poruke i prilagodio je plan mom pritisku. Hvala, Bar Master!")
        },
        new()
        {
            Key = "emir_mentor", Client = "emir.basic", Mentor = "mentor", Status = SubscriptionStatus.AwaitingMentor,
            CreatedDay = -2,
            Questionnaire = new(
                "Nakon kalistenike kod kuće želim u teretanu: skinuti još 10 kg i naučiti osnovne vježbe s utezima.",
                "Mogu trenirati četiri puta sedmično po sat vremena, poslije 18 h.",
                "Povišen krvni pritisak, kontrolisan terapijom. Ljekar je odobrio trening umjerenog intenziteta.",
                "Jedna tableta za pritisak ujutro.",
                "4 treninga sedmično.",
                "Vozač dostave. Vikendom šetam psa po sat vremena.")
        },
        new()
        {
            Key = "sara_dino", Client = "sara.hasic", Mentor = "dino.kurtovic", Status = SubscriptionStatus.Expired,
            CreatedDay = -150, Period = (-150, -120),
            Questionnaire = new(
                "Želim povećati snagu u čučnju, benchu i mrtvom dizanju i ispraviti tehniku.",
                "Četiri puta sedmično po 70–90 minuta, uglavnom navečer.",
                "Nemam zdravstvenih problema.",
                "Ne koristim lijekove.",
                "4 treninga sedmično.",
                "Studentica sam i dosta učim. Dva puta sedmično igram odbojku rekreativno."),
            Review = new(4, 2,
                "Dino zna o čemu govori – tehnika mrtvog dizanja mi je sada mnogo bolja i podigla sam 20 kg više nego " +
                "na početku. Plan je bio dobar, samo bih voljela malo više detalja u dijelu o ishrani. Preporučujem.")
        },
        new()
        {
            Key = "sara_mentor", Client = "sara.hasic", Mentor = "mentor", Status = SubscriptionStatus.AwaitingMentor,
            CreatedDay = -1,
            Questionnaire = new(
                "Spremam se za svoje prvo powerlifting takmičenje za otprilike pet mjeseci i treba mi strukturiran plan.",
                "Mogu trenirati 4–5 puta sedmično, do 90 minuta po treningu.",
                "Prije dva mjeseca imala sam blagu upalu tetive lakta, sada sam bez bolova.",
                "Ne koristim lijekove. Uzimam kreatin i magnezijum.",
                "4 do 5 treninga sedmično.",
                "Studentica, odbojka jednom sedmično. Ljeti puno plivam.")
        },
        new()
        {
            Key = "adna_kenan", Client = "adna.mehic", Mentor = "kenan.omerovic", Status = SubscriptionStatus.Expired,
            CreatedDay = -150, Period = (-150, -120),
            Questionnaire = new(
                "Želim popraviti kondiciju i ojačati noge prije ljetne sezone planinarenja.",
                "Tri do četiri puta sedmično po sat vremena, ujutro prije fakulteta.",
                "Bez zdravstvenih problema.",
                "Ne koristim lijekove.",
                "3–4 treninga sedmično.",
                "Planinarim skoro svaki vikend, najčešće na Bjelašnici i Treskavici."),
            Review = new(5, -1,
                "Kenan je profesionalac do srži. Program je bio savršeno prilagođen planinarenju – ove sezone sam na " +
                "Treskavici prvi put bila na čelu grupe od početka do kraja. Svaka preporuka!")
        },
        new()
        {
            Key = "adna_mentor", Client = "adna.mehic", Mentor = "mentor", Status = SubscriptionStatus.Active,
            CreatedDay = -5, Period = (-4, 26),
            Questionnaire = new(
                "Želim dobiti mišićnu masu, posebno noge i gluteus, i naučiti pravilno raditi s utezima.",
                "Četiri treninga sedmično po 60–75 minuta, najčešće poslijepodne.",
                "Nemam zdravstvenih problema, samo ponekad osjetim zglob lijeve šake kod potiska.",
                "Ne koristim lijekove.",
                "4 treninga sedmično.",
                "Planinarenje vikendom i svakodnevno oko 8.000 koraka.")
        },
        new()
        {
            Key = "belma_mentor", Client = "belma.causevic", Mentor = "mentor", Status = SubscriptionStatus.Expired,
            CreatedDay = -101, Period = (-100, -70),
            Questionnaire = new(
                "Želim smršati nakon porodiljskog i vratiti snagu i energiju.",
                "Tri puta sedmično po 45–60 minuta, dok je beba kod bake.",
                "Prirodni porođaj prije 10 mjeseci, ginekolog je odobrio trening. Ponekad me bole leđa od nošenja bebe.",
                "Ne koristim lijekove. Pijem željezo i vitamin D.",
                "3 treninga sedmično.",
                "Svakodnevne šetnje s kolicima od 30 do 45 minuta."),
            Review = new(4, 2,
                "Haris je pokazao veliko razumijevanje za moju situaciju nakon porođaja. Plan je bio blag, ali efikasan: " +
                "skinula sam 3 kg za mjesec i vratila snagu u leđima. Jedna zvjezdica manje samo zato što su mi " +
                "treninzi ponekad bili predugi za raspored s bebom.")
        },
        new()
        {
            Key = "nedim_mentor", Client = "nedim.alic", Mentor = "mentor", Status = SubscriptionStatus.Cancelled,
            CreatedDay = -61, Period = (-60, -30), CancelledDay = -45,
            StatusReason = DomainTexts.ClientCancelledReason,
            Questionnaire = new(
                "Želim povećati snagu u osnovnim dizanjima kao dodatak trčanju.",
                "Mogu dva do tri treninga s utezima sedmično, po sat vremena.",
                "Stara povreda lijevog skočnog zgloba, sada bez smetnji.",
                "Ne koristim lijekove.",
                "3 treninga s utezima i 3 trčanja sedmično.",
                "Trčim 30–40 km sedmično, vikendom vozim bicikl."),
            Review = new(4, 1,
                "Odličan program snage – za mjesec i po čučanj mi je skočio sa 110 na 125 kg. Otkazao sam jer sam " +
                "odlučio da se potpuno posvetim polumaratonu, ali Harisov pristup i ispravljanje tehnike mogu samo pohvaliti.")
        },
        new()
        {
            Key = "nedim_kenan", Client = "nedim.alic", Mentor = "kenan.omerovic", Status = SubscriptionStatus.Active,
            CreatedDay = -40, Period = (-40, 20), RenewalDays = [-10],
            Questionnaire = new(
                "Cilj mi je polumaraton ispod 1:40 na jesen, uz održavanje snage.",
                "Šest dana sedmično, 45–90 minuta, trening ujutro prije posla.",
                "Bez povreda. Povremeno se zategne lijevi list nakon dugih trčanja.",
                "Ne koristim lijekove. Uzimam elektrolite na dužim treninzima.",
                "5–6 treninga sedmično.",
                "Radim kao fizioterapeut i cijeli dan sam na nogama. Vikendom vozim bicikl.")
        },
        new()
        {
            Key = "ajla_lejla", Client = "ajla.hadzic", Mentor = "lejla.mujic", Status = SubscriptionStatus.Expired,
            CreatedDay = -90, Period = (-90, -60),
            Questionnaire = new(
                "Želim se riješiti bolova u leđima od sjedenja, ojačati trup i postati izdržljivija.",
                "Tri puta sedmično po 45 minuta, uglavnom navečer.",
                "Bolovi u donjem dijelu leđa od dugog sjedenja, bez dijagnoze diskus hernije.",
                "Povremeno ibuprofen kad me leđa jače zabole.",
                "3 treninga sedmično.",
                "Grafička dizajnerica, radim od kuće. Šetnja sa psom dva puta dnevno."),
            Review = new(5, 1,
                "Nakon mjesec dana s Lejlom leđa me više ne bole, a prvi put u životu sam istrčala 5 km bez stajanja. " +
                "Lejla je uvijek pozitivna i motiviše te čak i kad nemaš volje. Hvala!")
        },
        new()
        {
            Key = "ajla_selma", Client = "ajla.hadzic", Mentor = "selma.delic", Status = SubscriptionStatus.PendingPayment,
            CreatedDay = -1,
            Questionnaire = new(
                "Želim nastaviti raditi na pokretljivosti i naučiti osnovne kalisteničke vježbe.",
                "Četiri puta sedmično po 40 minuta, mogu trenirati kod kuće.",
                "Leđa su mnogo bolje nakon prošlog programa, ali se i dalje ukoče nakon dugog sjedenja.",
                "Ne koristim lijekove.",
                "4 treninga sedmično.",
                "Rad od kuće, šetnje sa psom i joga vikendom.")
        },
        new()
        {
            Key = "harun_amir", Client = "harun.begovic", Mentor = "amir.salihovic", Status = SubscriptionStatus.Expired,
            CreatedDay = -175, Period = (-175, -145),
            Questionnaire = new(
                "Želim ojačati gornji dio tijela i naučiti zgibove i propadanja s dodatnim teretom.",
                "Četiri puta sedmično po sat vremena.",
                "Povremeno me zaboli donji dio leđa nakon teškog mrtvog dizanja.",
                "Ne koristim lijekove.",
                "4 treninga sedmično.",
                "Radim u građevini, posao je fizički zahtjevan. Vikendom idem na ribolov."),
            Review = new(5, 1,
                "Svaka čast Amiru! Za mjesec dana sam sa 8 zgibova došao na 15 i uradio prvo propadanje s dodatnih " +
                "10 kg. Treninzi su kratki, ali ubitačni.")
        },
        new()
        {
            Key = "harun_kenan", Client = "harun.begovic", Mentor = "kenan.omerovic", Status = SubscriptionStatus.Expired,
            CreatedDay = -140, Period = (-140, -110),
            Questionnaire = new(
                "Želim popraviti kondiciju jer se brzo zadišem na poslu i na stepenicama.",
                "Tri do četiri treninga sedmično po 60 minuta.",
                "Donji dio leđa povremeno boli, ljekar je preporučio jačanje trupa.",
                "Ne koristim lijekove.",
                "3–4 treninga sedmično.",
                "Fizički posao u građevini, 8–10 sati dnevno na nogama."),
            Review = new(4, 3,
                "Kenan je vrhunski trener kondicije. Kondicija mi je mnogo bolja i više se ne zadišem na poslu. " +
                "Treninzi su bili zahtjevni, pa sam uz fizički posao ponekad teško pratio tempo.")
        },
        new()
        {
            Key = "harun_dino", Client = "harun.begovic", Mentor = "dino.kurtovic", Status = SubscriptionStatus.Rejected,
            CreatedDay = -30, RefundedDay = -29,
            StatusReason = "Trenutno nemam slobodnih termina za nove klijente. Preporučujem kolegu Harisa Mehmedovića.",
            Questionnaire = new(
                "Želim povećati snagu u čučnju i mrtvom dizanju, cilj mi je 180 kg u mrtvom dizanju.",
                "Četiri puta sedmično po 75 minuta, poslije posla.",
                "Donji dio leđa je stabilan otkako radim vježbe za trup, bez bolova zadnja tri mjeseca.",
                "Ne koristim lijekove.",
                "4 treninga sedmično.",
                "Građevina, fizički posao. Vikendom ribolov i šetnje.")
        },
        new()
        {
            Key = "lamija_selma", Client = "lamija.softic", Mentor = "selma.delic", Status = SubscriptionStatus.Expired,
            CreatedDay = -110, Period = (-110, -80),
            Questionnaire = new(
                "Želim popraviti držanje i opću kondiciju, jer se brzo umorim.",
                "Tri puta sedmično po 40 minuta, ujutro.",
                "Nemam zdravstvenih problema.",
                "Ne koristim lijekove.",
                "3 treninga sedmično.",
                "Medicinska sestra u smjenama, na poslu puno hodam."),
            Review = new(5, 0,
                "Selma je divna! Držanje mi je mnogo bolje, a jutarnji treninzi su postali moj omiljeni dio dana. " +
                "Plan se lako uklapao u smjene. Zbog nje sam se odlučila i na Hyrox 🙂")
        },
        new()
        {
            Key = "lamija_lejla", Client = "lamija.softic", Mentor = "lejla.mujic", Status = SubscriptionStatus.Active,
            CreatedDay = -15, Period = (-15, 15),
            Questionnaire = new(
                "Prijavila sam se na svoju prvu Hyrox utrku za tri mjeseca i želim je završiti bez hodanja.",
                "Četiri do pet treninga sedmično, 45–75 minuta, prilagođeno smjenama.",
                "Bez zdravstvenih problema. Ponekad me bole stopala nakon noćne smjene.",
                "Ne koristim lijekove.",
                "4–5 treninga sedmično.",
                "Medicinska sestra u smjenama. Slobodnim danima trčim 5 km.")
        },
        new()
        {
            Key = "faruk_dino", Client = "faruk.zukic", Mentor = "dino.kurtovic", Status = SubscriptionStatus.Expired,
            CreatedDay = -140, Period = (-140, -110),
            Questionnaire = new(
                "Želim smršati barem 15 kilograma i vratiti se fudbalu s prijateljima.",
                "Tri puta sedmično po sat vremena, navečer.",
                "Prekomjerna težina, povremeno osjetim koljena na stepenicama.",
                "Ne koristim lijekove.",
                "3 treninga sedmično.",
                "Radim u kancelariji i skoro da nemam fizičke aktivnosti."),
            Review = new(3, 2,
                "Plan je bio solidan i Dino je stručan, ali mi je na samom početku bilo preteško pa sam često preskakao " +
                "treninge. Skinuo sam 2 kg. Mislim da mi je trebalo postepenije uvođenje.")
        }
    ];

    // ---------------------------------------------------------------- odrađeni treninzi

    private static readonly (string PlanKey, SessionSeed Seed)[] SessionSeeds =
    [
        ("client_mentor", new SessionSeed(
            FromDay: -77, ToDay: -1, TrainingDays: [1, 2, 4, 5, 6], Count: 45, RestDayCount: 2,
            BaseRepetitions: [95, 80, 30, 100, 70, 120, 20], Trend: 25,
            Notes:
            [
                new(-72, 2, "Povećao težinu na čučnju."),
                new(-60, 4, "Dodao 2,5 kg na veslanje u pretklonu."),
                new(-48, null, "Umoran nakon posla, skratio trening na 50 minuta."),
                new(-38, 1, "Bench 80 kg × 8 u svim serijama – sljedeće sedmice 82,5 kg."),
                new(-21, 2, "Desno koljeno malo steže na zadnjoj seriji čučnja."),
                new(-15, 2, "Traka iznad koljena pomaže, čučanj je mnogo stabilniji."),
                new(-11, 2, "Čučanj 105 kg 4×6 bez ikakvih problema s koljenom!"),
                new(-4, 5, "Mrtvo dizanje 150 kg × 3 – novi lični rekord!")
            ])),
        ("mobile_amir", new SessionSeed(
            FromDay: -120, ToDay: -1, TrainingDays: [1, 3, 4, 5], Count: 40, RestDayCount: 3,
            BaseRepetitions: [90, 25, 120, 140, 100, 30, 15], Trend: 30,
            Notes:
            [
                new(-110, 1, "Negativni zgibovi su teži nego što sam mislila, ali sam uradila sve serije."),
                new(-90, 4, "Tabata me dotukla, ali sam izdržala svih 8 rundi."),
                new(-70, 5, "Prvi puni sklek s poda! 💪"),
                new(-50, 3, "Bugarski čučanj s bučicama od 4 kg."),
                new(-30, 1, "Rame više uopće ne boli kod sklekova."),
                new(-6, 5, "PRVI PUNI ZGIB! 🎉")
            ])),
        ("client_lejla", new SessionSeed(
            FromDay: -182, ToDay: -95, TrainingDays: [1, 2, 4, 5, 6], Count: 20, RestDayCount: 0,
            BaseRepetitions: [70, 40, 25, 90, 35, 110, 15], Trend: 15,
            Notes:
            [
                new(-175, 2, "Intervali na traci s nagibom 1% – zadnja dva su bila teška."),
                new(-130, 6, "Prvi put 5 km bez stajanja prije kružnog dijela!")
            ])),
        ("nedim_kenan", new SessionSeed(
            FromDay: -38, ToDay: -1, TrainingDays: [1, 2, 4, 5, 6], Count: 8, RestDayCount: 0,
            BaseRepetitions: [80, 45, 25, 95, 40, 120, 15], Trend: 10,
            Notes: [new(-12, 6, "Dugo trčanje 17 km, zadnjih 5 km u ciljanom tempu 4:40.")])),
        ("lamija_lejla", new SessionSeed(
            FromDay: -13, ToDay: -1, TrainingDays: [1, 2, 4, 5, 6], Count: 8, RestDayCount: 0,
            BaseRepetitions: [60, 35, 20, 75, 30, 90, 10], Trend: 10,
            Notes: [new(-3, 6, "Prva Hyrox simulacija – završila sam sve 4 runde!")]))
    ];

    // ---------------------------------------------------------------- mjesečni napredak (od najstarijeg do tekućeg mjeseca)

    private static readonly ProgressSeed[] ClientProgressSeeds =
    [
        new(90.5m, "Struk 94 cm, prsa 105 cm, ruka 36 cm, bedro 60 cm", "Čučanj 90 kg × 5, bench 72,5 kg × 5, mrtvo 120 kg × 5", "5 km za 29:40"),
        new(89.0m, "Struk 92 cm, prsa 105 cm, ruka 36,5 cm, bedro 60 cm", "Čučanj 95 kg × 5, bench 75 kg × 5, mrtvo 127,5 kg × 5", "5 km za 28:50"),
        new(87.8m, "Struk 90 cm, prsa 104 cm, ruka 37 cm, bedro 59,5 cm", "Čučanj 100 kg × 5, bench 77,5 kg × 5, mrtvo 135 kg × 5", "5 km za 27:55"),
        new(86.4m, "Struk 89 cm, prsa 105 cm, ruka 37,5 cm, bedro 60 cm", "Čučanj 100 kg × 6, bench 80 kg × 5, mrtvo 140 kg × 5", "Veslanje 2 km za 8:05"),
        new(85.2m, "Struk 87 cm, prsa 105 cm, ruka 38 cm, bedro 60,5 cm", "Čučanj 105 kg × 6, bench 82,5 kg × 5, mrtvo 145 kg × 5", "Veslanje 2 km za 7:52"),
        new(84.0m, "Struk 86 cm, prsa 106 cm, ruka 38,5 cm, bedro 61 cm", "Čučanj 110 kg × 5, bench 85 kg × 5, mrtvo 150 kg × 3", "Veslanje 2 km za 7:41")
    ];

    private static readonly ProgressSeed[] MobileProgressSeeds =
    [
        new(72.0m, "Struk 80 cm, kukovi 104 cm, bedro 60 cm, nadlaktica 29 cm", "Sklekovi s koljena 10, čučanj bez tereta 20, plank 30 s", "Brzo hodanje 5 km za 55 min"),
        new(71.2m, "Struk 79 cm, kukovi 103 cm, bedro 59,5 cm, nadlaktica 29 cm", "Sklekovi s klupe 12, australijski zgibovi 6, plank 45 s", "Brzo hodanje 5 km za 52 min"),
        new(70.1m, "Struk 77 cm, kukovi 102 cm, bedro 59 cm, nadlaktica 28,5 cm", "Sklekovi s klupe 15, australijski zgibovi 8, plank 60 s", "3 km trčanja i hodanja za 24 min"),
        new(69.4m, "Struk 76 cm, kukovi 101 cm, bedro 58 cm, nadlaktica 28,5 cm", "Puni sklekovi 3, negativni zgib 3 × 5 s, plank 75 s", "3 km trčanja za 21:30"),
        new(68.6m, "Struk 75 cm, kukovi 100 cm, bedro 57,5 cm, nadlaktica 28 cm", "Puni sklekovi 6, zgib uz gumu 5, hollow hold 30 s", "5 km za 36:10"),
        new(67.9m, "Struk 74 cm, kukovi 99 cm, bedro 57 cm, nadlaktica 28 cm", "Puni sklekovi 10, prvi puni zgib, hollow hold 40 s", "5 km za 34:20")
    ];
}

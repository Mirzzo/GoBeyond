using System.Globalization;

namespace GoBeyond.Infrastructure.Database;

// Sadržaj trening planova: dva detaljno napisana glavna demo plana (client, mobile)
// i šabloni po vrsti treninga (hibrid, utezi) koje ostali planovi popunjavaju svojim opterećenjima.
public sealed partial class DatabaseSeeder
{
    private sealed record DaySpec(int DayOfWeek, int TrainingMinutes, string Training, int? NutritionMinutes, string Nutrition);

    private sealed record PlanContent(string? Quote, IReadOnlyList<DaySpec> Days);

    // ================================================================ Haris → Tarik (client), utezi, hipertrofija

    private static PlanContent ClientMentorPlan() => new(
        "Snaga ne dolazi iz onoga što možeš danas, nego iz savladavanja onoga što jučer nisi mogao. 💪",
        [
            new DaySpec(DayOfWeek: 1, TrainingMinutes: 75,
                Training: """
                    GORNJI DIO A – prsa, ramena, triceps

                    Zagrijavanje (10 min):
                    • 5 min veslanja ili bicikla laganim tempom
                    • Rotacije ramena s gumom 2×15 i face pull s gumom 2×15
                    • Bench press s praznom šipkom 2×10

                    Glavni dio:
                    1. Bench press – 4×6–8 (82,5 kg), odmor 2–3 min, RPE 8
                    2. Kosi potisak bučicama (30°) – 3×8–10 (30 kg), odmor 2 min
                    3. Potisak šipke iznad glave stojeći – 3×8 (47,5 kg)
                    4. Propadanja na razboju – 3×10–12 (kad sve serije pređu 12, dodaj 5 kg)
                    5. Odručenje bučicama u stranu – 3×15 (10 kg), zadnja serija do otkaza
                    6. Triceps na sajli s užetom – 3×12–15

                    Hlađenje (5 min):
                    • Istezanje prsa i tricepsa uz zid, 2×30 s po strani
                    • Disanje u položaju 90/90 na leđima, 2 min

                    Progresija: kad sve serije bench pressa uradiš s 8 ponavljanja, sljedeće sedmice dodaj 2,5 kg.
                    """,
                NutritionMinutes: 45,
                Nutrition: """
                    Dnevni cilj: ~2.900 kcal | proteini 185 g · ugljikohidrati 340 g · masti 85 g

                    Doručak (07:30): 90 g ovsenih pahuljica kuhanih u 300 ml mlijeka, 1 banana, 20 g kikiriki putera
                    Užina (10:30): 200 g grčkog jogurta, 30 g oraha, šaka borovnica
                    Ručak (13:30): 200 g pilećih prsa, 250 g kuhane basmati riže, miješana salata s 10 ml maslinovog ulja
                    Prije treninga (17:00): 2 kriške integralnog hljeba, 60 g goveđeg pršuta, 1 jabuka
                    Poslije treninga (19:45): 30 g proteina u prahu u vodi i 1 banana
                    Večera (20:30): omlet od 3 jaja i 100 g bjelanaca, 150 g krompira iz rerne, povrće na žaru

                    Voda: najmanje 3 l. Kreatin 5 g uz doručak, svaki dan.
                    """),
            new DaySpec(DayOfWeek: 2, TrainingMinutes: 80,
                Training: """
                    DONJI DIO A – čučanj i kvadricepsi

                    Zagrijavanje (12 min):
                    • 5 min bicikla laganim tempom
                    • Elastična traka iznad koljena: bočni hod 2×15 koraka i čučanj s trakom 2×15
                    • Goblet čučanj s pauzom od 2 s na dnu 2×8 (16 kg)
                    • Zagrijavanje šipke: 20 kg × 10, 60 kg × 5, 80 kg × 3, 95 kg × 1

                    Glavni dio:
                    1. Čučanj sa šipkom – 4×6 (105 kg), odmor 3 min. U prvoj seriji pauza 2 s na dnu, koljena prate prste.
                    2. Rumunsko mrtvo dizanje – 3×8 (100 kg), odmor 2 min
                    3. Bugarski čučanj s bučicama – 3×10 po nozi (2×18 kg)
                    4. Potisak nogama – 3×12 (180 kg), spuštanje 3 s
                    5. Pregib nogu na mašini – 3×12
                    6. Podizanje na prste stojeći – 4×15

                    Hlađenje (8 min):
                    • Istezanje kvadricepsa i pregibača kuka, 2×40 s po nozi
                    • Valjak za bedra i listove, 3 min

                    Napomena: ako koljeno ponovo počne stezati, spusti težinu za 5 kg i javi mi se.
                    """,
                NutritionMinutes: 45,
                Nutrition: """
                    Dnevni cilj: ~2.950 kcal | proteini 185 g · ugljikohidrati 350 g · masti 85 g

                    Doručak (07:30): kajgana od 4 jaja, 2 kriške integralnog hljeba, paradajz i krastavac
                    Užina (10:30): 250 g kravljeg sira, 1 kašičica meda, 1 kruška
                    Ručak (13:30): 200 g junećeg gulaša, 250 g kuhane tjestenine, salata od kupusa
                    Prije treninga (17:00): 60 g ovsenih pahuljica s 200 ml mlijeka i 1 bananom
                    Poslije treninga (20:00): 30 g proteina u prahu i 2 rižina krekera s medom
                    Večera (20:45): 180 g pilećih bataka bez kože, 200 g riže, grilovano povrće

                    Voda: 3–3,5 l. Na dan čučnja ne preskači obrok prije treninga.
                    """),
            new DaySpec(DayOfWeek: 3, TrainingMinutes: 45,
                Training: """
                    AKTIVNI OPORAVAK – hodanje i mobilnost

                    1. Brzo hodanje 30 min (puls 110–125, možeš pričati bez zadihanosti)
                    2. Mobilnost (15 min):
                       • Mačka–krava 2×10
                       • Otvaranje grudnog koša u ležanju na boku 2×8 po strani
                       • Rotacije kukova 90/90 2×8 po strani
                       • Duboki čučanj uz oslonac 3×30 s
                       • Istezanje zadnje lože s trakom 2×40 s po nozi

                    Cilj je oporavak, ne umor. Ako si iscrpljen nakon posla, samo prošetaj.
                    """,
                NutritionMinutes: 40,
                Nutrition: """
                    Dan odmora: ~2.500 kcal | proteini 180 g · ugljikohidrati 250 g · masti 85 g

                    Doručak (08:00): omlet od 3 jaja sa špinatom i 30 g sira, 1 kriška integralnog hljeba
                    Užina (11:00): 1 jabuka i 30 g badema
                    Ručak (14:00): 180 g pastrmke, 200 g krompira, brokula na pari
                    Užina (17:00): 200 g grčkog jogurta s 30 g ovsenih pahuljica i cimetom
                    Večera (20:00): 200 g pilećih prsa, velika salata s pola avokada, 1 integralna tortilja

                    Voda: 3 l. Manje ugljikohidrata jer nema treninga u teretani, proteini ostaju isti.
                    """),
            new DaySpec(DayOfWeek: 4, TrainingMinutes: 75,
                Training: """
                    GORNJI DIO B – leđa i biceps

                    Zagrijavanje (10 min):
                    • 5 min veslanja na ergometru
                    • Vis na šipci 2×20 s i skapularni zgibovi 2×8
                    • Veslanje s gumom 2×15

                    Glavni dio:
                    1. Zgibovi širokim hvatom – 4×6–8 s dodatnih 5 kg
                    2. Veslanje šipkom u pretklonu – 4×8 (75 kg), odmor 2 min
                    3. Potisak bučicama iznad glave sjedeći – 3×10 (22 kg)
                    4. Jednoručno veslanje bučicom – 3×10 po ruci (32 kg)
                    5. Face pull na sajli – 3×15
                    6. Pregib bučicama sa supinacijom – 3×10–12 (14 kg)
                    7. Čekić pregib – 2×12 (14 kg)

                    Hlađenje (8 min):
                    • Vis na šipci 2×30 s
                    • Istezanje leđa i bicepsa, disanje stomakom 2 min
                    """,
                NutritionMinutes: 45,
                Nutrition: """
                    Dnevni cilj: ~2.900 kcal | proteini 185 g · ugljikohidrati 340 g · masti 85 g

                    Doručak (07:30): 90 g ovsenih pahuljica, 30 g proteina u prahu, 1 banana, 15 g chia sjemenki
                    Užina (10:30): sendvič od integralnog hljeba s 80 g ćureće šunke i paradajzom
                    Ručak (13:30): 200 g ćurećih prsa, 250 g heljde ili bulgura, salata od cvekle
                    Prije treninga (17:00): 2 rižina krekera s medom i 1 banana
                    Poslije treninga (19:45): 500 ml čokoladnog mlijeka
                    Večera (20:30): 150 g tune iz konzerve (ocijeđene), 250 g integralne tjestenine u paradajz sosu

                    Voda: najmanje 3 l.
                    """),
            new DaySpec(DayOfWeek: 5, TrainingMinutes: 80,
                Training: """
                    DONJI DIO B – mrtvo dizanje i zadnja loža

                    Zagrijavanje (12 min):
                    • 5 min bicikla
                    • Glute bridge 2×15 i bird dog 2×8 po strani
                    • Pregib u kukovima sa štapom na leđima 2×10
                    • Zagrijavanje šipke: 60 kg × 5, 100 kg × 3, 130 kg × 1

                    Glavni dio:
                    1. Mrtvo dizanje – 4×4 (145 kg), odmor 3 min; leđa neutralna, šipka uz noge
                    2. Prednji čučanj – 3×6 (70 kg)
                    3. Hip thrust sa šipkom – 3×10 (120 kg), pauza 1 s gore
                    4. Iskoraci u hodu s bučicama – 3×12 koraka po nozi (2×16 kg)
                    5. Nordijski pregib uz pomoć gume – 3×5
                    6. Plank s dodirom ramena – 3×40 s

                    Hlađenje (8 min):
                    • Istezanje zadnje lože i gluteusa, 2×40 s po strani
                    • Valjak za leđa i gluteus, 3 min
                    """,
                NutritionMinutes: 50,
                Nutrition: """
                    Dnevni cilj: ~3.000 kcal | proteini 185 g · ugljikohidrati 360 g · masti 85 g

                    Doručak (07:30): 3 jaja, 2 kriške hljeba, pola avokada, 1 narandža
                    Užina (10:30): smoothie od 250 ml mlijeka, 30 g proteina, 1 banane i 40 g ovsenih pahuljica
                    Ručak (13:30): bosanski lonac s 200 g junetine i povrćem, 2 kriške hljeba
                    Prije treninga (17:00): 150 g grčkog jogurta i 1 banana
                    Poslije treninga (20:00): 30 g proteina u prahu i 40 g suhih smokava
                    Večera (20:45): 200 g pilećih prsa, 300 g riže, zelena salata

                    Voda: najmanje 3 l. Petak je najteži dan u sedmici – jedi na vrijeme.
                    """),
            new DaySpec(DayOfWeek: 6, TrainingMinutes: 60,
                Training: """
                    RAMENA, RUKE I KONDICIJA

                    Zagrijavanje (8 min):
                    • 3 min preskakanja užeta
                    • Rotacije ramena s gumom 2×15 i face pull s gumom 2×15

                    Glavni dio – supersetovi (odmor 60–90 s nakon svakog para):
                    A1. Arnold potisak – 3×10 (18 kg)
                    A2. Zgibovi uskim hvatom – 3×8
                    B1. Odručenje u pretklonu – 3×15 (8 kg)
                    B2. Francuski potisak EZ šipkom – 3×10 (30 kg)
                    C1. Pregib EZ šipkom – 3×10 (30 kg)
                    C2. Propadanja između klupa – 3×15

                    Kondicioni finiš – EMOM 12 min:
                    • neparna minuta: 12 zamaha kettlebellom (24 kg)
                    • parna minuta: 10 burpeesa

                    Hlađenje: istezanje ramena i ruku, 5 min.
                    """,
                NutritionMinutes: 40,
                Nutrition: """
                    Dnevni cilj: ~3.050 kcal | proteini 180 g · ugljikohidrati 390 g · masti 80 g
                    Izmjena (verzija 2): +40 g ugljikohidrata i dodatni obrok prije treninga.

                    Doručak (09:00): palačinke od 80 g ovsenih pahuljica, 2 jaja i 1 banane, 20 g meda
                    Užina (12:00): 200 g grčkog jogurta i 40 g granole
                    Ručak (14:00): 200 g pilećih prsa, 250 g pečenog krompira, salata
                    Prije treninga (16:30): 2 kriške hljeba s 30 g džema i 1 banana
                    Poslije treninga (18:30): 30 g proteina u prahu i 500 ml mlijeka
                    Večera (20:30): 200 g oslića, 200 g riže, povrće na žaru

                    Subota je slobodnija – jedan obrok možeš pojesti vani, samo pazi da ima dovoljno proteina.
                    """),
            new DaySpec(DayOfWeek: 7, TrainingMinutes: 20,
                Training: """
                    ODMOR – regeneracija

                    • Bez treninga u teretani.
                    • Po želji lagana šetnja 30–45 min (Wilsonovo šetalište, Vrelo Bosne).
                    • Istezanje cijelog tijela 20 min: prsa, leđa, kukovi, zadnja loža i listovi – po 2×40 s.
                    • Spavanje 8 sati; ako je sedmica bila teška, dozvoli si i 9.
                    """,
                NutritionMinutes: 90,
                Nutrition: """
                    Dan odmora: ~2.500 kcal | proteini 180 g · ugljikohidrati 250 g · masti 85 g

                    Doručak (09:30): 3 jaja na oko, 2 kriške integralnog hljeba, 30 g kajmaka, paradajz
                    Ručak (13:00): 200 g pečene piletine, 200 g krompira, zelena salata s maslinovim uljem
                    Užina (16:30): 250 g kravljeg sira s voćem
                    Večera (19:30): 150 g tune, integralna tortilja i svježe povrće

                    Priprema obroka: skuhaj 1 kg pilećih prsa i 800 g riže za ponedjeljak, utorak i srijedu.
                    """)
        ]);

    // ================================================================ Amir → Amina (mobile), kalistenika, mršavljenje

    private static PlanContent MobileAmirPlan() => new(
        "Ne moraš biti savršena, samo dosljedna. Svaki trening te vodi korak bliže prvom zgibu!",
        [
            new DaySpec(DayOfWeek: 1, TrainingMinutes: 50,
                Training: """
                    GORNJI DIO TIJELA – guranje i povlačenje

                    Zagrijavanje (8 min):
                    • 2 min jumping jacks
                    • Kruženje rukama naprijed i nazad 2×15
                    • Skapularni sklekovi 2×10
                    • Mačka–krava 1×10

                    Glavni dio (odmor 60–90 s između serija):
                    1. Sklekovi s klupe ili stola – 4×8–10, spuštanje 3 s
                    2. Australijski zgibovi na niskoj šipci – 4×8, pauza 1 s gore
                    3. Negativni zgibovi – 4×3 (skoči u gornju poziciju, spuštaj se 5 s)
                    4. Propadanja na klupi sa savijenim koljenima – 3×10
                    5. Pike sklekovi s koljena – 3×8
                    6. Mrtvi vis na šipci – 3×20–30 s

                    Core (6 min):
                    • Hollow body hold 3×20 s
                    • Plank na podlakticama 3×40 s

                    Istezanje (5 min): prsa uz zid, triceps iznad glave, položaj djeteta 1 min.
                    """,
                NutritionMinutes: 40,
                Nutrition: """
                    Dnevni cilj: ~1.650 kcal | proteini 120 g · ugljikohidrati 160 g · masti 55 g

                    Doručak (07:00): 50 g ovsenih pahuljica s 200 ml mlijeka, 100 g borovnica, 10 g chia sjemenki
                    Užina (10:00): 150 g grčkog jogurta (2% masti) i 1 jabuka
                    Ručak (13:00): 150 g pilećih prsa, 120 g kuhane kinoe, velika salata s 5 ml maslinovog ulja
                    Užina (16:30): 1 kuhano jaje i 2 rižina krekera
                    Večera (19:30): 150 g bijele ribe, 200 g povrća na pari, 100 g krompira

                    Voda: 2,5 l. Kafa bez šećera, najviše dvije dnevno.
                    """),
            new DaySpec(DayOfWeek: 2, TrainingMinutes: 45,
                Training: """
                    AKTIVNI OPORAVAK – hodanje i mobilnost

                    1. Brzo hodanje 35 min (cilj 4.500–5.000 koraka), idealno na svježem zraku
                    2. Mobilnost (10 min):
                       • Kruženje kukovima 2×10 u svaki smjer
                       • Iskorak s rotacijom trupa 2×6 po strani
                       • Istezanje pregibača kuka 2×30 s po strani
                       • Mačka–krava 2×10

                    Ne preskači ovaj dan – troši kalorije i ubrzava oporavak za sutrašnji trening.
                    """,
                NutritionMinutes: 30,
                Nutrition: """
                    Dnevni cilj: ~1.550 kcal | proteini 120 g · ugljikohidrati 140 g · masti 55 g

                    Doručak (07:00): omlet od 2 jaja i 100 g bjelanaca sa špinatom, 1 kriška integralnog hljeba
                    Užina (10:00): 1 kruška i 15 g badema
                    Ručak (13:00): 150 g ćurećih prsa, 150 g pečenog batata, salata od kupusa i mrkve
                    Užina (16:30): 200 ml kefira
                    Večera (19:30): salata s 120 g tune, paradajzom, krastavcem i 50 g slanutka

                    Voda: 2,5 l.
                    """),
            new DaySpec(DayOfWeek: 3, TrainingMinutes: 50,
                Training: """
                    DONJI DIO TIJELA I CORE

                    Zagrijavanje (8 min):
                    • 2 min marširanja u mjestu s visokim koljenima
                    • Čučanj s pauzom na dnu 2×8
                    • Glute bridge 2×12
                    • Iskorak unazad 2×6 po nozi

                    Glavni dio – kružno, 4 kruga, odmor 90 s između krugova:
                    1. Čučanj s vlastitom težinom, spuštanje 3 s – 15
                    2. Bugarski čučanj (zadnja noga na klupi) – 8 po nozi
                    3. Glute bridge na jednoj nozi – 10 po nozi
                    4. Iskoraci u hodu – 20 koraka
                    5. Podizanje na prste na stepenici – 15

                    Core (3 kruga):
                    • Obrnuti trbušnjaci 12
                    • Bočni plank 20 s po strani
                    • Dead bug 10 po strani

                    Istezanje (5 min): kvadriceps, zadnja loža i gluteus, po 30 s.
                    """,
                NutritionMinutes: 40,
                Nutrition: """
                    Dnevni cilj: ~1.650 kcal | proteini 120 g · ugljikohidrati 165 g · masti 55 g

                    Doručak (07:00): 150 g grčkog jogurta, 40 g granole bez šećera, 1 banana
                    Užina (10:00): 2 rižina krekera s 30 g posnog sira i paradajzom
                    Ručak (13:00): 150 g junećeg gulaša s povrćem, 100 g kuhane riže
                    Užina (16:30): proteinski šejk (25 g proteina u vodi) i 1 jabuka
                    Večera (19:30): 2 jaja i 100 g bjelanaca, 200 g grilovanih tikvica i paprike, 1 kriška hljeba

                    Voda: 2,5 l. Poslije treninga obavezno protein.
                    """),
            new DaySpec(DayOfWeek: 4, TrainingMinutes: 35,
                Training: """
                    KONDICIONI KRUŽNI TRENING (HIIT)

                    Zagrijavanje (6 min):
                    • 2 min laganog preskakanja užeta ili skipa u mjestu
                    • Čučanj s podizanjem ruku 10, iskorak s rotacijom 8, sklek s koljena 8

                    Glavni dio – Tabata blokovi (20 s rada, 10 s odmora, 8 rundi; 1 min odmora između blokova):
                    Blok 1: jumping jacks / čučanj sa skokom (naizmjenično)
                    Blok 2: mountain climbers / sklek s koljena
                    Blok 3: burpee bez skoka / plank jack
                    Blok 4: preskakanje užeta / hollow body hold

                    Hlađenje (5 min): lagano hodanje i istezanje nogu.

                    Intenzitet: na kraju svakog bloka trebaš biti zadihana, ali tehnika mora ostati čista.
                    """,
                NutritionMinutes: 35,
                Nutrition: """
                    Dnevni cilj: ~1.600 kcal | proteini 120 g · ugljikohidrati 150 g · masti 55 g

                    Doručak (07:00): palačinka od 40 g ovsenih pahuljica, 1 jajeta i 1 banane
                    Užina (10:00): 150 g posnog kravljeg sira s krastavcem
                    Ručak (13:00): 150 g pilećih prsa u integralnoj tortilji s povrćem i 30 g humusa
                    Užina (16:30): 1 narandža i 15 g oraha
                    Večera (19:30): 150 g lososa, 150 g brokule, 80 g kuhane kinoe

                    Voda: 2,5–3 l. Na HIIT dan ne jedi obilno 2 sata prije treninga.
                    """),
            new DaySpec(DayOfWeek: 5, TrainingMinutes: 55,
                Training: """
                    CIJELO TIJELO – vještine i snaga

                    Zagrijavanje (8 min):
                    • Kruženje zglobovima od vrata do skočnih zglobova
                    • Skapularni zgibovi 2×8
                    • Sklekovi s klupe 1×10

                    Vještine (15 min):
                    1. Zgib uz pomoć elastične gume – 5×3–5, odmor 2 min
                    2. Puni sklekovi s poda – 5 serija do tehničkog otkaza, odmor 90 s (broj upiši u bilješku)

                    Snaga – 3 kruga (20 min):
                    • Australijski zgibovi 10
                    • Sumo čučanj s pauzom 15
                    • Sklekovi s klupe 12
                    • Iskoraci unazad 10 po nozi
                    • Superman držanje 20 s

                    Core (5 min):
                    • Tuck hold na paralelama ili između dvije stolice 4×10 s
                    • Plank 2×45 s

                    Istezanje 5 min.
                    """,
                NutritionMinutes: 40,
                Nutrition: """
                    Dnevni cilj: ~1.700 kcal | proteini 120 g · ugljikohidrati 170 g · masti 55 g

                    Doručak (07:00): 2 jaja, 1 kriška integralnog hljeba, 30 g avokada, paradajz
                    Užina (10:00): 150 g grčkog jogurta i 100 g jagoda
                    Ručak (13:00): 150 g ćufti od junećeg mesa u paradajz sosu, 100 g pirea od krompira, salata
                    Užina (16:30): proteinski šejk (25 g) i 1 banana
                    Večera (19:30): salata sa 120 g pilećih prsa, 30 g feta sira i maslinama

                    Voda: 2,5 l. Petkom navečer dozvoljen je jedan mali desert (do 200 kcal).
                    """),
            new DaySpec(DayOfWeek: 6, TrainingMinutes: 80,
                Training: """
                    DUGA AKTIVNOST NA OTVORENOM

                    Izaberi jednu aktivnost (70–90 min, lagan do umjeren tempo):
                    • Planinarenje (npr. Trebević ili Skakavac)
                    • Vožnja bicikla (Wilsonovo šetalište – Vrelo Bosne i nazad)
                    • Brzo hodanje 8–10 km

                    Puls: 120–140 otkucaja u minuti – trebaš moći pričati u punim rečenicama.

                    Nakon aktivnosti (10 min): istezanje listova, zadnje lože i kukova.
                    """,
                NutritionMinutes: 30,
                Nutrition: """
                    Dnevni cilj: ~1.750 kcal | proteini 115 g · ugljikohidrati 190 g · masti 55 g

                    Doručak (08:00): 50 g ovsenih pahuljica s mlijekom, 1 banana, 10 g kikiriki putera
                    Za put: sendvič od integralnog hljeba sa 60 g ćureće šunke, 1 jabuka, 1,5 l vode
                    Ručak (14:30): 150 g pilećih prsa sa žara, 150 g krompira, salata
                    Užina (17:00): 200 ml kefira
                    Večera (20:00): 2 jaja i salata od paradajza i krastavca

                    Subotom je u redu pojesti malo više ugljikohidrata – troši se puno energije.
                    """),
            new DaySpec(DayOfWeek: 7, TrainingMinutes: 15,
                Training: """
                    ODMOR – istezanje i planiranje

                    • Bez treninga – tijelo napreduje dok se odmara.
                    • Istezanje cijelog tijela 15 min (po 30–40 s): prsa, ramena, leđa, kukovi, zadnja loža, listovi.
                    • Jednom sedmično izmjeri obim struka, uvijek nedjeljom ujutro natašte.
                    • Isplaniraj obroke za narednu sedmicu i napravi spisak za kupovinu.
                    """,
                NutritionMinutes: 60,
                Nutrition: """
                    Dan odmora: ~1.500 kcal | proteini 115 g · ugljikohidrati 130 g · masti 55 g

                    Doručak (09:00): omlet od 2 jaja s povrćem i 30 g sira
                    Ručak (13:00): 150 g pečene piletine, 200 g povrća iz rerne
                    Užina (16:00): 150 g grčkog jogurta s cimetom i 10 g oraha
                    Večera (19:00): krem čorba od brokule i 100 g tune

                    Priprema obroka: ispeci piletinu, skuhaj kinou i nareži povrće za ponedjeljak i utorak.
                    """)
        ]);

    // ================================================================ šablon: hibridni trening (Lejla, Kenan)

    private sealed record HybridProfile
    {
        public required string Quote { get; init; }
        public required string Squat { get; init; }
        public required string RomanianDeadlift { get; init; }
        public required string BenchPress { get; init; }
        public required string PullUps { get; init; }
        public required string DumbbellRow { get; init; }
        public required string Kettlebell { get; init; }
        public required string WallBall { get; init; }
        public required string Farmer { get; init; }
        public required string RunFinisher { get; init; }
        public required string Intervals { get; init; }
        public required string EasyRun { get; init; }
        public required int EasyRunMinutes { get; init; }
        public required string LongSession { get; init; }
        public required int LongSessionMinutes { get; init; }
        public required NutritionProfile Nutrition { get; init; }
    }

    private static PlanContent HybridWeek(HybridProfile p) => new(p.Quote,
    [
        new DaySpec(1, 70, $"""
            SNAGA – DONJI DIO I KRATKO TRČANJE

            Zagrijavanje (10 min):
            • 5 min veslanja laganim tempom
            • Glute bridge 2×12 i bočni hod s trakom 2×10 koraka
            • Goblet čučanj 2×8 s laganim teretom

            Glavni dio:
            1. Čučanj sa šipkom – {p.Squat}, odmor 2–3 min
            2. Rumunsko mrtvo dizanje – {p.RomanianDeadlift}, odmor 2 min
            3. Iskoraci unazad s bučicama – 3×10 po nozi
            4. Zamasi kettlebellom – 3×15 ({p.Kettlebell})
            5. Plank – 3×40 s

            Završetak: {p.RunFinisher}.

            Hlađenje (5 min): istezanje kvadricepsa, zadnje lože i listova.
            """,
            45, TemplateNutrition(p.Nutrition, MealDay.Training, 0)),
        new DaySpec(2, 50, $"""
            TRČANJE – INTERVALI

            Zagrijavanje (12 min):
            • 10 min laganog trčanja
            • Dinamičko istezanje: zamasi nogama, visoka koljena, zabacivanje peta
            • 4 ubrzanja po 20 s

            Glavni dio:
            {p.Intervals}

            Hlađenje (8 min): 5 min laganog trčanja ili hodanja, istezanje listova i pregibača kuka.

            Svi intervali trebaju biti jednako brzi – zadnji ne smije biti sporiji od prvog.
            """,
            40, TemplateNutrition(p.Nutrition, MealDay.Training, 1)),
        new DaySpec(3, 40, """
            AKTIVNI OPORAVAK

            • 30 min laganog hodanja ili vožnje bicikla (puls ispod 120)
            • Mobilnost 10 min: rotacije kukova 90/90 2×8, mačka–krava 2×10, otvaranje grudnog koša 2×8 po strani
            • Valjak: bedra, listovi i leđa, po 60 s

            Cilj je da se sutra osjećaš svježe – ne pretjeruj.
            """,
            30, TemplateNutrition(p.Nutrition, MealDay.Rest, 4)),
        new DaySpec(4, 65, $"""
            SNAGA – GORNJI DIO I KRUŽNI TRENING

            Zagrijavanje (8 min):
            • 3 min veslanja
            • Rotacije ramena s gumom 2×15
            • Sklekovi 1×10 i skapularni zgibovi 1×8

            Glavni dio:
            1. Bench press – {p.BenchPress}, odmor 2 min
            2. Zgibovi – {p.PullUps}
            3. Jednoručno veslanje bučicom – {p.DumbbellRow}
            4. Potisak bučicama iznad glave – 3×10

            Kružni finiš (3 kruga, 2 min odmora između krugova):
            • 250 m veslanja
            • 12 wall ball izbačaja ({p.WallBall})
            • 10 burpeesa
            • 40 m farmerskog hoda ({p.Farmer})

            Hlađenje: istezanje prsa, leđa i ramena, 5 min.
            """,
            45, TemplateNutrition(p.Nutrition, MealDay.Training, 2)),
        new DaySpec(5, p.EasyRunMinutes, $"""
            TRČANJE – AEROBNA BAZA (ZONA 2)

            Zagrijavanje: 5 min brzog hodanja i dinamičko istezanje.

            Glavni dio:
            {p.EasyRun}

            Tempo je lagan – trebaš moći pričati u punim rečenicama. Ovaj trening gradi izdržljivost i ubrzava oporavak.

            Nakon trčanja: plank 3×30 s, bočni plank 2×20 s po strani i istezanje 5 min.
            """,
            40, TemplateNutrition(p.Nutrition, MealDay.Training, 3)),
        new DaySpec(6, p.LongSessionMinutes, $"""
            DUGI HIBRIDNI TRENING

            Zagrijavanje (10 min): lagano trčanje, dinamičko istezanje, 10 čučnjeva i 10 sklekova.

            Glavni dio:
            {p.LongSession}

            Hlađenje (10 min): hodanje, istezanje cijelog tijela i valjak.
            """,
            50, TemplateNutrition(p.Nutrition, MealDay.Long, 7)),
        new DaySpec(7, 20, """
            ODMOR

            • Bez strukturiranog treninga.
            • Šetnja 30–45 min na svježem zraku i istezanje cijelog tijela 15–20 min.
            • Spavanje najmanje 8 sati – oporavak je dio plana.
            • Ujutro izmjeri puls u mirovanju; ako je 5 ili više otkucaja viši nego inače, javi mi se.
            """,
            60, TemplateNutrition(p.Nutrition, MealDay.Rest, 5))
    ]);

    /// <summary>Tarik kod Lejle: održavanje snage uz skidanje masti i bolju kondiciju.</summary>
    private static readonly HybridProfile TarikHybridProfile = new()
    {
        Quote = "Budi jači od svojih izgovora.",
        Squat = "4×6 (90 kg)",
        RomanianDeadlift = "3×8 (90 kg)",
        BenchPress = "4×8 (72,5 kg)",
        PullUps = "4×6",
        DumbbellRow = "3×10 po ruci (30 kg)",
        Kettlebell = "24 kg",
        WallBall = "9 kg",
        Farmer = "2×28 kg",
        RunFinisher = "12 min laganog trčanja na traci (tempo oko 6:30 min/km)",
        Intervals = "6×400 m u tempu 5:10 min/km, između intervala 90 s hodanja",
        EasyRun = "40 min laganog trčanja, puls 135–145 (tempo oko 6:40 min/km)",
        EasyRunMinutes = 50,
        LongSession = """
            1. 5 km trčanja laganim tempom (oko 6:30 min/km)
            2. 3 runde bez pauze između vježbi, 2 min odmora između rundi:
               • 20 zamaha kettlebellom (24 kg)
               • 15 goblet čučnjeva (24 kg)
               • 10 sklekova
               • 500 m veslanja
            """,
        LongSessionMinutes = 85,
        Nutrition = new NutritionProfile(Kcal: 2500, Protein: 180, Carbs: 260, Fat: 80, Portion: 1.05, Water: "najmanje 3 l")
    };

    /// <summary>Nedim kod Kenana: priprema za polumaraton ispod 1:40 uz održavanje snage.</summary>
    private static readonly HybridProfile NedimHybridProfile = new()
    {
        Quote = "Brzina se gradi strpljenjem. Vjeruj procesu i drži tempo.",
        Squat = "4×5 (100 kg)",
        RomanianDeadlift = "3×6 (100 kg)",
        BenchPress = "4×6 (80 kg)",
        PullUps = "4×8 s dodatnih 5 kg",
        DumbbellRow = "3×10 po ruci (32 kg)",
        Kettlebell = "28 kg",
        WallBall = "9 kg",
        Farmer = "2×32 kg",
        RunFinisher = "20 min trčanja u zoni 2 (tempo oko 5:40 min/km)",
        Intervals = "5×1.000 m u tempu 4:25 min/km (ciljni tempo polumaratona je 4:40), između intervala 2 min laganog trčanja",
        EasyRun = "60 min trčanja u zoni 2, puls 140–150 (tempo 5:30–5:45 min/km); na kraju 6 ubrzanja po 20 s",
        EasyRunMinutes = 70,
        LongSession = """
            Dugo trčanje 16–18 km:
            • prvih 12 km u tempu oko 5:30 min/km
            • zadnjih 4–6 km u ciljanom tempu polumaratona (4:40 min/km)
            • svakih 5 km popij 150 ml vode s elektrolitima

            Poslije trčanja: 3×12 iskoraka unazad bez tereta i 3×15 podizanja na prste.
            """,
        LongSessionMinutes = 110,
        Nutrition = new NutritionProfile(Kcal: 2800, Protein: 160, Carbs: 380, Fat: 75, Portion: 1.15, Water: "3–3,5 l, a na dane dugih trčanja i više")
    };

    /// <summary>Lamija kod Lejle: priprema za prvu Hyrox utrku.</summary>
    private static readonly HybridProfile LamijaHybridProfile = new()
    {
        Quote = "Hyrox ne pobjeđuje najjači, nego najuporniji. 🔥",
        Squat = "3×8 (40 kg)",
        RomanianDeadlift = "3×10 (40 kg)",
        BenchPress = "3×8 (30 kg)",
        PullUps = "3×6 uz pomoć elastične gume",
        DumbbellRow = "3×10 po ruci (12 kg)",
        Kettlebell = "12 kg",
        WallBall = "4 kg",
        Farmer = "2×16 kg",
        RunFinisher = "10 min laganog trčanja (tempo oko 7:00 min/km)",
        Intervals = "8×200 m u tempu 5:15 min/km, između intervala 60 s hodanja",
        EasyRun = "35 min laganog trčanja, puls 140–150 (tempo oko 6:50 min/km)",
        EasyRunMinutes = 45,
        LongSession = """
            Hyrox simulacija – 4 runde, u svakoj 1 km trčanja umjerenim tempom i jedna stanica:
            1) 500 m veslanja
            2) 20 burpee skokova u dalj
            3) 40 m farmerskog hoda (2×16 kg)
            4) 30 wall ball izbačaja (4 kg)
            Odmor 2 min između rundi. Cilj: završiti sve runde bez hodanja.
            """,
        LongSessionMinutes = 75,
        Nutrition = new NutritionProfile(Kcal: 2000, Protein: 110, Carbs: 240, Fat: 65, Portion: 0.8, Water: "najmanje 2,5 l")
    };

    // ================================================================ šablon: trening s utezima, cijelo tijelo 3× sedmično (Haris)

    private sealed record StrengthProfile
    {
        public required string Quote { get; init; }
        public required string GobletSquat { get; init; }
        public required string DumbbellBench { get; init; }
        public required string LatPulldown { get; init; }
        public required string RomanianDeadlift { get; init; }
        public required string LegPress { get; init; }
        public required string HipThrust { get; init; }
        public required string OverheadPress { get; init; }
        public required string CableRow { get; init; }
        public required string KettlebellDeadlift { get; init; }
        public required string InclinePress { get; init; }
        public required string SingleArmRow { get; init; }
        public required string Farmer { get; init; }
        public required string Cardio { get; init; }
        public required NutritionProfile Nutrition { get; init; }
    }

    private static PlanContent StrengthWeek(StrengthProfile p) => new(p.Quote,
    [
        new DaySpec(1, 60, $"""
            CIJELO TIJELO A

            Zagrijavanje (10 min):
            • 5 min bicikla ili eliptičnog trenažera
            • Kruženje kukovima i ramenima, 10 u svaki smjer
            • Glute bridge 2×12 i sklekovi uz klupu 1×10

            Glavni dio (odmor 90 s između serija):
            1. Goblet čučanj – {p.GobletSquat}
            2. Potisak bučicama na ravnoj klupi – {p.DumbbellBench}
            3. Lat povlačenje na sajli – {p.LatPulldown}
            4. Rumunsko mrtvo dizanje bučicama – {p.RomanianDeadlift}
            5. Plank na podlakticama – 3×30–40 s

            Hlađenje (5 min): istezanje nogu, prsa i leđa.

            Tehnika je na prvom mjestu: kad sve serije uradiš sa zadanim brojem ponavljanja i dobrom formom, sljedeće sedmice povećaj teret.
            """,
            40, TemplateNutrition(p.Nutrition, MealDay.Training, 0)),
        new DaySpec(2, 40, $"""
            KARDIO I MOBILNOST

            1. {p.Cardio}
            2. Mobilnost (10 min):
               • Mačka–krava 2×10
               • Istezanje pregibača kuka u iskoraku 2×30 s po strani
               • Rotacije grudnog koša u ležanju na boku 2×8 po strani
               • Duboki čučanj uz oslonac 3×20 s
            """,
            30, TemplateNutrition(p.Nutrition, MealDay.Rest, 4)),
        new DaySpec(3, 60, $"""
            CIJELO TIJELO B

            Zagrijavanje (10 min):
            • 5 min hodanja na traci s nagibom
            • Bočni hod s trakom 2×10 koraka po strani
            • Čučanj bez tereta 2×10

            Glavni dio (odmor 90 s između serija):
            1. Potisak nogama – {p.LegPress}
            2. Hip thrust sa šipkom – {p.HipThrust}
            3. Potisak bučicama iznad glave sjedeći – {p.OverheadPress}
            4. Veslanje na sajli sjedeći – {p.CableRow}
            5. Iskoraci u hodu – 3×10 po nozi
            6. Dead bug – 3×8 po strani

            Hlađenje (5 min): istezanje gluteusa, kvadricepsa i ramena.
            """,
            40, TemplateNutrition(p.Nutrition, MealDay.Training, 1)),
        new DaySpec(4, 30, """
            AKTIVNI OPORAVAK

            • Šetnja 30 min laganim tempom
            • Istezanje cijelog tijela 10 min: po 30 s za listove, zadnju ložu, kukove, prsa i leđa
            • Ako se osjećaš umorno, dovoljno je samo prošetati.
            """,
            30, TemplateNutrition(p.Nutrition, MealDay.Rest, 6)),
        new DaySpec(5, 60, $"""
            CIJELO TIJELO C

            Zagrijavanje (10 min):
            • 5 min veslanja
            • Pregib u kukovima sa štapom na leđima 2×10
            • Rotacije ramena s gumom 2×15

            Glavni dio (odmor 90 s između serija):
            1. Mrtvo dizanje s kettlebellom – {p.KettlebellDeadlift}
            2. Potisak bučicama na kosoj klupi – {p.InclinePress}
            3. Jednoručno veslanje bučicom – {p.SingleArmRow}
            4. Split čučanj – 3×8 po nozi
            5. Farmerski hod – {p.Farmer}

            Hlađenje (5 min): istezanje zadnje lože, leđa i ramena.
            """,
            40, TemplateNutrition(p.Nutrition, MealDay.Training, 2)),
        new DaySpec(6, 50, """
            KARDIO U PRIRODI

            • 45–60 min brzog hodanja, planinarenja ili vožnje bicikla
            • Puls 120–140 – možeš pričati, ali osjećaš napor
            • Nakon aktivnosti istezanje listova, zadnje lože i kukova, 10 min
            """,
            40, TemplateNutrition(p.Nutrition, MealDay.Training, 3)),
        new DaySpec(7, 15, """
            ODMOR

            • Bez treninga – mišići napreduju dok se odmaraju.
            • Lagana šetnja po želji.
            • Pripremi obroke za naredna dva dana i pogledaj plan za sljedeću sedmicu.
            """,
            60, TemplateNutrition(p.Nutrition, MealDay.Rest, 5))
    ]);

    /// <summary>Belma kod Harisa: početnica, mršavljenje nakon porodiljskog.</summary>
    private static readonly StrengthProfile BelmaStrengthProfile = new()
    {
        Quote = "Ne žuri. Svaki trening je poklon tebi i tvojoj porodici.",
        GobletSquat = "3×12 (12 kg)",
        DumbbellBench = "3×10 (2×8 kg)",
        LatPulldown = "3×12 (30 kg)",
        RomanianDeadlift = "3×10 (2×10 kg)",
        LegPress = "3×12 (60 kg)",
        HipThrust = "3×12 (40 kg)",
        OverheadPress = "3×10 (2×6 kg)",
        CableRow = "3×12 (25 kg)",
        KettlebellDeadlift = "3×10 (16 kg)",
        InclinePress = "3×10 (2×8 kg)",
        SingleArmRow = "3×10 po ruci (10 kg)",
        Farmer = "3×30 m (2×12 kg)",
        Cardio = "30 min hodanja na traci s nagibom 8–10% (5 km/h) ili šetnja s kolicima uzbrdo",
        Nutrition = new NutritionProfile(Kcal: 1800, Protein: 130, Carbs: 170, Fat: 60, Portion: 0.8, Water: "najmanje 2,5 l")
    };

    /// <summary>Adna kod Harisa: rekreativka, povećanje mišićne mase (nogu i gluteusa).</summary>
    private static readonly StrengthProfile AdnaStrengthProfile = new()
    {
        Quote = "Mala poboljšanja svaki dan vode do velikih rezultata.",
        GobletSquat = "4×10 (16 kg)",
        DumbbellBench = "4×8 (2×12 kg)",
        LatPulldown = "4×10 (35 kg)",
        RomanianDeadlift = "4×8 (2×14 kg)",
        LegPress = "4×10 (90 kg)",
        HipThrust = "4×10 (60 kg)",
        OverheadPress = "3×10 (2×8 kg)",
        CableRow = "4×10 (30 kg)",
        KettlebellDeadlift = "4×8 (24 kg)",
        InclinePress = "3×10 (2×10 kg)",
        SingleArmRow = "3×10 po ruci (14 kg)",
        Farmer = "3×40 m (2×16 kg)",
        Cardio = "25 min lagane vožnje bicikla ili hodanja na traci (puls 110–130)",
        Nutrition = new NutritionProfile(Kcal: 2200, Protein: 125, Carbs: 260, Fat: 70, Portion: 0.9, Water: "najmanje 2,5 l")
    };

    // ================================================================ šablon ishrane

    private enum MealDay
    {
        Training,
        Rest,
        Long
    }

    /// <param name="Portion">Množilac gramaža u jelovnicima (1,0 ≈ 2.400 kcal).</param>
    private sealed record NutritionProfile(int Kcal, int Protein, int Carbs, int Fat, double Portion, string Water);

    /// <summary>
    /// Dnevni jelovnik: makroi zavise od vrste dana, a gramaže se skaliraju prema profilu.
    /// Jelovnici 0–3 su za dane treninga, 4–6 za dane odmora, 7 za dan dugog treninga.
    /// </summary>
    private static string TemplateNutrition(NutritionProfile n, MealDay day, int menu)
    {
        string G(int grams) => (Math.Round(grams * n.Portion / 5) * 5).ToString("0", CultureInfo.InvariantCulture);

        var (title, kcal, carbs, tip) = day switch
        {
            MealDay.Rest => ("Dan odmora", n.Kcal - 300, n.Carbs - 75,
                "Manje ugljikohidrata jer nema napornog treninga, proteini ostaju isti."),
            MealDay.Long => ("Dan dugog treninga", n.Kcal + 200, n.Carbs + 50,
                "Tokom dugog treninga pij oko 500 ml tečnosti na sat."),
            _ => ("Dnevni cilj", n.Kcal, n.Carbs,
                "Obrok s ugljikohidratima pojedi 1,5–2 sata prije treninga.")
        };

        var meals = menu switch
        {
            0 => $"""
                Doručak (07:30): {G(80)} g ovsenih pahuljica kuhanih u {G(250)} ml mlijeka, 1 banana, {G(15)} g kikiriki putera
                Užina (10:30): {G(200)} g grčkog jogurta, {G(20)} g oraha, šaka borovnica
                Ručak (13:30): {G(180)} g pilećih prsa, {G(200)} g kuhane riže, miješana salata s maslinovim uljem
                Užina (17:00): 2 kriške integralnog hljeba, {G(50)} g goveđeg pršuta, 1 jabuka
                Večera (20:00): {G(150)} g lososa, {G(200)} g krompira iz rerne, povrće na pari
                """,
            1 => $"""
                Doručak (07:30): kajgana od 3 jaja, 2 kriške integralnog hljeba, paradajz i krastavac
                Užina (10:30): {G(250)} g kravljeg sira, 1 kruška, 1 kašičica meda
                Ručak (13:30): {G(180)} g junećeg gulaša, {G(200)} g integralne tjestenine, salata od kupusa
                Užina (17:00): 1 banana i {G(30)} g suhih kajsija
                Večera (20:00): {G(180)} g ćurećih prsa, {G(200)} g batata iz rerne, zelena salata
                """,
            2 => $"""
                Doručak (07:30): {G(60)} g ovsenih pahuljica, {G(30)} g proteina u prahu, šaka bobičastog voća
                Užina (10:30): sendvič od integralnog hljeba s {G(80)} g ćureće šunke i paradajzom
                Ručak (13:30): {G(180)} g pilećih bataka bez kože, {G(200)} g bulgura, salata od cvekle
                Poslije treninga: {G(30)} g proteina u prahu i 1 banana
                Večera (20:00): omlet od 3 jaja, {G(150)} g krompira, grilovano povrće
                """,
            3 => $"""
                Doručak (07:30): {G(200)} g grčkog jogurta, {G(50)} g granole, 1 banana
                Užina (10:30): 2 rižina krekera s medom i {G(20)} g badema
                Ručak (13:30): {G(180)} g junećih ćufti u paradajz sosu, {G(200)} g pirea od krompira
                Užina (17:00): {G(250)} ml kefira i 1 jabuka
                Večera (20:00): {G(180)} g oslića, {G(200)} g riže, povrće na žaru
                """,
            4 => $"""
                Doručak (08:00): omlet od 3 jaja sa špinatom i {G(30)} g sira, 1 kriška integralnog hljeba
                Užina (11:00): {G(30)} g badema i 1 kruška
                Ručak (14:00): {G(180)} g pastrmke, {G(150)} g krompira, brokula na pari
                Užina (17:00): {G(250)} ml kefira
                Večera (20:00): salata s {G(150)} g tune, {G(80)} g slanutka i maslinovim uljem
                """,
            5 => $"""
                Doručak (09:30): 3 jaja na oko, 1 kriška hljeba, {G(30)} g kajmaka, paradajz
                Ručak (13:30): {G(180)} g pečene piletine, {G(150)} g krompira, zelena salata
                Užina (17:00): {G(200)} g grčkog jogurta s voćem i cimetom
                Večera (20:00): krem čorba od povrća i {G(120)} g tune
                """,
            6 => $"""
                Doručak (08:00): {G(200)} g kravljeg sira, 1 kriška integralnog hljeba, krastavac i paprika
                Užina (11:00): 1 narandža i {G(20)} g oraha
                Ručak (14:00): bosanski lonac s {G(180)} g junetine i povrćem
                Užina (17:00): {G(150)} g grčkog jogurta
                Večera (20:00): {G(150)} g pilećih prsa, velika salata s avokadom
                """,
            _ => $"""
                Doručak (07:30, 2 h prije treninga): palačinke od {G(70)} g ovsenih pahuljica, 2 jaja i 1 banane, kašičica meda
                Tokom treninga: 500 ml vode s elektrolitima, po potrebi 1 banana
                Poslije treninga: {G(30)} g proteina u prahu i {G(300)} ml mlijeka
                Ručak (14:00): {G(200)} g pilećih prsa, {G(250)} g riže, salata
                Večera (20:00): {G(180)} g lososa, {G(200)} g krompira, povrće na pari
                """
        };

        return $"""
            {title}: ~{FormatKcal(kcal)} kcal | proteini {n.Protein} g · ugljikohidrati {carbs} g · masti {n.Fat} g

            {meals}

            Voda: {n.Water}. {tip}
            """;
    }

    private static string FormatKcal(int kcal) => kcal.ToString("#,0", CultureInfo.InvariantCulture).Replace(',', '.');
}

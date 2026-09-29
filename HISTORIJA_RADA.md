# GoBeyond — historija rada i nastavak

## Trajni zahtjev korisnika (27.09.2026.)

Popraviti postojeću nefunkcionalnu implementaciju i dovršiti aplikaciju prema prijavi teme `IB210020 - RS2 Prijava teme.docx`, uz tehničke zahtjeve iz `RSII_Upute_za_izradu_seminarskog_rada_2024_25 (1).pdf`.

Način rada:
- Glavni agent raspoređuje taskove subagentima primjerenim zadatku (ne moraju biti najjači modeli).
- Glavni agent je reviewer: pregleda rad, daje ocjenu 1–10 i vraća na doradu sve ispod 8, sa konkretnim instrukcijama.
- Commitovi se rade na ime korisnika (git identitet `Mirza Rujanac <mirza.rujanac@edu.fit.ba>`), bez dodatnih co-author linija.
- Ovaj fajl čuva kontekst da korisnik ne mora ponavljati prompt.

Dokumenti su izvori zahtjeva za aplikaciju. Administrativne upute za predaju nisu nalog za objavljivanje repozitorija, slanje poruka ili predaju rada.

## Izvori zahtjeva (lokalno)

- `docs/requirements/prijava-teme.txt` — tekst prijave teme.
- `docs/requirements/upute-seminarski.txt` — tekst uputa za izradu seminarskog rada.
- `docs/requirements/mockups/*.png` — 14 skica iz prijave (imena fajlova opisuju ekran; 01–06 desktop, 07–14 mobile).
- `pravila.md` — sažetak pravila iz uputa.
- Originali: `C:\Users\mirza\Downloads\IB210020 - RS2 Prijava teme.docx`, PDF uputa (priložen u chatu).

## Sesija 1 (27.09.2026., prethodni agent)

- Početno stanje: .NET 9 API + Flutter desktop (Admin/Mentor) i mobile (Client). README/plan tvrde MVP, ali to nije dokaz funkcionalnosti.
- Uočeno: lažna potvrda plaćanja bez Stripe-a, nezaštićen webhook, RabbitMQ worker bez stvarnog consumer toka, neusaglašena konfiguracija i naziv baze.
- Započete (necommitane) izmjene u ~90 fajlova: outbox + RabbitMQ publisher, SMTP consumer, Stripe gateway, nove tabele (TrainingType, TrainingSession, UserActivity, OutboxMessage), migracija `CompleteDomain`, novi desktop/mobile ekrani.
- Korisnička izmjena `GoBeyond.API/GoBeyond.API/Properties/launchSettings.json` (IIS Express profil) — sačuvati je.

## Sesija 2 (27.09.2026., Claude Code — Opus 5.5 kao reviewer)

### Baseline provjere na početku sesije
- `dotnet build GoBeyond.sln` — prolazi (0 grešaka, 2 upozorenja u `ReviewsController.cs`).
- `flutter analyze` mobile — bez grešaka.
- `flutter analyze` desktop — 2 greške u `lib/presentation/widgets/report_documents.dart` (ne kompajlira se).
- Docker: sqlserver, rabbitmq, mailpit, email-consumer rade; `gobeyond-api-1` pao sa exit 139.

### Plan
1. Audit (7 paralelnih read-only auditora + critic) → lista nedostataka.
2. Podjela implementacije po slojevima (backend / desktop / mobile / infra) sa API ugovorom.
3. Review svakog dijela, ocjena, dorada ispod 8.
4. Integracijski smoke test (docker compose + API + oba Flutter klijenta), README, commit.

### Audit (završen)
7 auditora (Sonnet) + completeness critic, 69 nalaza. Najvažnije:
- API se ruši pri startu (PendingModelChangesWarning — migracije ne odgovaraju modelu; exit 139 u Dockeru).
- Desktop se ne kompajlira (`report_documents.dart`).
- Plan builder (skice 04/05), mobilna početna (07), meni (13), upitnik (09), historija (14) ne odgovaraju skicama.
- Izvještaj mentora bez mjesečne zarade i vremena na platformi; izvještaj klijenata ne postoji.
- Recommender nije pravi algoritam; nema "sličnih mentora".
- Nema internih poruka, sistemskih obavijesti ni automatskih obavijesti (istek, neaktivnost, izostanak plana).
- Demo (lažno) plaćanje; MentorCategory enum duplira TrainingType; login preko hardkodiranih aliasa; UI na engleskom; mrtav kod.
- Napomena: jedan auditor je (protivno uputi) vratio `GoBeyondDbContextModelSnapshot.cs` na HEAD — nebitno jer se migracije regenerišu.
Puni nalazi: scratchpad sesije (`audit-gaps.md`), nisu u repou.

### Ključne odluke (glavni agent)
- `docs/api-contract.md` je jedini izvor istine za API; odstupanja se upisuju u njegov Changelog.
- Jedna svježa migracija `InitialCreate`; baza `210020`; seed idempotentan.
- Pravi `Username` stupac; seed nalozi `admin`, `desktop`, `mentor`, `client`, `mobile` — lozinka `test`.
- Šifarnici: TrainingType, FitnessGoal, FitnessLevel, Gender (generički BaseCRUD + SearchObject, kao na nastavi); uloge su enum sa `GET /api/roles`.
- Tok saradnje: klijent plati (Stripe PaymentSheet) → zahtjev (AwaitingMentor) → mentor prihvata (IZRADI PLAN) ili odbija (Stripe refund) → Active 30 dana → Expired; produženje = nova uplata.
- Stripe bez demo moda; ključevi samo iz `.env` (korisnik mora unijeti svoje TEST ključeve). Publishable key stiže iz `create-intent` odgovora.
- Recommender: content-based, vektori osobina + kosinusna sličnost; `/api/mentors/{id}/similar`.
- Dodano zbog prijave ("Obim"): interne poruke mentor↔klijent, sistemske obavijesti admina, lifecycle obavijesti (istek, neaktivnost, izostanak plana), grafikon napretka.
- UI kompletno na bosanskom, tamna tema sa žutim akcentima (kao skice).
- Checkpoint commit `861a925` (necommitani WIP iz sesije 1) prije implementacije.

### Podjela rada (runda 1)
| Agent | Model | Odgovornost |
|---|---|---|
| backend | Opus | GoBeyond.API/** (svi projekti + testovi), docker-compose, config, migracija, seed, recommender, RabbitMQ, Stripe |
| desktop | Sonnet | UI/gobeyond_desktop — admin + mentor po skicama 01–06 |
| mobile | Sonnet | UI/gobeyond_mobile — klijent po skicama 07–14 |
| glavni agent | Opus (reviewer) | ugovor, review + ocjene, integracija, README, commitovi |

Pravila za agente: rade samo u svom folderu, bez git komandi koje mijenjaju stanje; glavni agent commituje nakon reviewa.

### Review ocjene
| Runda | Agent | Ocjena | Glavni razlozi | Ishod |
|---|---|---|---|---|
| 1 | backend | 8.5/10 | Kompletan rewrite: 93 akcije, 23 tabele (19 domenskih, 34 FK), jedna `InitialCreate` migracija, 65 xUnit testova, smoke 240/240, dead-letter provjeren. Security review: autorizacijska matrica i IDOR čisti. Preostalo: rubni slučajevi plaćanja (uplata nakon otkazivanja, `processing` status, idempotency, provjera metadata), progutana greška refunda pri brisanju mentora, sitnice | PRIHVAĆENO, commit `b1d498b`; dorada plaćanja u toku |
| 1 | mobile | 7/10 | Kompletan rewrite (69 fajlova, 14 testova), vjeran skicama 07–14, parsiranje provjereno protiv živog API-ja. Ali: drill-down ekrani nemaju Back dugme (pravilo uputa), detalj pretplate ne prikazuje uplate i upitnik, greške ugniježđenih polja (`client.*`) se ne prikazuju, "PORUKA MENTORU" za PendingPayment, "ZAVRŠIO SAM TRENING" za arhiviran plan, nedostaju `mounted` provjere, profil se ne oporavlja nakon greške | vraćeno na doradu (10 stavki) |
| 3 | mobile | 9/10 | Biblioteke ažurirane (dio 5.11, secure_storage 11.2, fl_chart 1.2, intl 0.20, flutter_stripe 13.1 — 14.x traži AGP 9, što se ovdje ne može provjeriti; cached_network_image 4 traži noviji Dart). `minSdk` = Flutter default (24). Android checklist urađen | PRIHVAĆENO, commit `0008fa5` |
| 2 | backend | 9/10 | Dorada plaćanja: auto-refund za uplatu koja se ne može primijeniti (`RefundPending` + retry), 409 dok se prethodna uplata obrađuje, Idempotency-Key, provjera metadata/iznosa, refund greška više nije progutana, check constraints, 96 testova | PRIHVAĆENO, commit `a5faae2` |
| 2 | mobile | 8.5/10 | Svih 10 stavki riješeno: Back strelica na drill-down ekranima, navigacija bez duplih frameova, detalj pretplate sa uplatama/upitnikom, ugniježđene greške polja, NASTAVI PLAĆANJE za PendingPayment, `mounted` provjere, retry profila, pretraga obavijesti. 18 testova, live smoke sa privremenim nalogom | PRIHVAĆENO, commit `edffca2` |
| 2 | desktop | 8.5/10 | Sve stavke runde 1 riješene. Live smoke protiv API-ja (59/62, 2 stvarna problema popravljena: `sortOrder` 1–100, test harness), 30 testova, analyze čist, tokeni u Credential Lockeru. Ostaje sitnica: backend greške u reject/cancel dijalozima idu kao snackbar | PRIHVAĆENO, commit `cb668ed` |
| 1 | desktop | 7/10 | Kompletan rewrite (51 fajl, 25 testova, analyze čist), skice i pravila dobro pokriveni. Ali: DataTable u izvještajima ima 8 kolona / 9 ćelija (pad), email regex odbija `ime@edu.fit.ba`, lažna poruka o ponovnom odobrenju, uloge prikazane na engleskom, šifarnici gutaju greške polja, sesija se ne čisti kad refresh padne, tokeni u plaintextu; nije testirano protiv živog API-ja | vraćeno na doradu (11 stavki + live smoke test) |

### Završni completeness audit (5 agenata)
Svaka stavka prijave (2.1, 2.2, 3.1.x, 3.2.x, 5, Obim) i svako pravilo iz uputa praćeni su kroz UI → API → bazu. **0 blocker/major nedostataka.** Dva minor nalaza idu u rundu 3:
- certifikati se serviraju javno (`/uploads`) → prelaze na zaštićeni endpoint (Admin ili vlasnik-mentor);
- zastarjele biblioteke → ažurirati Flutter biblioteke na posljednje verzije; backend ostaje na .NET 9 (instaliran SDK 9), samo zadnje 9.0.x zakrpe.

### Runda 3 (završena)
- backend 9/10: certifikati van `wwwroot` (privatni volume + `SeedFiles`), `GET /api/certificates/{id}/file` samo za admina/vlasnika-mentora, stari javni URL-ovi → 404; NuGet na zadnje net9 verzije (Microsoft.* 9.0.20, RabbitMQ.Client 7.2.2, Swashbuckle 10.2.3); 120 testova. Commit `b17bf33`, `516c915`.
- desktop 9/10: certifikati se preuzimaju preko autentifikovanog klijenta (PDF/slika po Content-Type), mentor može pregledati svoje certifikate, dio 5.11; `pdf`/`printing` ostaju jer novije verzije traže Dart 3.12 (mašina ima Flutter 3.41.4 / Dart 3.11.1). 38 testova. Commit `d31394a`.
- mobile 9/10: vidi tabelu. Commit `0008fa5`.

Napomena: u sandboxu agenata `flutter build windows` je padao zbog symlinkova za pluginove (Developer Mode isključen). Dana 29.09.2026. release build je uspio iz sesije glavnog agenta: `UI/gobeyond_desktop/build/windows/x64/runner/Release/gobeyond_desktop.exe`.

### Završna provjera (svježi klon, kao ocjenjivač) — PROŠLO
- `git clone` → `docker compose up -d --build` bez `.env` i bez ikakvih izmjena: svih 5 servisa healthy za ~1 min.
- Smoke 53/53: login za sve demo naloge, sve glavne rute po ulogama, certifikati (200 sa tokenom, 401 bez), email preko RabbitMQ → Mailpit, create-intent bez Stripe ključeva vraća 400 sa porukom.
- `dotnet test` 120/120 · desktop analyze čist + 38/38 testova · mobile analyze čist + 18/18 testova.
- README provjeren tvrdnju po tvrdnju — bez netačnosti. Nema commitanih tajni, nema apsolutnih lokalnih putanja.
- Stack je ponovo podignut sa svježom bazom (`docker compose up -d --build`).

## Stanje: ZAVRŠENO (implementacija po prijavi i uputama)

Commitovi ove sesije (svi na ime Mirza Rujanac): `861a925` checkpoint · `b1d498b` backend · `cb668ed` desktop · `edffca2` mobile · `a5faae2` plaćanja · `0008fa5` mobile biblioteke · `d31394a` desktop certifikati · `b17bf33` + `516c915` privatni certifikati · `38b60ac` README · + docs.

## Sljedeći koraci (korisnik)

1. **Stripe TEST ključevi** u `.env` (`Payments__SecretKey`, `Payments__PublishableKey`) da plaćanje radi. Ocjenjivač mora moći platiti bez intervencije, pa odlučiti kako mu dostaviti ključeve. Ključevi se ne smiju commitati u javni repo osim ako je to svjesna odluka (samo TEST ključevi).
2. **Windows Developer Mode** (Settings → System → For developers) — preporučeno ako `flutter run -d windows` javi grešku o symlinkovima (release build je 29.09. uspio i bez njega).
3. **Android emulator test** mobilne aplikacije (na ovoj mašini nema Android SDK-a): posebno Stripe PaymentSheet (flutter_stripe 13.1), upload slika, navigacija.
4. Ručno proći desktop tokove (plan builder, izvještaji PDF/print) u buildanoj aplikaciji.
5. `git push` na GitHub (repo mora biti javan) — nije urađeno automatski.

Poznata ograničenja: Stripe tokovi (uspješno plaćanje, refund, webhook) testirani samo unit testovima s lažnim gatewayem, ne protiv pravog Stripe-a; podsjetnik pred istek i automatski Expired nisu viđeni live (implementirani i pokriveni kodom).

## Kako nastaviti

Pročitati ovaj fajl, `pravila.md` i `docs/requirements/`. Provjeriti `git status` i sačuvati korisničke izmjene. Nastaviti od "Sljedeći koraci", evidentirati konkretne testove i review ocjene. Ne tražiti ponavljanje već zadanog cilja.

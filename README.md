# GoBeyond

Seminarski rad iz predmeta **Razvoj softvera II** (FIT Mostar), Mirza Rujanac, IB210020.

GoBeyond povezuje mentore (fitness trenere) s klijentima kroz personalizovane sedmične planove treninga i ishrane, praćenje napretka i pretplatu na mentora.

- **Desktop aplikacija** (Flutter, Windows) je za administratore i mentore.
- **Mobilna aplikacija** (Flutter, Android) je za klijente.
- **Backend** je ASP.NET Core 9 REST API sa SQL Serverom (baza `210020`) i pomoćnim servisom `GoBeyond.EmailConsumer`, koji poruke prima preko RabbitMQ-a.

## Pristupni podaci

Lozinka za sve naloge je `test`.

| Aplikacija | Korisničko ime | Uloga |
|---|---|---|
| Desktop | `desktop` | Administrator |
| Desktop | `admin` | Administrator |
| Desktop | `mentor` | Mentor |
| Mobilna | `mobile` | Klijent |
| Mobilna | `client` | Klijent |

Prijava radi sa korisničkim imenom ili email adresom. U bazi postoje i drugi seed korisnici (mentori po vrstama treninga, mentori koji čekaju odobrenje, klijenti u raznim statusima pretplate), svi sa lozinkom `test`.

**Stripe testne kartice:** vidi [Stripe ključevi](#2-stripe-ključevi-plaćanje-pretplate).

## Pokretanje

### 1. Backend (Docker)

Preduslov je Docker Desktop. Iz root foldera repozitorija pokrenite:

```bash
docker compose up -d --build
```

Pokreće se pet servisa:

| Servis | Adresa |
|---|---|
| REST API + Swagger | http://localhost:5000/swagger |
| SQL Server (baza `210020`) | `localhost,1433` (`sa` / `GoBeyondDemo!210020`) |
| RabbitMQ management | http://localhost:15672 (`gobeyond` / `GoBeyondDemoRabbit`) |
| Mailpit (pregled poslanih emailova) | http://localhost:8025 |
| `email-consumer` | pomoćni servis, nema port |

Pri prvom pokretanju API sam primijeni migracije i napuni bazu demo podacima (1–2 minute dok SQL Server ne postane dostupan). Za potpuno svježu bazu pokrenite `docker compose down -v`, pa ponovo `docker compose up -d --build`.

### 2. Stripe ključevi (plaćanje pretplate)

Plaćanje ide preko Stripe-a u TEST modu. Ključevi se ne nalaze u kodu.

1. Kopirajte `.env.example` u `.env` (u root folderu).
2. Upišite `Payments__SecretKey` (`sk_test_...`) i `Payments__PublishableKey` (`pk_test_...`) sa https://dashboard.stripe.com/test/apikeys.
3. Pokrenite `docker compose up -d` ponovo.

Isti `.env` čitaju i API i `GoBeyond.EmailConsumer` kad se pokreću lokalno (`dotnet run`, Visual Studio, VS Code). Varijable koje su već postavljene (docker-compose, `launchSettings.json`) imaju prednost. Mobilna aplikacija publishable ključ dobija od API-ja, pa se ne podešava u Flutteru. Bez ključeva sve ostalo radi, a pokušaj plaćanja vraća poruku "Stripe plaćanje nije konfigurisano na serveru."

**Testne kartice** (bilo koji budući datum, bilo koji CVC i poštanski broj):

| Kartica | Rezultat |
|---|---|
| `4242 4242 4242 4242` | uspješno plaćanje |
| `4000 0025 0000 3155` | traži 3D Secure potvrdu (u testnom prozoru potvrdite autentifikaciju) |
| `4000 0000 0000 9995` | odbijena kartica; u istom prozoru možete unijeti drugu karticu |

Nakon plaćanja aplikacija poziva `POST /api/payments/{id}/confirm`, a backend provjerava uplatu na Stripe-u. Webhook nije obavezan. Ako potvrda ne stigne (npr. aplikacija se ugasi), API za nekoliko minuta sam provjeri uplatu na Stripe-u i primijeni je, ili je vrati ako je pretplata u međuvremenu otkazana.

Opcionalno, webhook preko Stripe CLI:

```bash
stripe listen --events payment_intent.succeeded,payment_intent.payment_failed,payment_intent.canceled --forward-to http://localhost:5000/api/payments/webhook
```

Ispisani `whsec_...` upišite kao `Payments__WebhookSecret` u `.env` i pokrenite `docker compose up -d`.

### 2a. Email (Mailpit podrazumijevano, opcionalno Gmail)

Bez `.env` postavki emailovi idu na **Mailpit** (http://localhost:8025) — ništa ne napušta mašinu, pogodno za demo/razvoj i za ocjenjivanje bez ikakvih kredencijala.

Za stvarnu dostavu preko Gmail-a:

1. Na Gmail nalogu uključite **dvostepenu verifikaciju** (2-Step Verification), pa napravite **App Password** (https://myaccount.google.com/apppasswords) — obična lozinka naloga ne radi za SMTP.
2. U `.env` (root foldera) dodajte:
   ```
   Smtp__Host=smtp.gmail.com
   Smtp__Port=587
   Smtp__UseSsl=true
   Smtp__Username=vas.nalog@gmail.com
   Smtp__Password=<app password, ne obična lozinka>
   Smtp__FromEmail=vas.nalog@gmail.com
   Smtp__FromName=GoBeyond
   ```
   Gmail zahtijeva da `From` bude tačno prijavljeni nalog (`Smtp__Username`), inače ga sam prepiše.
3. Ponovo podignite servis koji šalje email: `docker compose up -d --build` (rekreira `email-consumer`; `docker-compose.yml` koristi Mailpit samo kao podrazumijevanu vrijednost `${Smtp__Host:-mailpit}` / `${Smtp__Port:-1025}`, pa postavke iz `.env` pobjeđuju).

**Zaštita seed/demo adresa (`Smtp:SuppressedRecipientDomains`, podrazumijevano `["gobeyond.ba"]` u `appsettings.Shared.json`):** kad SMTP host **nije** Mailpit, `GoBeyond.EmailConsumer` NE šalje email primaocima na navedenim domenama ili njihovim poddomenama (seed korisnici imaju `{username}@gobeyond.ba`, koja može biti tuđa, stvarna domena; poddomena kao `edu.gobeyond.ba` je i dalje ista treća strana, pa se i njoj potiskuje slanje) — poruka se samo loguje ("suppressed") i potvrđuje (ack) bez pokušaja slanja i bez dead-lettera. Kad je host Mailpit, supresija se ne primjenjuje jer email ionako ostaje lokalno, pa je korisno vidjeti svu demo poštu (registracija, obavijesti, poruke) seed korisnika u Mailpit sučelju. Kroz stvarni SMTP se ne šalje ni primaocu čiji host, u obliku koji SMTP klijent stvarno šalje, nije ispravno DNS ime (samo ASCII slova, cifre, `-` i `.`; npr. `x@edu＠gobeyond.ba` bi otišlo kao `x@edu@gobeyond.ba`), i kad je lista domena prazna — takva poruka se loguje kao upozorenje i potvrđuje.

**Format poruke:** svaki email se šalje kao `multipart/alternative` (text/plain kao UTF-8/base64 sa CRLF prijelomima reda + jednostavan HTML dio kao UTF-8/quoted-printable), sa `Message-ID` na domeni pošiljaoca — bez linkova i slika — da bi Gmail i slični filteri manje sumnjičili poruku kao spam.

### 3. Desktop aplikacija (Windows)

```bash
cd UI/gobeyond_desktop
flutter pub get
flutter run -d windows --dart-define=GO_BEYOND_API_URL=http://localhost:5000
```

Ako `flutter run` javi grešku o symlinkovima ("Building with plugins requires symlink support"), uključite **Developer Mode** (Settings → System → For developers).

### 4. Mobilna aplikacija (Android emulator)

```bash
cd UI/gobeyond_mobile
flutter pub get
flutter run --dart-define=GO_BEYOND_API_URL=http://10.0.2.2:5000
```

`10.0.2.2` je adresa host računara iz Android emulatora. Za fizički uređaj koristite IP adresu računara (npr. `http://192.168.1.10:5000`).

Android build koristi Gradle 9.1 i radi sa JDK-om koji dolazi uz Android Studio (i JDK 25), kao i sa JDK 17/21, bez dodatnog podešavanja. Prvi build preuzima Gradle (~230 MB) i traje nekoliko minuta. Projekat sadrži i `windows`/`linux` folder, pa i ovdje `flutter pub get` traži **Developer Mode** (vidi desktop aplikaciju iznad).

## Konfiguracija

- Sva konfiguracija backenda i pomoćnog servisa je na jednom mjestu: `appsettings.Shared.json`. To su konekcijski string, JWT, RabbitMQ, SMTP, Stripe, intervali obavijesti i ograničenja uploada.
- Vrijednosti se mogu pregaziti environment varijablama u formatu `Sekcija__Kljuc`, preko `.env` fajla (čitaju ga docker-compose i lokalno pokretanje) ili `docker-compose.yml`. U `docker-compose.yml` su pregažena samo host imena Docker servisa.
- Flutter aplikacije adresu API-ja čitaju iz `--dart-define=GO_BEYOND_API_URL=...`.

## Arhitektura

```text
GoBeyond.API/
  GoBeyond.API             REST API: kontroleri, auth (JWT + refresh token), middleware, hosted servisi
  GoBeyond.Core            entiteti, DTO-i, enumi, search objekti
  GoBeyond.Infrastructure  EF Core (SQL Server), servisi, state machine planova, recommender, outbox, Stripe
  GoBeyond.Contracts       poruke i opcije za RabbitMQ
  GoBeyond.EmailConsumer   pomoćni servis: RabbitMQ consumer → SMTP
  GoBeyond.Tests           xUnit testovi
UI/
  gobeyond_desktop         Flutter desktop (admin + mentor)
  gobeyond_mobile          Flutter mobile (klijent)
docs/api-contract.md       API ugovor između backenda i klijentskih aplikacija
```

- **Mikroservisi / RabbitMQ.** API domensku promjenu i email poruku upisuje u istoj transakciji (outbox tabela). `OutboxDispatcher` poruke objavljuje na queue `gobeyond.notifications`. `GoBeyond.EmailConsumer` ih konzumira i šalje email preko SMTP-a (Mailpit podrazumijevano, opcionalno Gmail — vidi [Email](#2a-email-mailpit-podrazumijevano-opcionalno-gmail)). Primaoci na zaštićenim domenama i njihovim poddomenama (`Smtp:SuppressedRecipientDomains`) se preskaču kad host nije Mailpit — domena primaoca se čita preko `MailAddress` (isti parser koji stvarno šalje email), pa i adrese sa završnom tačkom ili uglastim zagradama (`x@gobeyond.ba.`, `<x@gobeyond.ba>`) ostaju pokrivene; Unicode varijante domene (npr. fullwidth slova, ideografska tačka) se porede u ASCII/IDN obliku koji SMTP klijent stvarno šalje, a host koji u tom obliku nije ispravno DNS ime (npr. `edu@gobeyond.ba` iz fullwidth `＠`) se kroz stvarni SMTP nikad ne šalje. Nakon 5 neuspjelih pokušaja ili za poruku bez obaveznih polja (npr. bez naslova/tijela) ide u `gobeyond.notifications.dead`, nikad kao prazan email. Ako broker otkaže consumer-a (npr. obrisan queue) ili kanal/konekcija padne, consumer se sam ponovo poveže i re-deklariše queue umjesto da tiho stane. Poruka koju broker isporuči ponovo (redelivery nakon pada procesa, ili dok je prvo slanje još u toku) se prepoznaje i samo potvrđuje, bez ponovnog slanja — ključ za to (append-only fajl na `Smtp:SentMessageIdsFilePath`, apsolutna putanja van repozitorija, `/data/...` na imenovanom docker volume-u) NIJE sam Id poruke (taj se restartuje od 1 kad se baza resetuje), nego Id + vrijeme upisa u outbox + hash sadržaja, da drugačija poruka koja nakon reseta baze slučajno dobije isti Id i dalje bude stvarno poslana. Greška u bilježenju (npr. zaključan fajl) se samo loguje kao upozorenje i nikad ne blokira slanje niti pokretanje servisa.
- **Automatske obavijesti.** `SubscriptionLifecycleService` periodično:
  - označava istekle pretplate,
  - šalje podsjetnik pred istek,
  - upozorava mentora kad plan nije objavljen,
  - javlja klijentu kad je neaktivan.
- **Sistem preporuke (content-based filtering).**
  - Za svakog mentora gradi se vektor osobina: vrsta treninga, specijalizacije (ciljevi), ciljevi klijenata s kojima je uspješno sarađivao i iskustvo. Ocjena ulazi kao Bayesov prosjek.
  - Za klijenta se gradi vektor iz željene vrste treninga, vrsta treninga ranijih mentora, primarnog cilja i nivoa spreme.
  - Mentori se rangiraju po **kosinusnoj sličnosti**. Ista mjera između mentora daje "Slični mentori" na detaljima mentora.
- **Planovi (State Machine).** Draft → Published → Archived. Objava zahtijeva popunjenih svih 7 dana, a svaka izmjena objavljenog plana obavještava klijenta.
- **Šifarnici** (vrste treninga, ciljevi, nivoi spreme, spolovi) koriste generički BaseCRUD kontroler i servis sa SearchObject-ima.

## Testovi

```bash
dotnet test GoBeyond.API/GoBeyond.sln
cd UI/gobeyond_desktop && flutter test
cd UI/gobeyond_mobile && flutter test
```

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

- **Mikroservisi / RabbitMQ.** API domensku promjenu i email poruku upisuje u istoj transakciji (outbox tabela). `OutboxDispatcher` poruke objavljuje na queue `gobeyond.notifications`. `GoBeyond.EmailConsumer` ih konzumira i šalje email preko SMTP-a (Mailpit). Nakon 5 neuspjelih pokušaja poruka ide u `gobeyond.notifications.dead`.
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

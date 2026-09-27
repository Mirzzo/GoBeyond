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

### Review ocjene
_(popunjava se tokom rada)_

## Sljedeći koraci

- Završiti audit i upisati nalaze ovdje.

## Kako nastaviti

Pročitati ovaj fajl, `pravila.md` i `docs/requirements/`. Provjeriti `git status` i sačuvati korisničke izmjene. Nastaviti od "Sljedeći koraci", evidentirati konkretne testove i review ocjene. Ne tražiti ponavljanje već zadanog cilja.

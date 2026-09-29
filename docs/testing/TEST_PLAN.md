# GoBeyond — test plan (29.09.2026.)

Cilj: provjeriti aplikaciju end-to-end na Android emulatoru, desktop klijentu i API-ju, sa naglaskom na tri prioritetne oblasti. Svaki pronađeni bug se reprodukuje nezavisno, popravlja u zasebnom tasku i prolazi review (ocjena mora biti veća od 8).

## Prioriteti

| Prioritet | Oblast |
|---|---|
| P1 | Plaćanje preko Stripe-a (PaymentSheet na emulatoru + API rubni slučajevi) |
| P1 | Notifikacije: in-app (mobile/desktop), email (RabbitMQ → SMTP), sistemske obavijesti |
| P1 | Background jobovi: OutboxDispatcher, EmailConsumer, SubscriptionLifecycleService (istek, podsjetnici, usklađivanje uplata, retry povrata) |
| P2 | Auth, autorizacija/IDOR, validacije, mentori i preporuke, planovi, napredak, poruke, recenzije, profil, admin, izvještaji |
| P3 | UI detalji (navigacija, Back dugme, prikaz, tekstovi na bosanskom), regresija postojećih testova |

## Okruženje

| Stavka | Vrijednost |
|---|---|
| Backend | `docker compose` stack: API `http://localhost:5000`, SQL Server `localhost,1433` (baza `210020`), RabbitMQ, Mailpit `http://localhost:8025`, `email-consumer` |
| Email | Glavni stack šalje preko Gmail SMTP-a iz `.env`; domena `gobeyond.ba` je potisnuta (seed i QA nalozi ne primaju stvarne mailove) |
| Stripe | TEST mod, ključevi u `.env`, valuta `usd`, webhook secret nije postavljen (potvrda ide preko `/confirm` i usklađivanja) |
| Android | AVD `GoBeyond_API35` (Pixel 7, Android 15, x86_64), `emulator-5554`, API sa emulatora: `http://10.0.2.2:5000` |
| Desktop | Flutter Windows, `GO_BEYOND_API_URL=http://localhost:5000` |
| Izolovana instanca (background jobovi) | kopija backenda u scratchpadu, API na portu 5100, baza `210020_bgtest`, vlastiti RabbitMQ queue-ovi, SMTP → Mailpit, `Lifecycle__IntervalSeconds=10` |

## Testni nalozi i izolacija podataka

- Korisnikov stvarni nalog (klijent, email na Gmail-u) registruje se kroz mobilnu aplikaciju i koristi samo u E2E toku na emulatoru, sa mentorom `mentor`. Na taj nalog stižu stvarni emailovi; prijem potvrđuje korisnik.
- Ostali testeri registruju vlastite QA naloge sa adresama `qa.<oblast>.<n>@gobeyond.ba` (potisnuta domena, nema stvarnih mailova).
- Mentori po testeru: E2E → `mentor`; plaćanja → `dino.kurtovic`, `amir.salihovic`; notifikacije → `selma.delic`, `lejla.mujic`; desktop → `kenan.omerovic`; opšti API → samo novoregistrovani mentori.
- Sistemske obavijesti prema ulozi Client (stižu i na korisnikov email) šalje samo E2E tok, jednom.
- Testovi ostavljaju QA podatke u razvojnoj bazi. Svježa baza: `docker compose down -v` pa `docker compose up -d --build` (briše i korisnikov testni nalog).

## Testni slučajevi

### P1-A — Stripe plaćanje

| ID | Gdje | Slučaj | Očekivano |
|---|---|---|---|
| PAY-01 | emulator | Registracija, odabir mentora, upitnik, PaymentSheet sa `4242 4242 4242 4242` | Pretplata `AwaitingMentor`, uplata `Succeeded`, mentor dobija obavijest |
| PAY-02 | emulator | Odbijena kartica `4000 0000 0000 9995` | Poruka na bosanskom, može se unijeti druga kartica, nema duple uplate |
| PAY-03 | emulator | 3D Secure `4000 0025 0000 3155` (potvrda i odbijanje autentifikacije) | Potvrda → uspjeh; odbijanje → jasna poruka, pretplata ostaje `PendingPayment` |
| PAY-04 | emulator | Zatvaranje PaymentSheet-a bez plaćanja, pa NASTAVI PLAĆANJE | Pretplata `PendingPayment`, nastavak radi |
| PAY-05 | emulator | Produženje aktivne pretplate | `endDate` +30 dana, uplata `Renewal`, podsjetnik resetovan |
| PAY-06 | API | Mentor odbija plaćeni zahtjev | Stripe refund postoji, uplata `Refunded`, klijent obaviješten |
| PAY-07 | API | Otkazivanje pa kasna uplata | Automatski povrat, pretplata ostaje otkazana |
| PAY-08 | API | Dupli/paralelni `create-intent` i `confirm`, Idempotency-Key | Jedna uplata, 409 gdje treba, nema dupliranja perioda |
| PAY-09 | API | IDOR: tuđa pretplata / tuđa uplata | 403/404 |
| PAY-10 | izolovana | Webhook: bez potpisa, pogrešan potpis, ispravan HMAC potpis, ponovljeni događaj | Odbijeno / obrađeno idempotentno |
| PAY-11 | API + job | Naplaćena a nepotvrđena uplata | Usklađivanje je primijeni ili vrati |
| PAY-12 | API | Uplata u izvještajima i detalju pretplate | Iznosi i valuta tačni |
| PAY-13 | API | Promjena cijene mentora između intent-a i potvrde, iznos/valuta, min. iznos | Naplaćuje se iznos iz intent-a, nema neslaganja |

### P1-B — Notifikacije

| ID | Gdje | Slučaj | Očekivano |
|---|---|---|---|
| NOT-01 | API | Matrica događaj → primalac → in-app / email (plaćanje, prihvatanje, odbijanje, otkazivanje, plan objavljen/izmijenjen, poruka, povrat, istek, podsjetnici, odobrenje mentora, sistemska obavijest) | Svaki događaj pravom primaocu, tekst na bosanskom, email samo gdje je predviđen |
| NOT-02 | emulator | Lista obavijesti, broj nepročitanih, označi pročitano / sve, pretraga, osvježavanje | Radi bez restarta aplikacije |
| NOT-03 | desktop | Obavijesti za mentora i admina | Prikazuju se i označavaju |
| NOT-04 | Gmail | Stvarni emailovi na korisnikov nalog | Korisnik potvrđuje prijem (glavni agent pita) |
| NOT-05 | API | Sistemske obavijesti: ciljna uloga, izmjena, brisanje (briše nepročitane) | Prema ugovoru |
| NOT-06 | API + emulator | Poruke mentor ↔ klijent → NewMessage | In-app obavijest, bez emaila |
| NOT-07 | API | PlanUpdated throttle (10 min) | Najviše jedna obavijest u 10 min po planu |
| NOT-08 | API | Potisnute domene, obrisani korisnici | Nema slanja; nema pogrešnih retry-a |
| NOT-09 | izolovana | Dijakritike (č ć š đ ž) u naslovu i tijelu emaila | Ispravno kodirano u Mailpit-u |

### P1-C — Background jobovi

| ID | Gdje | Slučaj | Očekivano |
|---|---|---|---|
| BG-01 | izolovana | Outbox → RabbitMQ → EmailConsumer → SMTP | Email stiže, `SentAt` postavljen |
| BG-02 | izolovana | RabbitMQ nedostupan za API | Outbox čuva poruke, šalje nakon oporavka, bez duplikata |
| BG-03 | izolovana | SMTP greška | Retry sa `x-attempt`, nakon 5 pokušaja dead-letter |
| BG-04 | izolovana | Neispravan JSON na queue-u | Dead-letter, consumer nastavlja |
| BG-05 | izolovana | Lifecycle: Expired, SubscriptionExpiring (jednom), PlanMissing (48 h, ponavljanje 24 h), Inactivity (7 d, ponavljanje 7 d), RefundPending retry, usklađivanje uplata | Tačni prijelazi i obavijesti, bez duplikata |
| BG-06 | izolovana | Restart API-ja i consumera usred obrade | Nema izgubljenih ni dupliranih emailova |
| BG-07 | API | Heartbeat → UserActivity (max 90 s po pozivu) | Vrijeme na platformi u izvještajima |
| BG-08 | emulator + Gmail | Podsjetnik pred istek na korisnikovom nalogu (pomjeren `EndDate`) | In-app + email, pa produženje resetuje podsjetnik |

### P2 — Ostale funkcionalnosti

| ID | Oblast | Slučajevi |
|---|---|---|
| AUTH | Auth | Registracija klijenta/mentora (validacije, dijakritike, duplikati), prijava imenom i emailom, refresh, logout, promjena lozinke, blokiran/obrisan/pending/rejected nalog |
| SEC | Autorizacija | Matrica uloga za sve rute, IDOR (pretplate, planovi, poruke, obavijesti, certifikati, napredak), upload (tip, veličina, broj fajlova) |
| MEN | Mentori | Lista, filter, sort, pretraga, detalj, recenzije, slični mentori, preporuke za klijenta |
| PLN | Planovi | Izrada, 7 dana, objava, izmjena objavljenog (verzija), arhiviranje, klijentski prikaz, evidencija treninga |
| PRG | Napredak | Mjesečni unos, slika, budući mjesec (400), snapshot plana, grafikon |
| MSG/REV | Poruke, recenzije | Slanje po statusu pretplate, pročitano, recenzija samo za prihvaćenu pretplatu, jedna po pretplati |
| ADM | Admin | Korisnici (izmjena, blokiranje, brisanje), zahtjevi mentora (odobri/odbij), certifikati, pretplate (otkaži), izvještaji i PDF |
| DSK | Desktop | Pokretanje, prijava admin/mentor, glavni tokovi, obavijesti |
| MOB | Mobile UI | Navigacija, Back dugme, prikaz na 1080×2400, tekstovi na bosanskom, upload slike iz galerije |
| REG | Regresija | `dotnet test`, `flutter analyze` + `flutter test` (desktop i mobile) |

## Izvještavanje o bugovima

Svaki nalaz sadrži: ID testa, ozbiljnost (blocker/major/minor/trivial), korake za reprodukciju, očekivano i stvarno ponašanje, dokaz (HTTP odgovor, log, screenshot, SQL), vjerovatan uzrok i fajlove. Nalaz se nezavisno reprodukuje prije popravke.

## Tok rada

1. Testiranje (paralelno: E2E emulator, plaćanja API, notifikacije API, background jobovi, opšti API/sigurnost, desktop).
2. Nezavisna reprodukcija svakog nalaza.
3. Popravka po grupama nalaza (subagenti), sa testovima.
4. Review glavnog agenta, ocjena 1–10; ocjena 8 ili niža vraća se na doradu.
5. Ponovni test popravljenih slučajeva, commit, zapis u `HISTORIJA_RADA.md`.

## Rezultati

Rezultati i ocjene se upisuju u `docs/testing/TEST_REPORT.md` i `HISTORIJA_RADA.md`.

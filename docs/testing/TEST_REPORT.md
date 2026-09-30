# GoBeyond — izvještaj o testiranju (29.–30.09.2026.)

Testiranje je rađeno po [TEST_PLAN.md](TEST_PLAN.md). Testirali su subagenti:
- API i background jobove na docker stacku i na izolovanim instancama;
- desktop aplikaciju;
- mobilnu aplikaciju na Android emulatoru (`GoBeyond_API35`) sa stvarnim korisničkim nalogom.

Svaki nalaz je nezavisno reprodukovan. Potvrđeni nalazi su popravljeni u zasebnim fix grupama, svaka u svom git worktree-u. Svaka grupa je prošla adversarial pre-review, a konačnu ocjenu je dao glavni agent. Implementacija je prihvaćena samo uz ocjenu **veću od 8**. Grupe s ocjenom 8 ili nižom vraćene su na doradu. Nakon popravki je urađen regresijski retest na redeployanom stacku i završni E2E na emulatoru.

## Sažetak

| Stavka | Rezultat |
|---|---|
| Runda 1 (API, background jobovi, desktop, code audit) | 268 slučajeva: 219 pass, 48 fail, 1 skipped. 53 prijave, 50 stvarnih, oko 35 jedinstvenih bugova |
| E2E na emulatoru (korisnikov nalog) | 60 slučajeva: 52 pass, 7 fail, 1 blocked. 31 nalaz, 26 stvarnih |
| Regresijski retest (3 testera, redeployan stack) | 62 ranija nalaza: 54 popravljena, 2 namjerno nepromijenjena (nisu bugovi, po ugovoru), 6 za email-consumer koji tada još nije bio redeployan (pokriveno kasnije). 8 novih sitnijih nalaza, svi popravljeni |
| Završni E2E na emulatoru | **29/29 pass**, bez novih nalaza |
| Fix grupe | 12 grupa, sve prihvaćene sa ocjenom 8,5–9 (neke nakon 1–3 dorade) |
| Automatski testovi na kraju | Backend **557/557**, desktop **87/87** (`flutter analyze` čist), mobile **116/116** (`flutter analyze` čist) |
| Email na stvarni Gmail | Prvi E2E: 9/9 stiglo, ali u **Spam**. Nakon prelaska na multipart (text + HTML) sa `Message-ID`: 2/2 u **Inbox**, uredno formatirani (potvrdio korisnik) |

### Prioritetne oblasti (P1)

| Oblast | Stanje na kraju |
|---|---|
| **Stripe plaćanje** | PaymentSheet na emulatoru radi za sve testirane kartice: odbijena kartica `…9995` (poruka na bosanskom nakon zatvaranja sheet-a, bez dodatne uplate), 3D Secure `…3155` (neuspjeh, pa uspjeh) i produženje karticom `…4242` (+30 dana od starog kraja). Na API-ju, sa Stripe objektima provjerenim na serveru: jedna otvorena pretplata i pri paralelnim zahtjevima; jedan ishod za accept/reject i confirm/cancel; admin otkazivanje i odbijanje plaćenog zahtjeva vraćaju novac; zastarjeli PaymentIntent se otkazuje na Stripe-u; nepotvrđene uplate se usklađuju; Stripe timeout ne gasi API; osporena naplata (dispute) se ne pokušava vraćati beskonačno, a admin i klijent dobijaju jasnu poruku |
| **Notifikacije** | In-app (mobile i desktop) i email za sve događaje iz matrice. Tekstovi su na bosanskom, datumi u zoni platforme, iznosi "39,99 USD", rodno neutralno, bez "..". NewMessage obavijest ima naslov "Nova poruka: {ime}", pamti pošiljaoca i označava se pročitanom pri otvaranju niti. Obavijesti se osvježavaju bez restarta aplikacije |
| **Background jobovi** | Outbox → RabbitMQ → EmailConsumer → SMTP: bez duplikata pri redelivery-ju (i nakon pada procesa i dok slanje traje), prazan ili neispravan payload ide u dead-letter, consumer se sam ponovo poveže kad ga broker otkaže, supresija test domene se ne može zaobići. Lifecycle: istek, podsjetnik pred istek (jednom, produženje ga resetuje), "plan nedostaje", usklađivanje uplata i ponavljanje povrata rade i nakon restarta stacka |

## Runda 1 — rezultati po oblastima

| Tester | Slučajeva | Pass | Fail | Prijava | Stvarnih |
|---|---|---|---|---|---|
| payments-api (Stripe, pretplate) | 39 | 27 | 12 | 9 | 8 |
| notifications-api | 52 | 46 | 6 | 10 | 8 |
| background-jobs (izolovana instanca) | 21 | 18 | 3 | 7 | 7 |
| api-security-auth | 48 | 41 | 7 | 10 | 10 |
| api-features | 46 | 37 | 8 (+1 skipped) | 7 | 7 |
| desktop | 43 | 38 | 5 | 5 | 5 |
| code-audit-p1 (plaćanja, lifecycle) | 19 | 12 | 7 | 5 | 5 |

Tri prijave nisu bugovi:
- izvještaj ne sadrži zaradu obrisanih mentora (namjerno);
- ponovna objava plana šalje PlanPublished (po ugovoru; throttle važi za izmjene);
- supresija poddomena (svejedno je popravljena kao hardening).

Najvažniji (major) nalazi:
- admin otkazivanje plaćenog zahtjeva (`AwaitingMentor`) ne vraća novac;
- Stripe timeout u lifecycle servisu gasi cijeli API;
- race condition-i: duple otvorene pretplate, istovremeni accept i reject, confirm u isto vrijeme kad i istek ili otkaz;
- zamijenjeni ili stari PaymentIntent ostaje plativ i nikad se ne vraća;
- admin reset lozinke ne poništava access token;
- degradirani mentor (uloga promijenjena u klijenta) i dalje je vidljiv i plativ;
- desktop dijalozi se ruše (dispose kontrolera).

## E2E na emulatoru (korisnikov nalog), prvi prolaz

| Tok | Pass | Fail | Blocked |
|---|---|---|---|
| M1 — registracija, mentori, plaćanje (PAY-01..04) | 13 | 3 | 0 |
| M2 — notifikacije, plan, poruke, napredak, sistemska obavijest, profil | 30 | 4 | 1 |
| M3 — podsjetnik pred istek, produženje, recenzija, sesija | 9 | 0 | 0 |

31 nalaz, od toga 26 stvarnih (2 major):
- detalj aktivne pretplate nije dostupan;
- ekrani (obavijesti, pretplata, plan, chat) se ne osvježavaju;
- engleski tekstovi u Material widgetima;
- gramatika (rod, padeži, množina);
- Back na sličnim mentorima;
- na backendu: NewMessage obavijest ostaje nepročitana, "Specijalizovan" za mentorice.

Email notifikacije (NOT-04): 9 poslanih poruka, **sve stigle, ali u Spam**.

## Fix grupe i ocjene

| Grupa | Oblast | Ocjene po rundama | Konačno |
|---|---|---|---|
| build-fix | Gradle 9.1.0 (APK build sa JDK 25) | 9 | **9** |
| B1 | plaćanja, pretplate, tekstovi (row lock, unique indeks, povrati, usklađivanje, Stripe timeout) | 9 | **9** |
| B2 → B2R | auth i sigurnost (security stamp, sesije, refresh token, vidljivost mentora) | 8 → 9 | **9** |
| B3 | validacije, planovi, poruke, napredak | 8,5 | **8,5** |
| B4 | NewMessage, razlozi preporuke, lock pri auto-acceptu, podsjetnici, zona, decimalni zarez | 9 | **9** |
| D1 → D1R | desktop (dijalozi, polling obavijesti, PDF, valuta) | 8 → 8,5 | **8,5** |
| E1 | email-consumer (duplikati, supresija, DLQ, reconnect, HTML email) | 6 → 8 → 8 → 8,5 | **8,5** |
| M1 → M1R | mobile (26 E2E nalaza) | 7,5 → 8,5 | **8,5** |
| B5 | serijalizacija promjena lozinke, trim imena, tekstovi | 9 | **9** |
| B6 | cijena nakon plaćanja, osporene uplate, pošiljalac NewMessage, 404 za neplaćene | 9 | **9** |
| F1B | završne sitnice: supresija, admin upozorenje za osporene uplate, testovi | 9 | **9** |
| F1M | završne sitnice: mobile ("Osporeno", tastatura, testovi) | 9 | **9** |

Sve izmjene su u `master` grani kao atomični commitovi u ime korisnika, bez trailera. API ugovor je ažuriran do **v1.8** (`docs/api-contract.md`, changelog). Nove migracije:
- `SubscriptionConsistency`
- `AddUserSecurityStamp`
- `AddNotificationSender`

## Regresijski retest (redeployan stack)

| Tester | Retestirano | Popravljeno | Ostalo | Novi nalazi |
|---|---|---|---|---|
| rt-security-features | 18 | 17 | 1 (supresija poddomena, email-consumer tada nije bio redeployan) | 3 |
| rt-payments | 27 | 26 | 1 (nije bug: zarada obrisanih mentora) | 3 |
| rt-notifications | 17 | 11 | 1 po ugovoru, 5 za email-consumer (tada nije bio redeployan) | 3 (1 duplikat) |

Novi nalazi, popravljeni u B5 i B6:
- istovremene promjene lozinke (admin reset se mogao izgubiti, deadlock → 500);
- trim imena prije validacije;
- tačka iza razloga odbijanja mentora;
- decimalni zarez u ocjeni;
- cijena pretplate nakon ponovnog ulaska u plaćanje;
- beskonačni povrat osporene naplate;
- spajanje NewMessage obavijesti dva pošiljaoca istog imena;
- 400 umjesto 404 za plan neplaćene pretplate;
- tekst desktop dijaloga.

Nalazi za email-consumer su nakon redeploya potvrđeni pre-reviewom (1,55 miliona generisanih adresa bez zaobilaženja supresije, redelivery bez duplikata uživo) i završnim E2E-om (svaki email poslan tačno jednom).

## Završni E2E (30.09.2026., korisnikov nalog)

**29/29 pass**, bez novih nalaza:
- **Sesija:** preživjela je deploy bez ponovne prijave.
- **Mobilne popravke potvrđene na stvarnom UI-ju:**
  - Pretplata (akcije, DETALJI aktivne pretplate);
  - mentori (množina recenzija, Back);
  - obavijesti (badge, pull-to-refresh, prazna pretraga, označi pročitano);
  - Moj plan;
  - Historija treninga;
  - bosanski kalendar;
  - gramatika validatora.
- **Chat:** poruka mentora pojavi se u otvorenom chatu za 9 s, a nakon povratka iz pozadine odmah.
- **Plaćanje:** odbijena kartica, pa uspješno produženje karticom 4242 (kraj pretplate 30.12.2026.); jedna uplata.
- **Plan:** mentor ga je izmijenio (verzija 5).
- **Email:** "Pretplata je produžena" i "Vaš trening plan je ažuriran" poslani su jednom. **Oba su stigla u Inbox i uredno su formatirana.**

Nakon završnih sitnica (F1B/F1M) stack je ponovo rebuildan. Uživo je potvrđeno da lifecycle prebacuje QA uplatu sa osporenom naplatom u `Disputed` bez daljih pokušaja povrata. Admin otkazivanje takvog zahtjeva vraća upozorenje "Uplata od 39,99 USD je osporena kod banke klijenta i nije vraćena; ishod rješava postupak osporavanja na Stripe-u.".

## Otvoreno i poznata ograničenja

- **Stripe PaymentSheet** (nativni Stripe UI) prikazuje svoje tekstove na jeziku uređaja; Stripe nema bosanski. Sve poruke same aplikacije su na bosanskom (dokumentovano u `UI/gobeyond_mobile/README.md`).
- **Stripe webhook** sa ispravnim potpisom nije testiran na glavnom stacku (webhook secret nije postavljen; potvrda ide preko `/confirm` i usklađivanja). Na izolovanoj instanci sa test secret-om: nepotpisan i pogrešno potpisan → odbijen, potpisan ali neispravan payload → `400`.
- **Osporene naplate** se otkrivaju tek pri pokušaju povrata (nema dispute webhook-a). Ishod spora admin rješava na Stripe-u.
- **Zaključavanje** kod promjena lozinke se oslanja na `READ_COMMITTED_SNAPSHOT` (uključen na bazi `210020`). Bez njega eventualni deadlock vraća `400` sa porukom za ponovni pokušaj, ne `500`.
- **Stare obavijesti** (nastale prije popravki) zadržavaju stare tekstove, npr. "39.99 USD" ili "Nova poruka od …". To su istorijski podaci.
- **Cijene u aplikacijama** se prikazuju kao "$39.99" (po mockupima), a tekstovi obavijesti i emailova kao "39,99 USD".
- **QA podaci** (nalozi `qa_*` / `qa.*@gobeyond.ba`, njihove pretplate, Stripe TEST objekti) ostaju u razvojnoj bazi. QA mentori su blokirani ili obrisani i ne pojavljuju se u katalogu. Svježa baza: `docker compose down -v`, pa `docker compose up -d --build` (briše i korisnikov testni nalog).

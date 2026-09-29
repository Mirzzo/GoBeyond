# GoBeyond — API ugovor (izvor istine za backend, desktop i mobile)

Verzija 1 (27.09.2026). Backend implementira tačno ovaj ugovor. Ako backend mora odstupiti, ažurira ovaj fajl i upisuje izmjenu u sekciju **Changelog** na dnu, da je UI agenti vide.

## 0. Konvencije

- Base URL: desktop `http://localhost:5000`, Android emulator `http://10.0.2.2:5000`; oba Flutter klijenta čitaju `--dart-define=GO_BEYOND_API_URL=...`.
- JSON je camelCase, enumi su stringovi (`"Active"`), datumi ISO-8601 UTC (`"2026-09-27T10:00:00Z"`), a `dateOfBirth` je `"yyyy-MM-dd"`.
- Slike i fajlovi se vraćaju kao **relativne putanje** (`/uploads/...`, `/seed/...`). Klijent ih prefiksira base URL-om. Izuzetak su certifikati mentora: njihov `fileUrl` je `/api/certificates/{id}/file` i zahtijeva `Authorization: Bearer` header (vidi §5).
- Auth ide preko headera `Authorization: Bearer <accessToken>`. Access token traje 60 min. Refresh ide preko `POST /api/auth/refresh`.
- Liste vraćaju JSON niz, osim šifarnika koji vraćaju `{ "items": [...], "totalCount": n }` (generički BaseCRUD obrazac s nastave).
- Greške:
  - `400` `{ "message": "Provjerite unesene podatke.", "errors": { "email": ["Unesite validnu email adresu (npr. ime@domena.com)."] } }`, gdje su ključevi u `errors` camelCase imena polja.
  - `401` / `403` / `404` / `409` vraćaju `{ "message": "<poruka na bosanskom>" }`.
  - `500` vraća `{ "message": "Došlo je do greške na serveru. Pokušajte ponovo." }`.
  - `400` `{ "message": "Isti podaci se upravo mijenjaju u drugom zahtjevu. Pokušajte ponovo." }` kad SQL Server prekine zahtjev zbog istovremene izmjene istih podataka (deadlock, istek čekanja na zaključavanje). Zahtjev se može ponoviti.
- Sve poruke koje backend vraća su na **bosanskom (ijekavica)** i konkretne (npr. "Korisničko ime je već zauzeto.").
- Slobodni tekst (razlozi odbijanja/otkazivanja, upitnik, opisi dana plana, motivacijska poruka, unosi napretka, komentar recenzije, naslov i sadržaj obavijesti, biografija, poruka), ime i prezime korisnika te naziv i opis stavke šifarnika se trimuju **prije** provjere dužine: tekst dopunjen razmacima do minimuma vraća `400` (npr. ime `"A     "` → "Ime mora imati 2–50 znakova."), a razmaci oko ispravne vrijednosti se ne snimaju.
- Datumi u tekstovima obavijesti i emailova su u vremenskoj zoni platforme (`Lifecycle:TimeZoneId`, podrazumijevano Europe/Sarajevo), u formatu `dd.MM.yyyy.`. Iznosi imaju decimalni zarez i valutu velikim slovima, npr. `39,99 USD`.
- Uloge: `Admin`, `Mentor`, `Client`. Politike: `AdminOnly`, `MentorOnly`, `ClientOnly`, `MentorOrAdmin`.

## 1. Model podataka (finalni)

Referentne tabele (šifarnici, CRUD preko generičkog BaseCRUD kontrolera/servisa + SearchObject):
- `TrainingType { id, name (2–50, jedinstveno), description (≤500, obavezno) }` — seed: Weightlifting, Calisthenics, Hybrid.
- `FitnessGoal { id, name (2–60, jedinstveno), description (≤300, opciono) }` — seed: Mršavljenje, Povećanje mišićne mase, Snaga, Izdržljivost i kondicija, Priprema za takmičenje, Opće zdravlje i pokretljivost.
- `FitnessLevel { id, name (2–40, jedinstveno), description (≤300, opciono), sortOrder (int) }` — seed: Početnik, Rekreativac, Srednji nivo, Napredni.
- `Gender { id, name (2–30, jedinstveno) }` — seed: Muško, Žensko.
- Uloge su enum `UserRole` (sistemske, vezane za autorizaciju). Dropdown ih puni sa `GET /api/roles`.

Domenske tabele:
- `User { id, firstName, lastName, username (3–30, [a-zA-Z0-9._], jedinstveno), email (jedinstveno), phoneNumber?, dateOfBirth, genderId, passwordHash, role, profileImageUrl?, isActive (false = blokiran), isDeleted, createdAt, lastLoginAt? }`
- `MentorProfile { id, userId, trainingTypeId, nickname? (≤50, "AKA"), bio (50–4000), yearsOfExperience (0–60), monthlyPrice (1–1000), status (Pending|Approved|Rejected), rejectionReason?, reviewedAt? }` + M2M `MentorSpecialization(mentorProfileId, fitnessGoalId)` (čista međutabela).
- `MentorCertificate { id, mentorProfileId, fileName, fileUrl, uploadedAt, isVerified, verifiedAt? }`
- `ClientProfile { id, userId, weightKg (30–300), heightCm (100–250), fitnessLevelId, trainingExperienceYears (0–60), fitnessGoalId, goalDescription? (≤500), preferredTrainingTypeId? }`
- `Subscription { id, clientProfileId, mentorProfileId, status (PendingPayment|AwaitingMentor|Active|Rejected|Cancelled|Expired), price, currency, createdAt, paidAt?, acceptedAt?, startDate?, endDate?, cancelledAt?, statusReason? }`
- `Questionnaire { id, subscriptionId (1:1), primaryGoal, timeCommitment, healthIssues, medications, weeklySessions, outsideActivity }` — svi slobodan tekst 2–500, obavezni.
- `Payment { id, subscriptionId, amount, currency, stripePaymentIntentId, purpose (Initial|Renewal), status (Pending|Succeeded|Failed|Refunded|RefundPending|Disputed), createdAt, paidAt?, refundedAt? }`
- `TrainingPlan { id, subscriptionId (1:1), mentorProfileId, clientProfileId, motivationalQuote? (≤300), status (Draft|Published|Archived), version (int), createdAt, updatedAt, publishedAt? }`
- `DayPlan { id, trainingPlanId, dayOfWeek (1=Ponedjeljak … 7=Nedjelja), trainingDurationMinutes (1–600), trainingDescription (10–8000), nutritionDurationMinutes? (1–1440), nutritionDescription (10–8000) }` — jedinstveno (trainingPlanId, dayOfWeek).
- `TrainingSession { id, trainingPlanId, dayPlanId, clientProfileId, completedAt, repetitions (1–10000), note? (≤500) }`
- `ProgressEntry { id, clientProfileId, year, month (jedinstveno po klijentu), photoUrl?, weightKg (30–300), measurements (2–300, "Obimi"), strength (2–300, "Snaga"), conditioning (2–300, "Kondicija"), trainingPlanId?, planSnapshotJson?, createdAt, updatedAt }`
- `Review { id, subscriptionId (jedna recenzija po pretplati), clientProfileId, mentorProfileId, rating (1–5), comment (10–1000), createdAt, updatedAt? }`
- `Notification { id, userId, title, body, type, isRead, createdAt, announcementId?, senderUserId? }` (`senderUserId` je pošiljalac NewMessage obavijesti; interno polje, ne vraća se u DTO-u) — type: PlanPublished, PlanUpdated, NewCollaborationRequest, RequestAccepted, RequestRejected, SubscriptionExpiring, SubscriptionExpired, SubscriptionCancelled, MentorApproved, MentorRejected, PlanMissing, Inactivity, NewMessage, Announcement, PaymentSucceeded.
- `Announcement { id, createdByUserId, title (3–120), content (10–2000), targetRole? (null = svi), createdAt }` — sistemske poruke koje admin objavljuje.
- `Message { id, subscriptionId, senderUserId, content (1–2000), sentAt, isRead }` — interni sistem poruka mentor ↔ klijent.
- `RefreshToken`, `UserActivity { userId, day, activeSeconds, lastHeartbeatAt }`, `OutboxMessage` (email outbox → RabbitMQ).

## 2. Auth

| Metoda | Ruta | Auth | Tijelo / odgovor |
|---|---|---|---|
| POST | `/api/auth/login` | anon | `{ username, password }`. `username` može biti korisničko ime ili email. Odgovor je `AuthResponse`. Blokiran ili obrisan korisnik → 403 "Vaš nalog je blokiran. Kontaktirajte administratora." Mentor u statusu Pending → 403 "Vaš mentorski nalog čeka odobrenje administratora." Rejected → 403 "Vaš zahtjev za mentorski nalog je odbijen: {razlog}". |
| POST | `/api/auth/register/client` | anon | `{ firstName, lastName, username, email, phoneNumber?, dateOfBirth, genderId, password, confirmPassword, weightKg, heightCm, fitnessLevelId, trainingExperienceYears, fitnessGoalId, goalDescription?, preferredTrainingTypeId? }` → `AuthResponse` |
| POST | `/api/auth/register/mentor` | anon | `multipart/form-data`: polja `firstName, lastName, username, email, phoneNumber?, dateOfBirth, genderId, password, confirmPassword, trainingTypeId, nickname?, bio, yearsOfExperience, monthlyPrice, specializationIds` (ponovljeno polje) + `certificates` (1–5 fajlova, pdf/jpg/jpeg/png, ≤5 MB). Odgovor: `{ "message": "Registracija je uspješna. Vaš nalog čeka odobrenje administratora." }` |
| POST | `/api/auth/refresh` | anon | `{ refreshToken }` → `AuthResponse` |
| POST | `/api/auth/logout` | auth | `{ refreshToken }` → 204 |
| POST | `/api/auth/change-password` | auth | `{ currentPassword, newPassword, confirmPassword }` → `{ message: "Lozinka je uspješno promijenjena." }`. Završava sesije na svim drugim uređajima (njihovi access i refresh tokeni odmah dobijaju `401`). Uređaj koji je promijenio lozinku ostaje prijavljen: njegov trenutni access token dobija `401`, a `POST /api/auth/refresh` sa njegovim refresh tokenom vraća nove tokene. Promjene lozinke istog korisnika (i admin reset, blokiranje, brisanje, izmjena korisnika) izvršavaju se jedna za drugom: promjena provjerena lozinkom koja je u međuvremenu zamijenjena vraća `400` `errors.currentPassword` "Trenutna lozinka nije ispravna.", pa od dvije istovremene promjene uspijeva tačno jedna i njen uređaj ostaje prijavljen. Ako je nalog u međuvremenu blokiran ili obrisan: `401`. |

Pravila lozinke: 8–64 znaka, bar jedno slovo i jedan broj. Poruka: "Lozinka mora imati 8–64 znaka, uključujući barem jedno slovo i jedan broj." Seed nalozi s lozinkom `test` su izuzetak jer ne prolaze kroz registraciju.

`AuthResponse = { accessToken, refreshToken, expiresAt, user: { id, username, firstName, lastName, email, role, profileImageUrl } }`

## 3. Šifarnici (generički BaseCRUD)

Za svaki `{resource}` ∈ `training-types`, `fitness-goals`, `fitness-levels`, `genders`:

| Metoda | Ruta | Auth |
|---|---|---|
| GET | `/api/{resource}?name=&page=&pageSize=` | anon (registracija treba dropdownove) → `{ items, totalCount }` |
| GET | `/api/{resource}/{id}` | anon |
| POST | `/api/{resource}` | AdminOnly → kreirani objekat |
| PUT | `/api/{resource}/{id}` | AdminOnly |
| DELETE | `/api/{resource}/{id}` | AdminOnly → 204, ili 409 "Stavka se ne može obrisati jer je u upotrebi." |

`GET /api/roles` (AdminOnly) vraća `[{ value: "Admin", name: "Administrator" }, { value: "Mentor", name: "Mentor" }, { value: "Client", name: "Klijent" }]`.

## 4. Profil (svi prijavljeni)

- `GET /api/user-profile/me` → `UserProfile`:
  ```
  { username, firstName, lastName, email, phoneNumber, dateOfBirth, genderId, genderName, role, profileImageUrl,
    mentor?: { trainingTypeId, trainingTypeName, nickname, bio, yearsOfExperience, monthlyPrice, specializationIds, specializationNames, status },
    client?: { weightKg, heightCm, fitnessLevelId, fitnessLevelName, trainingExperienceYears, fitnessGoalId, fitnessGoalName, goalDescription, preferredTrainingTypeId } }
  ```
- `PUT /api/user-profile/me` — isti oblik bez read-only polja (`role`, `status`, `*Name`). Lozinka se ovdje ne mijenja. Promjena mentor polja ne vraća mentora u Pending. Odgovor: `UserProfile`.
- `POST /api/user-profile/me/photo` (multipart `file`, jpg/png ≤5 MB) → `{ profileImageUrl }`.
- `DELETE /api/user-profile/me/photo` → 204.

## 5. Admin (AdminOnly)

Korisnici:
- `GET /api/admin/users?search=&role=&isActive=` → `[AdminUser]`, gdje je `AdminUser = { id, username, firstName, lastName, fullName, email, phoneNumber, role, isActive, createdAt, profileImageUrl }` (obrisani se ne vraćaju).
- `GET /api/admin/users/{id}` → `AdminUserDetail = AdminUser + { dateOfBirth, genderId, mentor?, client? }` (isti pod-objekti kao `UserProfile`).
- `PUT /api/admin/users/{id}` → `{ firstName, lastName, username, email, phoneNumber?, dateOfBirth, genderId, role, mentor?, client? }`. Promjena uloge u Mentor ili Client zahtijeva odgovarajući pod-objekat ako profil ne postoji (inače 400 s jasnom porukom). Lozinka se ne traži. Odgovor: `AdminUserDetail`.
- `PUT /api/admin/users/{id}/reset-password` → `{ newPassword, confirmPassword }` → `{ message }`.
- `PUT /api/admin/users/{id}/block` / `.../unblock` → `AdminUser`. Blokiranje završava sve sesije korisnika (access tokeni odmah `401`, refresh tokeni opozvani); nakon odblokiranja je potrebna nova prijava.
- `DELETE /api/admin/users/{id}` → 204. Soft delete: `isDeleted = true`, `isActive = false`, tokeni se opozivaju, a unblock ne može vratiti obrisanog korisnika. Za mentora se sve pretplate AwaitingMentor/Active prekidaju (Cancelled, `statusReason` "Mentor je uklonjen sa platforme.") i klijenti dobijaju obavijest. Njihovi planovi ostaju vidljivi klijentima (read-only) i više se ne mogu uređivati ni obnavljati. Uplata čija je naplata osporena kod banke klijenta se ne vraća (status `Disputed`, vidi admin otkazivanje).

Mentori i zahtjevi:
- `GET /api/admin/mentors?search=&trainingTypeId=&isActive=` → `[AdminMentor]`, gdje je `AdminMentor = { userId, mentorProfileId, fullName, nickname, username, email, profileImageUrl, trainingTypeName, monthlyPrice, averageRating, reviewCount, activeSubscribers, isActive }` (samo Approved mentori čiji korisnik ima ulogu Mentor).
- `GET /api/admin/mentor-requests?search=&trainingTypeId=` → `[MentorRequest]` (Pending), gdje je `MentorRequest = { mentorProfileId, userId, fullName, email, trainingTypeName, yearsOfExperience, requestedAt, certificateCount }`.
- `GET /api/admin/mentor-requests/{mentorProfileId}` → `MentorRequestDetail = MentorRequest + { nickname, bio, dateOfBirth, age, phoneNumber, monthlyPrice, specializationNames, profileImageUrl, certificates: [Certificate] }`, gdje je `Certificate = { id, fileName, fileUrl, uploadedAt, isVerified }`, a `fileUrl` je uvijek `/api/certificates/{id}/file`.
- `PUT /api/admin/mentor-requests/{mentorProfileId}/approve` → `{ message }`. Obavještava mentora (in-app + email).
- `PUT /api/admin/mentor-requests/{mentorProfileId}/reject` → `{ reason (10–500) }` → `{ message }`.
- `PUT /api/admin/certificates/{id}/verify` → `Certificate`.
- `GET /api/certificates/{id}/file` (MentorOrAdmin: administrator ili mentor vlasnik certifikata) → sadržaj fajla sa `Content-Type` `application/pdf` / `image/png` / `image/jpeg` i `Content-Disposition: inline; filename="<originalni naziv>"` (uz `filename*=UTF-8''...` za dijakritike). Drugi mentor ili klijent → `403` "Nemate pristup ovom certifikatu.", bez tokena → `401`, nepostojeći certifikat ili fajl → `404`. Certifikati se čuvaju izvan `wwwroot` i nisu dostupni kao statički fajlovi.
- `GET /api/admin/mentors/{mentorProfileId}/certificates` → `[Certificate]`.

Klijenti:
- `GET /api/admin/clients?search=&fitnessGoalId=&isActive=` → `[AdminClient]`, gdje je `AdminClient = { userId, clientProfileId, fullName, username, email, profileImageUrl, fitnessGoalName, fitnessLevelName, activeMentorName?, isActive }`.

Pretplate:
- `GET /api/admin/subscriptions?search=&status=` → `[AdminSubscription]`, gdje je `AdminSubscription = { id, clientFullName, mentorFullName, trainingTypeName, status, price, currency, createdAt, startDate, endDate, statusReason }`.
- `PUT /api/admin/subscriptions/{id}/cancel` → `{ reason (5–300) }` → `AdminSubscription`. Dozvoljeno za PendingPayment, AwaitingMentor i Active. Plaćen zahtjev koji mentor nije prihvatio (AwaitingMentor) se vraća preko Stripe-a (Payment `Refunded`), a obavijest klijentu navodi vraćeni iznos; ako povrat ne uspije → `400` "Otkazivanje nije moguće: povrat uplate klijentu nije uspio. Pokušajte ponovo." i ništa se ne mijenja. Active i PendingPayment se otkazuju bez povrata. Nedovršena uplata se zatvara na Stripe-u (naplaćena se vraća, plativ PaymentIntent se otkazuje). Klijent uvijek dobija obavijest, a mentor samo ako je zahtjev vidio (pretplata nije bila PendingPayment). Ako je naplata osporena kod banke klijenta (Stripe dispute, `charge_disputed`), Stripe povrat ne dozvoljava: otkazivanje se ipak izvršava, uplata dobija status `Disputed` (bez `refundedAt`, ne računa se u zaradu, povrat se ne ponavlja), a obavijest klijentu navodi: "Uplata od {iznos} je osporena kod vaše banke, pa se ne vraća automatski. O povratu odlučuje postupak osporavanja." Ishod spora i eventualni ručni povrat administrator rješava na Stripe-u.

Izvještaji (`year`/`month` su opcioni, default je tekući mjesec):
- `GET /api/admin/reports/mentors?search=&trainingTypeId=&year=&month=` → `{ items: [MentorReportRow], totals: { activeSubscribers, monthlyEarnings, totalEarnings, timeOnPlatformMinutes, mentorCount } , year, month, currency }`, gdje je `MentorReportRow = { mentorProfileId, fullName, trainingTypeName, activeSubscribers, totalSubscribers, monthlyEarnings, totalEarnings, timeOnPlatformMinutes, averageRating }`.
- `GET /api/admin/reports/mentors/{mentorProfileId}?year=&month=` → `MentorReportRow + { email, monthlyBreakdown: [{ year, month, earnings, newSubscribers, minutesOnPlatform }] (zadnjih 6 mjeseci) }`.
- `GET /api/admin/reports/clients?search=&year=&month=` → `{ items: [ClientReportRow], totals: { activeSubscriptions, totalPaid, completedTrainings, progressEntries, timeOnPlatformMinutes, clientCount }, year, month, currency }`, gdje je `ClientReportRow = { clientProfileId, fullName, activeMentorName, activeSubscriptions, totalSubscriptions, totalPaid, completedTrainings, progressEntries, lastProgressAt, timeOnPlatformMinutes }`.
- `GET /api/admin/reports/clients/{clientProfileId}?year=&month=` → `ClientReportRow + { email, monthlyBreakdown: [{ year, month, paid, completedTrainings, minutesOnPlatform }] }`.
- `GET /api/admin/reports/overview` → `{ clientCount, mentorCount, pendingMentorRequests, activeSubscriptions, monthlyEarnings, currency, earningsLast6Months: [{ year, month, amount }], topMentors: [{ fullName, trainingTypeName, averageRating, activeSubscribers }] (top 5) }`.

Sistemske obavijesti:
- `GET /api/admin/announcements?search=` → `[{ id, title, content, targetRole, createdAt, createdByName, recipientCount }]`.
- `POST /api/admin/announcements` → `{ title, content, targetRole? }`. Kreira Notification za svakog aktivnog korisnika ciljane uloge i email preko outbox → RabbitMQ.
- `PUT /api/admin/announcements/{id}` → ažurira naslov i sadržaj (notifikacije već poslane ostaju).
- `DELETE /api/admin/announcements/{id}` → 204 (briše i vezane nepročitane notifikacije).

## 6. Mentor (MentorOnly, prefiks `/api/mentors/me`)

- `GET /api/mentors/me/certificates` → `[Certificate]` (`fileUrl` = `/api/certificates/{id}/file`, preuzima se sa tokenom mentora). `POST .../certificates` (multipart `files`) → `[Certificate]`. `DELETE .../certificates/{id}` → 204 (bar jedan mora ostati).
- `GET /api/mentors/me/collaboration-requests?search=` → `[CollaborationRequest]`. Uključuje pretplate AwaitingMentor i Active bez objavljenog plana. `CollaborationRequest = { subscriptionId, clientFullName, clientPhotoUrl, status, requestedAt, planId?, planStatus? }`.
- `GET /api/mentors/me/collaboration-requests/{subscriptionId}` → `ClientDescription`:
  ```
  { subscriptionId, status, clientFullName, clientPhotoUrl, age, genderName, weightKg, heightCm, fitnessLevelName,
    trainingExperienceYears, fitnessGoalName, goalDescription, requestedAt,
    questionnaire: { primaryGoal, timeCommitment, healthIssues, medications, weeklySessions, outsideActivity } }
  ```
- `PUT /api/mentors/me/collaboration-requests/{subscriptionId}/accept` → `CollaborationRequest`. Subscription postaje Active sa `startDate = sada` i `endDate = sada + 30 dana`. Klijent dobija obavijest. Idempotentno ako je već Active.
- `PUT /api/mentors/me/collaboration-requests/{subscriptionId}/reject` → `{ reason (10–500) }` → `{ message }`. Status Rejected, uplata se refundira preko Stripe-a (Payment.status = Refunded), klijent dobija obavijest. Osporena naplata (Stripe dispute) ne sprječava odbijanje: uplata dobija status `Disputed` bez povrata, a klijent i mentor dobijaju rečenicu da se osporena uplata ne vraća automatski i da o povratu odlučuje postupak osporavanja. Ostale greške povrata vraćaju `400` "Odbijanje nije moguće: povrat uplate klijentu nije uspio. Pokušajte ponovo.".
- `GET /api/mentors/me/subscribers?search=&status=` → `[Subscriber]`, gdje je `Subscriber = { subscriptionId, clientFullName, clientPhotoUrl, status, startDate, endDate, planId?, planStatus?, lastTrainingAt? }`. Mentor vidi samo plaćene pretplate (`paidAt` postavljen): neplaćena pretplata se ne vraća ni sa `?status=` filterom, a `GET .../subscribers/{id}` i `GET .../collaboration-requests/{id}` za nju vraćaju `404`.
- `GET /api/mentors/me/subscribers/{subscriptionId}` → `ClientDescription + { startDate, endDate, sessions: [TrainingSessionItem], progress: [ProgressEntryItem] }`.

Planovi (`/api/training-plans`):
- `GET /api/training-plans?search=&status=` (MentorOnly) → `[PlanSummary]` za mentorove planove. Pretraga ide po imenu ili prezimenu klijenta. `PlanSummary = { id, subscriptionId, clientFullName, clientPhotoUrl, status, version, filledDays, createdAt, updatedAt, publishedAt, subscriptionStatus, canEdit }`.
- `GET /api/training-plans/by-subscription/{subscriptionId}` (MentorOnly) → `PlanDetail` ili 404.
- `GET /api/training-plans/{id}` (Mentor vlasnik, Admin, ili Klijent vlasnik) → `PlanDetail`. Read-only radi i kad pretplata više nije aktivna.
  ```
  PlanDetail = { id, subscriptionId, mentorFullName, clientFullName, motivationalQuote, status, version, canEdit,
                 createdAt, updatedAt, publishedAt,
                 days: [{ id, dayOfWeek, dayName, trainingDurationMinutes, trainingDescription, nutritionDurationMinutes, nutritionDescription }] }
  ```
  `dayName` je na bosanskom (Ponedjeljak…Nedjelja). `days` sadrži samo popunjene dane.
- `POST /api/training-plans` (MentorOnly) → `{ subscriptionId, motivationalQuote? }` → `PlanDetail` (Draft). 409 ako plan za pretplatu već postoji. Pretplata mora biti Active; ako je AwaitingMentor, backend je prvo automatski prihvati (IZRADI PLAN = prihvatanje). Kreiranje zaključava pretplatu kao i ostali prelazi statusa: ako je zahtjev istovremeno odbijen ili otkazan, kreiranje vraća `400` "Plan se može kreirati samo za aktivnu saradnju." (ne `409`). Pretplata koju klijent nije platio (npr. PendingPayment ili otkazana neplaćena) vraća `404` "Pretplata nije pronađena.", isto kao nepostojeća ili tuđa.
- `PUT /api/training-plans/{id}` (MentorOnly) → `{ motivationalQuote? }` → `PlanDetail`.
- `PUT /api/training-plans/{id}/days/{dayOfWeek}` (MentorOnly) → `{ trainingDurationMinutes, trainingDescription, nutritionDurationMinutes?, nutritionDescription }` → `PlanDetail`. Ako je plan Published, `version++` i klijent dobija obavijest PlanUpdated (najviše jedna obavijest u 10 minuta po planu).
- `DELETE /api/training-plans/{id}/days/{dayOfWeek}` (MentorOnly, samo Draft) → `PlanDetail`.
- `PUT /api/training-plans/{id}/publish` (MentorOnly) → `PlanDetail`. Zahtijeva svih 7 dana, inače 400 "Plan mora imati popunjenih svih 7 dana prije objave." Draft/Archived → Published, obavijest PlanPublished klijentu (in-app + email).
- `PUT /api/training-plans/{id}/archive` (MentorOnly) → `PlanDetail`. Published → Archived.
- Prijelazi statusa idu kroz postojeći State Machine obrazac (`StateMachineServices/TrainingPlans`). Nedozvoljen prijelaz vraća 400 s porukom.
- `canEdit = false` ako pretplata nije Active ili je mentor obrisan.

## 7. Klijent (ClientOnly, osim gdje piše anon)

Mentori i preporuke:
- `GET /api/mentors?trainingTypeId=&search=&sortBy=rating|name|price&sortDirection=asc|desc` (anon) → `[MentorSummary]`, gdje je `MentorSummary = { mentorProfileId, fullName, nickname, profileImageUrl, trainingTypeId, trainingTypeName, averageRating, reviewCount, monthlyPrice, currency, yearsOfExperience, age }`. Vraća samo Approved, aktivne i neobrisane mentore čiji korisnik ima ulogu Mentor.
- `GET /api/mentors/{mentorProfileId}` (anon) → `MentorDetail = MentorSummary + { bio, specializationNames, reviews: [Review] (zadnjih 10) }`.
- `GET /api/mentors/{mentorProfileId}/reviews` (anon) → `[Review]`, gdje je `Review = { id, clientFullName, clientPhotoUrl, rating, comment, createdAt, isMine }`.
- `GET /api/mentors/{mentorProfileId}/similar?take=3` (anon) → `[MentorSummary]` (content-based sličnost mentor↔mentor).
- `GET /api/recommendations/mentors?take=5` → `[{ mentor: MentorSummary, score (0–1), reasons: [string] }]`.

Pretplate i plaćanje:
- `POST /api/subscriptions` → `{ mentorProfileId, questionnaire: { primaryGoal, timeCommitment, healthIssues, medications, weeklySessions, outsideActivity } }` → `Subscription` (PendingPayment). 409 ako klijent već ima PendingPayment/AwaitingMentor/Active pretplatu: "Već imate aktivnu ili započetu saradnju sa mentorom." Postojeću PendingPayment pretplatu kod istog mentora vraća umjesto 409.
- `GET /api/subscriptions/my?status=` → `[Subscription]`, gdje je `Subscription = { id, mentorProfileId, mentorFullName, mentorPhotoUrl, trainingTypeName, status, price, currency, createdAt, startDate, endDate, statusReason, canReview, reviewId?, canRenew, canCancel }`.
- `GET /api/subscriptions/my/{id}` → `Subscription + { questionnaire, payments: [{ amount, currency, purpose, status, createdAt, paidAt }] }`.
- `POST /api/subscriptions/{id}/cancel` → `Subscription`. Dozvoljeno iz PendingPayment ili Active, bez povrata novca. Nedovršena uplata se zatvara na Stripe-u: plativ PaymentIntent se otkazuje, a već naplaćen (potvrda nije stigla) se automatski vraća (`PaymentRefunded`), osim ako je naplata osporena kod banke (tada `Disputed`, bez povrata). Ako Stripe nije dostupan → `400` "Komunikacija sa Stripe servisom nije uspjela. Pokušajte ponovo." i ništa se ne mijenja. Mentor dobija obavijest samo za Active.
- `POST /api/payments/create-intent` → `{ subscriptionId }` → `{ paymentId, clientSecret, publishableKey, amount, currency, purpose }`. Purpose je Initial za PendingPayment i Renewal za Active (produženje +30 dana). Istovremeni zahtjevi za istu pretplatu se izvršavaju jedan za drugim i dobijaju isti PaymentIntent. Zastario PaymentIntent (promijenjena cijena ili stariji od pola `Lifecycle:PaymentReconcileWindowHours`) se otkazuje na Stripe-u i kreira se novi; ako je u međuvremenu plaćen, primjenjuje se i vraća `409` "Ova uplata je već uspješno izvršena. Osvježite prikaz pretplate.". Stripe `409` (isti zahtjev još u obradi) → `409` "Prethodno plaćanje se još obrađuje. Pokušajte ponovo za nekoliko trenutaka."
- `POST /api/payments/{paymentId}/confirm` → `Subscription`. Backend dohvata PaymentIntent sa Stripe-a i tek kad je `succeeded` postavlja Payment Succeeded. Initial prebacuje pretplatu u AwaitingMentor, postavlja `price` pretplate na naplaćeni iznos i šalje mentoru obavijest NewCollaborationRequest (isto važi za webhook, usklađivanje i `create-intent` koji primjenjuje već plaćen raniji PaymentIntent). Renewal produžava `endDate` za 30 dana. Neuspjeh vraća 400 "Plaćanje nije završeno. Pokušajte ponovo."
- `POST /api/payments/webhook` (anon, Stripe potpis se obavezno verifikuje) — ista obrada, idempotentno. Potpisan payload koji nije JSON → `400` "Neispravan Stripe webhook payload."; JSON bez objekta `data.object` se ignoriše (`200`).
- Nema demo/lažnog plaćanja. Bez konfigurisanog Stripe ključa `create-intent` vraća 400 "Stripe plaćanje nije konfigurisano na serveru."

Plan i treninzi:
- `GET /api/training-plans/my-current` → `PlanDetail` za zadnji Published ili Archived plan tekuće Active pretplate. Ako takav ne postoji, uzima se plan zadnje Cancelled pretplate prekinute brisanjem mentora. Inače 404 "Još nemate objavljen plan."
- `POST /api/training-plans/{planId}/days/{dayOfWeek}/sessions` → `{ repetitions (1–10000), note? }` → `TrainingSessionItem`. Dozvoljeno samo za Published plan aktivne saradnje (`canEdit = true`: pretplata Active i mentor nije obrisan); inače `400` "Saradnja je završena, pa se treninzi više ne mogu evidentirati."
- `GET /api/training-plans/{planId}/sessions` (klijent vlasnik ili mentor vlasnik) → `[TrainingSessionItem]`, gdje je `TrainingSessionItem = { id, dayOfWeek, dayName, completedAt, repetitions, note }`.

Napredak (historija treninga):
- `GET /api/progress/years` → `[int]` (godine sa unosima + tekuća godina u vremenskoj zoni platforme).
- `GET /api/progress?year=` → `[ProgressEntryItem]`, gdje je `ProgressEntryItem = { id, year, month, monthName, photoUrl, weightKg, measurements, strength, conditioning, hasPlanSnapshot, createdAt, updatedAt }`.
- `GET /api/progress/{year}/{month}` → `ProgressEntryItem` ili 404.
- `PUT /api/progress/{year}/{month}` → `{ weightKg, measurements, strength, conditioning }` → `ProgressEntryItem` (upsert). Pri kreiranju se snima JSON snapshot trenutnog plana, ali samo za unos tekućeg mjeseca; za prošle mjesece snapshot ostaje prazan i `GET .../plan` vraća `404`. Tekući i budući mjesec se računaju u vremenskoj zoni platforme (`Lifecycle:TimeZoneId`); budući mjesec vraća `400` "Nije moguće unijeti napredak za budući mjesec.".
- `POST /api/progress/{year}/{month}/photo` (multipart `file`) → `ProgressEntryItem`. Unos mora postojati.
- `GET /api/progress/{year}/{month}/plan` → `PlanDetail` iz snapshota ("HISTORIJA PLANA") ili 404.
- `GET /api/progress/chart` → `[{ year, month, weightKg }]` za grafikon.

Recenzije:
- `POST /api/reviews` → `{ subscriptionId, rating, comment }` → `Review`. Dozvoljeno za pretplatu koja je bila prihvaćena (Active, Expired, ili Cancelled nakon prihvatanja), jedna po pretplati.
- `PUT /api/reviews/{id}` → `{ rating, comment }` → `Review`. `DELETE /api/reviews/{id}` → 204. Samo vlastite.

## 8. Zajedničko (svi prijavljeni)

- Notifikacije: `GET /api/notifications?unreadOnly=&search=` → `[{ id, title, body, type, isRead, createdAt }]`. `PUT /api/notifications/{id}/read`, `PUT /api/notifications/read-all`, `GET /api/notifications/unread-count` → `{ count }`.
- Poruke:
  - `GET /api/messages/threads?search=` → `[{ subscriptionId, otherPartyName, otherPartyPhotoUrl, lastMessage, lastMessageAt, unreadCount, canSend }]`. Mentor vidi pretplate AwaitingMentor/Active/Expired/Cancelled koje je klijent platio (`paidAt` postavljen), klijent svoje.
  - `GET /api/messages/threads/{subscriptionId}` → `[{ id, content, sentAt, isMine, senderName }]` i označava tuđe poruke pročitanim, kao i čitaočevu nepročitanu NewMessage obavijest od druge strane (pošiljalac se prepoznaje po korisniku, ne po imenu, pa se obavijesti dvije osobe istog imena ne spajaju; osim ako druga nit sa istom osobom još ima nepročitanih poruka).
  - `POST /api/messages/threads/{subscriptionId}` → `{ content }` → poruka. Dozvoljeno dok je pretplata AwaitingMentor ili Active. Druga strana dobija NewMessage notifikaciju (in-app).
- Aktivnost: `POST /api/activity/heartbeat` → 204. Desktop i mobile ga zovu svakih 60 s dok je aplikacija aktivna i korisnik prijavljen. Backend dodaje najviše 90 s po pozivu u `UserActivity`.

## 9. Asinhrono (RabbitMQ)

- API upisuje `OutboxMessage` u istoj transakciji kao domensku promjenu. `OutboxDispatcher` (hosted service u API-ju) objavljuje `EmailNotificationMessage` na queue `gobeyond.notifications`.
- `GoBeyond.EmailConsumer` (poseban projekat i kontejner) konzumira poruke i šalje email preko SMTP-a (Mailpit u docker-compose podrazumijevano; opcionalno Gmail preko `.env`, vidi Changelog v1.5). Nakon 5 neuspjelih pokušaja poruka ide u dead-letter ili se označava neuspjelom, bez beskonačnog requeue-a. Kad SMTP host nije Mailpit, primaoci na domenama iz `Smtp:SuppressedRecipientDomains` (npr. seed korisnici na `gobeyond.ba`, §10) ili njihovim poddomenama (npr. `edu.gobeyond.ba`) se ne šalju — samo loguje i ack-uje (Changelog v1.5, v1.8). Domena primaoca se čita preko `MailAddress` i poredi u ASCII/IDN obliku koji SMTP klijent stvarno šalje, pa su pokriveni i oblici `x@gobeyond.ba.`, `<x@gobeyond.ba>` i Unicode varijante domene.
- Poruka bez obaveznih polja (`MessageId`, `EventType`, `RecipientEmail`, `Subject`, `Body`) ili sa neispravnim JSON-om ide odmah u `gobeyond.notifications.dead`, nikad kao prazan email. `EmailNotificationMessage` ima i polje `CreatedAtTicks` (vrijeme upisa u outbox, UTC ticks). Ponovna isporuka iste poruke (isti `MessageId` + `CreatedAtTicks` + hash sadržaja) se samo potvrđuje, bez ponovnog slanja: i nakon pada procesa (trajni zapis na `Smtp:SentMessageIdsFilePath`, docker volume) i dok je prvo slanje još u toku. Ako broker otkaže consumer-a (npr. obrisan queue), consumer se sam ponovo poveže. Email se šalje kao `multipart/alternative`: text/plain (UTF-8, base64, CRLF) + jednostavan HTML dio (UTF-8, quoted-printable, bez linkova i slika), sa `Message-ID` na domeni pošiljaoca.
- `SubscriptionLifecycleService` (hosted service u API-ju, interval iz konfiguracije) radi sljedeće:
  - Prvo usklađuje nepotvrđene Stripe uplate (Pending i Failed, starije od 5 min, mlađe od 48 h): naplaćene primjenjuje ili automatski vraća, otkazane označava `Failed`, a plativ PaymentIntent koji se više ne smije platiti (zamijenjen, ili pretplata više ne prima uplatu) otkazuje na Stripe-u. Kad Stripe nije dostupan (prekid veze ili timeout), preostale uplate se provjeravaju u sljedećem ciklusu; greška nikad ne gasi API.
  - Active pretplate s prošlim `endDate` prebacuje u Expired i obavještava klijenta i mentora ("istek saradnje"). Prije isteka na Stripe-u provjerava nepotvrđena produženja (bez obzira na starost): produženje plaćeno prije isteka produžava pretplatu od starog `endDate`, a neplaćeno se otkazuje.
  - Klijentu šalje SubscriptionExpiring 3 dana prije isteka (jednom po periodu; produženje ga resetuje).
  - Mentoru šalje PlanMissing ako je pretplata Active duže od 48 h bez objavljenog plana (najviše jednom dnevno, "izostanak plana").
  - Podsjetnici se šalju za svaku pretplatu posebno, nad zaključanom pretplatom: produženje ili otkazivanje u istom trenutku ne dobija zastario podsjetnik, a greška jedne pretplate ne zaustavlja ostale.
  - Klijentu šalje Inactivity nakon 7 dana bez heartbeat-a (najviše jednom sedmično, "neaktivnost").
  - Ponavlja povrat `RefundPending` uplata u svakom ciklusu. Ako Stripe odgovori da je naplata osporena (`charge_disputed`), uplata prelazi u `Disputed`, klijent jednom dobija obavijest "Uplata je osporena" i povrat se više ne pokušava.

## 10. Seed (demo) podaci

Svi seed korisnici imaju lozinku `test`.

| Korisničko ime | Uloga | Namjena |
|---|---|---|
| `admin` | Admin | desktop, administrator |
| `desktop` | Admin | desktop (alias po uputama) |
| `mentor` | Mentor | desktop mentor, Approved, Weightlifting, ima zahtjeve, pretplatnike, planove i poruke |
| `client` | Client | mobile, Active pretplata kod `mentor`, objavljen plan sa svih 7 dana, 6 mjeseci napretka sa slikama, treninzi, poruke, obavijesti |
| `mobile` | Client | mobile (alias po uputama), sa sličnim podacima |

Dodatno:
- Bar 2 Approved mentora po vrsti treninga, sa slikama, nadimkom, biografijom, specijalizacijama i recenzijama.
- 2 Pending mentora sa certifikatima (stvarni PDF/PNG fajlovi), 1 Rejected i 1 blokiran korisnik.
- Oko 10 klijenata u raznim statusima pretplate. `mentor` ima bar 2 AwaitingMentor zahtjeva.
- Uplate kroz zadnjih 6 mjeseci, UserActivity za zadnjih 60 dana, 2 sistemske obavijesti.

## Changelog

- v1 — inicijalni ugovor (glavni agent).
- v1.1 — backend implementacija (backend agent). Pojašnjenja i sitna odstupanja (sve ostalo je tačno po ugovoru):
  - **Statusi:** svi uspješni `POST`/`PUT` vraćaju `200` sa tijelom (nema `201`). `204` vraćaju: sva brisanja, `POST /api/auth/logout`, `POST /api/activity/heartbeat`, `PUT /api/notifications/{id}/read` i `PUT /api/notifications/read-all`.
  - **Greške:** zauzeto korisničko ime/email i duplikat naziva u šifarniku vraćaju `400` sa greškom po polju (`errors.username` = "Korisničko ime je već zauzeto.", `errors.email` = "Email adresa je već registrovana.", `errors.name`), ne `409`. Ključevi ugniježđenih polja su `mentor.bio`, `client.weightKg`, `questionnaire.primaryGoal`; neispravan JSON tip daje "Neispravan format vrijednosti." pod imenom polja. Pogrešna lozinka pri prijavi: `401` "Pogrešno korisničko ime ili lozinka."; bez tokena/istekao token: `401` "Niste prijavljeni ili je sesija istekla. Prijavite se ponovo."; pogrešna uloga: `403` "Nemate pravo pristupa ovoj akciji."; nepostojeća ruta: `404` `{ message }`.
  - **Datumi** su UTC sa sufiksom `Z`. Valuta je `usd`.
  - **Refresh token** se rotira: stari token nakon `POST /api/auth/refresh` više ne važi. Promjena uloge, blokiranje, brisanje i admin reset lozinke opozivaju refresh tokene, a postojeći access token odmah prestaje važiti (`401`).
  - **Izvještaji:** `monthlyEarnings`, `timeOnPlatformMinutes` i `completedTrainings` (klijent) računaju se za odabrani `year`/`month`; `totalEarnings`, `totalSubscribers`, `totalPaid`, `totalSubscriptions` i `progressEntries` su ukupni. `activeSubscribers`/`activeSubscriptions` su trenutno stanje. Zarada = uspješne, nevraćene uplate po datumu plaćanja. Detaljni izvještaji (`/reports/mentors/{id}`, `/reports/clients/{id}`) dodatno vraćaju `year`, `month`, `currency`; `monthlyBreakdown` ima 6 mjeseci zaključno sa odabranim, od najstarijeg.
  - **Dodatna polja (samo dodana, ništa nije uklonjeno):** `ClientProfileInfo.preferredTrainingTypeName`, `AdminUserDetail.genderName`. `POST /api/subscriptions` i `POST /api/payments/{id}/confirm` vraćaju `Subscription` sa detaljima (`questionnaire`, `payments`), isto kao `GET /api/subscriptions/my/{id}`.
  - **Admin:** administrator ne može blokirati, obrisati ni promijeniti ulogu vlastitog naloga (`400`). Promjena uloge nije dozvoljena dok korisnik ima PendingPayment/AwaitingMentor/Active saradnju (`400`, `errors.role`). Mentor kojeg admin kreira promjenom uloge je odmah Approved. `GET /api/admin/mentor-requests/{id}` radi za svakog neobrisanog mentora (i odobrenog, radi pregleda certifikata); approve/reject rade samo za Pending (`400` "Ovaj zahtjev je već obrađen."). Brisanje mentora: AwaitingMentor/Active/PendingPayment pretplate prelaze u Cancelled ("Mentor je uklonjen sa platforme."), uplate za neprihvaćene zahtjeve (AwaitingMentor) se vraćaju, klijent dobija obavijest i `GET /api/training-plans/my-current` mu i dalje vraća plan (`canEdit=false`). Brisanje klijenta otkazuje njegove otvorene pretplate i obavještava mentora.
  - **Planovi (State Machine):** Draft dozvoljava izmjenu i brisanje dana i objavu (7 dana); Published dozvoljava izmjenu dana i motivacijske poruke (svaka izmjena `version++`, PlanUpdated najviše jednom u 10 min) i arhiviranje; Archived dozvoljava izmjenu dana i ponovnu objavu (`version++`, obavijest PlanPublished). Nedozvoljeno: `400` "Akcija \"...\" nije dozvoljena za plan u statusu \"...\"". Klijent ne vidi Draft plan (`403`).
  - **Mentor:** `GET /api/mentors/me/subscribers` bez `status` filtera ne vraća PendingPayment i Rejected. Zadnji certifikat se ne može obrisati (`400`).
  - **Poruke:** admin dobija prazan niz za `threads` i `403` za pojedinačnu nit. Druga strana ima najviše jednu nepročitanu NewMessage obavijest po pošiljaocu, sa naslovom `Nova poruka: {ime i prezime}` i zadnjom porukom kao tekstom.
  - **Plaćanja:** povrat kod odbijanja zahtjeva ide preko Stripe Refund API-ja; seed uplate (`seed_pi_...`, nikad naplaćene preko Stripe-a) samo se označavaju kao Refunded. Webhook bez podešenog `Payments__WebhookSecret` ili sa neispravnim potpisom vraća `400`.
  - **Heartbeat:** pauza duža od 180 s tretira se kao nova sesija (pripisuje se 0 s).
  - **Emailovi (outbox → RabbitMQ → EmailConsumer → Mailpit)** šalju se i za registraciju klijenta ("Dobrodošli na GoBeyond") i mentora ("Registracija je primljena"), pored obavijesti iz ugovora.
  - Parametar `take` kod `similar` i `recommendations` je ograničen na 1–20. Paginacija šifarnika: `page` od 1, `pageSize` 1–100 (default 50).
- v1.2 — plaćanja i sitne dorade (backend agent):
  - **Novi enum stringovi (UI mora podržati nepoznate/nove vrijednosti):** `Payment.status` može biti i `RefundPending`, a `Notification.type` može biti i `PaymentRefunded`.
  - **Uplata koja se ne može primijeniti** (npr. uspješno plaćanje stiglo nakon što je pretplata otkazana/istekla/odbijena, ili duplikat početne uplate) se više ne prihvata tiho: backend je odmah vraća preko Stripe-a (`Refunded`), klijent dobija obavijest `PaymentRefunded` (in-app + email), a pretplata ostaje nepromijenjena. Ako Stripe povrat ne uspije, uplata dobija status `RefundPending` (trajna oznaka), klijent dobija obavijest da je povrat u obradi, a `SubscriptionLifecycleService` ponavlja povrat u svakom ciklusu. `RefundPending` uplate se ne računaju u zaradu u izvještajima.
  - **`POST /api/payments/create-intent`:** ako prethodni PaymentIntent iste pretplate i namjene još traje (`processing` ili drugi nezavršen status), vraća `409` "Prethodno plaćanje se još obrađuje. Pokušajte ponovo za nekoliko trenutaka." i NE kreira novi. Ako je prethodni PaymentIntent u međuvremenu uspio, backend ga primijeni i vraća `409` "Ova uplata je već uspješno izvršena. Osvježite prikaz pretplate.". PaymentIntent koji čeka karticu (`requires_payment_method/confirmation/action`) se ponovo koristi (ista `paymentId` i `clientSecret`); nova uplata se kreira tek kad je stari `canceled` ili mu je iznos zastario (promjena cijene).
  - **Provjera PaymentIntent-a:** `confirm` i webhook prije primjene provjeravaju `metadata.subscriptionId`, `metadata.purpose`, iznos i valutu u odnosu na uplatu. Nepodudaranje → `400` "Podaci o plaćanju na Stripe-u ne odgovaraju ovoj uplati, pa plaćanje nije primijenjeno. Kontaktirajte podršku." (uplata se ne primjenjuje, greška se loguje). `confirm` je idempotentan: za uplatu koja je već `Succeeded`, `Refunded` ili `RefundPending` samo vraća pretplatu.
  - **Idempotency-Key** se šalje na svaki Stripe POST (kreiranje PaymentIntent-a: `create-intent:{subscriptionId}:{purpose}:{pokušaj}:{iznosUCentima}`, povrat: `refund:{paymentId}`), pa ponovljen zahtjev ne kreira drugi PaymentIntent niti drugi povrat. Nema promjene API-ja.
  - **Neuspio povrat se više ne ignoriše:** odbijanje zahtjeva (`PUT /api/mentors/me/collaboration-requests/{id}/reject`) vraća `400` "Odbijanje nije moguće: povrat uplate klijentu nije uspio. Pokušajte ponovo.", a brisanje mentora (`DELETE /api/admin/users/{id}`) `400` "Brisanje nije moguće: povrat uplate klijentu nije uspio. Pokušajte ponovo." — u oba slučaja ništa se ne mijenja. Seed uplate (`seed_pi_...`) se i dalje vraćaju lokalno, bez Stripe-a.
  - Politika `MentorOrAdmin` iz §0 je uklonjena jer je nijedan endpoint ne koristi (`GET /api/training-plans/{id}` i `.../sessions` provjeravaju vlasništvo u servisu).
  - Poruke o veličini fajla (uključujući `413`/prevelik zahtjev) sada se računaju iz `Uploads:MaxFileSizeBytes`.
  - Baza: dodani check constraint-i `ClientProfiles.TrainingExperienceYears` 0–60 i `DayPlans.NutritionDurationMinutes` NULL ili 1–1440 (i dalje jedna `InitialCreate` migracija; primjenjuju se pri sljedećem resetu baze).
- v1.3 — sigurni certifikati i nadogradnja paketa (backend agent):
  - **Certifikati više nisu javni.** Novi endpoint `GET /api/certificates/{id}/file` (administrator ili mentor vlasnik; ostali `403`, bez tokena `401`, nepostojeći `404`) vraća fajl sa ispravnim `Content-Type` i `Content-Disposition: inline; filename="..."`. U svim DTO-ima `Certificate.fileUrl` je sada `/api/certificates/{id}/file` (relativno; klijent ga prefiksira base URL-om i šalje `Authorization: Bearer` header, npr. `Image.network(url, headers: ...)` ili preuzimanje bajtova za PDF prikaz). Stari URL-ovi `/seed/certificates/...` i `/uploads/certificates/...` vraćaju `404`.
  - Uploadovani certifikati se čuvaju izvan `wwwroot` u `Uploads:PrivateRoot` (Docker volume `gobeyond-private-files`), a demo certifikati u `SeedFiles/certificates` (dio image-a, nisu javni). Postojeći certifikati iz ranije baze i dalje se preuzimaju kroz novi endpoint (automatski fallback na stare lokacije).
  - **Profilne slike i slike napretka ostaju javne po nepogodivom GUID URL-u** (`/uploads/profile/{guid}.png`, `/uploads/progress/{guid}.png`, `/seed/...`): prikazuju se na mnogo mjesta u obje aplikacije (liste mentora, recenzije, poruke) kroz obične `Image.network` pozive, nemaju osjetljiv sadržaj kao certifikati (lični dokumenti), a GUID naziv se ne može pogoditi niti izlistati (direktorij se ne lista).
  - Politika `MentorOrAdmin` je ponovo u upotrebi (za `GET /api/certificates/{id}/file`).
  - NuGet paketi nadograđeni na zadnje verzije kompatibilne sa net9.0 (Microsoft.* 9.0.20, RabbitMQ.Client 7.2.2, System.IdentityModel.Tokens.Jwt 8.23.0, Swashbuckle.AspNetCore 10.2.3, xunit 2.9.3); bez promjene API-ja i baze.
- v1.4 — provjera plaćanja sa pravim Stripe-om (TEST mod) i lokalni `.env` (backend agent). Rute, tijela i odgovori su nepromijenjeni:
  - **Idempotency-Key je jedinstven i izvan jedne baze.** Stripe pamti ključ 24 h po Stripe nalogu, a Id-evi iz baze se ponavljaju nakon `docker compose down -v` ili na drugoj instalaciji sa istim ključevima. Tada je Stripe odbijao zahtjev (`idempotency_error`, pa create-intent i odbijanje vraćaju `400`) ili vraćao stari, možda već plaćeni PaymentIntent. Novi format: kreiranje PaymentIntent-a `create-intent:{subscriptionId}:{createdAt pretplate, yyyyMMddTHHmmssfffffff}:{purpose}:{pokušaj}:{iznosUCentima}`, povrat `refund:{paymentIntentId}` (umjesto `refund:{paymentId}`). Ponovljen ili istovremen zahtjev i dalje dobija isti ključ.
  - **Povrat već vraćene naplate.** Ako je naplata na Stripe-u već vraćena (npr. ručno iz Stripe Dashboard-a, ili ponovni pokušaj nakon isteka ključa), Stripe odgovara `charge_already_refunded`. Backend to sada tretira kao izvršen povrat (uplata `Refunded`), pa odbijanje zahtjeva, brisanje mentora i ponavljanje `RefundPending` povrata više ne padaju trajno.
  - **Webhook `payment_intent.payment_failed` više ne označava uplatu kao `Failed`.** Kod odbijene kartice Stripe vraća PaymentIntent u `requires_payment_method` i on se može ponovo platiti, pa uplata ostaje `Pending`, a `create-intent` vraća isti `paymentId`/`clientSecret` (isto kao bez webhook-a). `Failed` se postavlja samo za otkazan PaymentIntent (`payment_intent.canceled`, ili `canceled` pri confirm/create-intent).
  - **Usklađivanje uplata bez webhook-a.** `SubscriptionLifecycleService` u svakom ciklusu, prije označavanja isteklih pretplata, provjerava na Stripe-u `Pending` uplate starije od `Lifecycle:PaymentReconcileAfterMinutes` (5) i mlađe od `Lifecycle:PaymentReconcileWindowHours` (48). To pokriva slučaj kad je kartica naplaćena, a `POST /api/payments/{id}/confirm` nikad nije stigao (aplikacija ugašena, prekid mreže, klijent otkazao PendingPayment pretplatu prije potvrde), a webhook nije podešen. Uspješna uplata se primjenjuje istom provjerom kao confirm: pretplata prelazi u AwaitingMentor, a za Renewal se produžava. Ako pretplata više nije u odgovarajućem statusu (npr. Cancelled), uplata se automatski vraća (`Refunded` + obavijest `PaymentRefunded`). Otkazan PaymentIntent postaje `Failed`. Ranije je takva uplata ostajala naplaćena bez usluge i bez povrata.
  - **Konfiguracija / lokalno pokretanje:** API i `GoBeyond.EmailConsumer` pri startu (prije čitanja konfiguracije) traže root `.env` u trenutnom folderu ili folderu aplikacije i njihovim roditeljima (do korijena repozitorija) i učitavaju ga kao environment varijable. Tako `dotnet run` / Visual Studio / VS Code dobijaju iste Stripe ključeve kao docker-compose. Već postavljene varijable (docker-compose `environment`/`env_file`, `launchSettings.json`, sistemske) imaju prednost i ne pregazuju se. Format je `KLJUC=vrijednost`: komentari `#`, opcioni `export `, vrijednosti u `"..."`/`'...'`, `=` unutar vrijednosti je dozvoljen, a prazna vrijednost znači "nije postavljeno". U Docker image-u `.env` ne postoji (`.dockerignore`). Integracijski testovi ga isključuju varijablom `GOBEYOND_SKIP_DOTENV=1`.
  - **Valuta ostaje `usd`** (skice prikazuju $). Na Stripe nalogu sa EUR valutom naplata u USD se konvertuje u EUR uz naknadu za konverziju (~2 %). Stripe traži da iznos nakon konverzije bude najmanje 0,50 EUR, pa je stvarni minimum oko 0,60 USD. Minimalna cijena mentora je 1 USD, pa to nije ograničenje.
- v1.5 — stvarna dostava emaila preko Gmail-a (EMAIL agent). Rute, tijela i odgovori su nepromijenjeni:
  - **`docker-compose.yml`:** `email-consumer` je ranije uvijek hardkodirao `Smtp__Host: mailpit` / `Smtp__Port: 1025` u `environment:`, pa je pregazio `Smtp__Host`/`Smtp__Port` iz `.env` (compose `environment` pobjeđuje `env_file`). Sada su to `${Smtp__Host:-mailpit}` / `${Smtp__Port:-1025}` — bez tih varijabli u `.env` ponašanje je identično kao prije (Mailpit), a kad su postavljene (npr. na Gmail), koriste se. `api` servis email ne šalje (samo piše u outbox), pa nije mijenjan.
  - **Nova postavka `Smtp:SuppressedRecipientDomains`** (`appsettings.Shared.json`, podrazumijevano `[ "gobeyond.ba" ]`) štiti stvaran SMTP nalog i treće strane od seed/demo adresa (`{username}@gobeyond.ba`, vidi §10) koje nisu prave poštanske adrese pod našom kontrolom. `GoBeyond.EmailConsumer` primjenjuje je SAMO kada `Smtp:Host` NIJE Mailpit (host `mailpit`/`localhost`/`127.0.0.1`): u Mailpitu email nikad ne napušta mašinu, pa je demo poštu seed korisnika i dalje korisno vidjeti tamo bez ograničenja; čim je host stvaran SMTP servis, primalac na navedenoj domeni se ne šalje — poruka se loguje na nivou Information (samo domena, bez tijela/pune adrese) i odmah potvrđuje (ack), bez retry-ja i bez dead-lettera.
  - Gmail specifično (provjereno stvarnom dostavom preko IMAP-a): STARTTLS na portu 587 (`Smtp__UseSsl=true`) sa `System.Net.Mail.SmtpClient` radi bez izmjena; `From` mora biti tačno prijavljeni Gmail nalog (`Smtp__Username`), inače ga Gmail sam prepiše; UTF-8 naslov/tijelo (već postojeći `SubjectEncoding`/`BodyEncoding`) ispravno prikazuje bosanske znakove (č, ć, š, đ, ž). Pogrešna Gmail lozinka/app password baca `SmtpException` koja se loguje bez ikakvih kredencijala i ide kroz postojeći `MaxAttempts`/dead-letter mehanizam (§9) — nema beskonačnog retry-ja.
  - `.env.example` i README dokumentuju Gmail podešavanje (2-Step Verification + App Password) i supresiju; `.env` (gitignored) nije mijenjan u ovom dokumentu.
- v1.6 — ispravke nakon testiranja (runda 1). Rute i tijela su nepromijenjeni:
  - **Jedna otvorena saradnja i pri istovremenim zahtjevima:** filtrirani jedinstveni indeks nad `Subscriptions.ClientProfileId` za PendingPayment/AwaitingMentor/Active (migracija `SubscriptionConsistency` otkazuje zaostale neplaćene duplikate sa razlogom "Dvostruka pretplata je automatski otkazana."). Istovremeni `POST /api/subscriptions` vraća postojeću PendingPayment pretplatu ili `409`.
  - **Prelazi statusa pretplate** (plaćanje, prihvatanje, odbijanje, otkazivanje, brisanje korisnika, istek) se izvršavaju jedan za drugim nad zaključanom pretplatom: istovremeno prihvatanje i odbijanje daju jedan ishod (drugi zahtjev `400`), a otkazivanje se nikad tiho ne poništava.
  - **Admin otkazivanje AwaitingMentor zahtjeva vraća uplatu** (§5). Tekstovi otkazivanja i brisanja mentora navode stvarno vraćeni iznos.
  - **Zamijenjeni i prestari PaymentIntent-i** se otkazuju na Stripe-u; usklađivanje provjerava i `Failed` uplate; `Payment.paidAt` je vrijeme naplate na Stripe-u.
  - **Stripe timeout** `Payments:RequestTimeoutSeconds` (30 s) → `400` "Komunikacija sa Stripe servisom nije uspjela. Pokušajte ponovo."; `SubscriptionLifecycleService` više ne gasi API zbog greške Stripe-a.
  - **Mentor ne vidi neplaćene pretplate** (lista pretplatnika, detalji, niti poruka) i ne dobija obavijest o otkazivanju neplaćene pretplate.
  - **Tekstovi obavijesti:** datumi u zoni platforme bez dvostruke tačke, razlog uvijek završava tačkom, rodno neutralne formulacije.
  - **Validacija:** slobodni tekst se trimuje prije provjere dužine; `mentor.monthlyPrice` smije imati najviše dvije decimale (`400` "Mjesečna cijena može imati najviše dvije decimale.").
  - **Treninzi** se mogu evidentirati samo dok je saradnja aktivna; **snapshot plana** uz unos napretka samo za tekući mjesec.
  - Nove postavke: `Payments:RequestTimeoutSeconds` (30), `Lifecycle:TimeZoneId` ("Europe/Sarajevo"), `Lifecycle:StartupDelaySeconds` (20).
- v1.7 — ispravke nakon testiranja (runda 2). Rute i tijela su nepromijenjeni:
  - **Security stamp i sesije:** access token nosi tvrdnje `stamp` i `sid`. Admin reset lozinke, blokiranje, brisanje, promjena uloge i vlastita promjena lozinke mijenjaju stamp: raniji access tokeni odmah dobijaju `401` "Sesija više nije važeća. Prijavite se ponovo." i ne oživljavaju nakon odblokiranja ili vraćanja uloge. Token izdat prije v1.7 (bez `stamp`) dobija jedan `401`, a aplikacija ga obnovi refresh-om (migracija `AddUserSecurityStamp`).
  - **Refresh token** važi samo uz stamp sa kojim je izdat i rotira se atomski: od više istovremenih `POST /api/auth/refresh` sa istim tokenom uspijeva tačno jedan.
  - **Vlastita promjena lozinke** završava sesije na drugim uređajima, a uređaj koji je promijenio lozinku ostaje prijavljen.
  - Istovremena registracija, admin izmjena korisnika ili izmjena vlastitog profila sa istim korisničkim imenom ili emailom vraća `400` (`errors.username`/`errors.email`), nikad `500`.
  - Istekao token: `401` "Niste prijavljeni ili je sesija istekla. Prijavite se ponovo."; klijent koji traži certifikat: `403` "Nemate pristup ovom certifikatu.".
  - Parametri pretrage `search`/`name` se skraćuju na 100 znakova.
  - **Mentorski profil** je vidljiv i dostupan za novu saradnju samo dok korisnik ima ulogu Mentor; promjena uloge nije dozvoljena dok postoji otvorena saradnja (`400` `errors.role`), ni kad se pretplata otvara u istom trenutku.
  - **NewMessage obavijest:** naslov `Nova poruka: {ime i prezime}`; otvaranje niti je označava pročitanom.
  - **Razlozi preporuke** su rodno neutralni ("Specijalizacija: {cilj}") sa ispravnim oblikom godina ("22 godine iskustva").
  - **Kreiranje plana** i **podsjetnici** rade nad zaključanom pretplatom; **napredak** računa tekući mjesec u zoni platforme; **iznosi** u tekstovima sa decimalnim zarezom.
  - Poruke administratoru: "Mentorski nalog ({ime i prezime}) je odobren." i "Zahtjev za mentorski nalog ({ime i prezime}) je odbijen."
- v1.8 — ispravke nakon ponovnog testiranja (runda 3). Rute i tijela su nepromijenjeni:
  - **Promjene lozinke i sesija se ne preklapaju:** vlastita promjena lozinke, admin reset, blokiranje, odblokiranje, brisanje i izmjena korisnika izvršavaju se jedna za drugom nad zaključanim korisnikom. Promjena provjerena lozinkom koja je u međuvremenu zamijenjena vraća `400` `errors.currentPassword`, pa se admin reset nikad ne gubi; od dvije istovremene promjene uspijeva tačno jedna i njen uređaj ostaje prijavljen. Odblokiranje korisnika obrisanog u istom trenutku vraća `404`.
  - **Istovremena izmjena istih podataka** koju SQL Server prekine (deadlock, istek čekanja na zaključavanje) vraća `400` "Isti podaci se upravo mijenjaju u drugom zahtjevu. Pokušajte ponovo." umjesto `500`.
  - **Validacija:** ime i prezime (registracija, profil, admin izmjena) i naziv/opis u šifarnicima trimuju se prije provjere dužine. Ime od jednog znaka sačuvano ranije ostaje nepromijenjeno, a pri sljedećem snimanju profila korisnik ga mora ispraviti.
  - **Novi status uplate `Disputed`** (UI prikazuje "Osporeno"): povrat naplate osporene kod banke klijenta (Stripe `charge_disputed`) nije moguć, pa se odbijanje zahtjeva, admin otkazivanje i brisanje korisnika ipak izvršavaju bez povrata te uplate, `RefundPending` uplata sa osporenom naplatom se više ne ponavlja, a klijent dobija obavijest da o povratu odlučuje postupak osporavanja.
  - **Cijena pretplate** nakon plaćanja je naplaćeni iznos početne uplate (i kad je ponovni ulazak u plaćanje u međuvremenu postavio novu cijenu mentora).
  - **NewMessage obavijest** pamti pošiljaoca (migracija `AddNotificationSender`, kolona `Notifications.SenderUserId`): obavijesti dvije osobe istog imena se ne spajaju. Starije obavijesti se i dalje prepoznaju po imenu u naslovu.
  - `POST /api/training-plans` za neplaćenu pretplatu vraća `404` "Pretplata nije pronađena." (ranije `400`).
  - **Tekstovi:** razlog odbijanja mentorskog naloga u obavijesti i emailu završava tačkom; ocjena u razlogu preporuke ima decimalni zarez ("Visoka ocjena klijenata (4,5)"); potvrda admin otkazivanja PendingPayment pretplate na desktopu navodi samo klijenta.
  - **EmailConsumer:** supresija pokriva poddomene i Unicode/IDN oblike zaštićenih domena; poruka bez obaveznih polja ide u dead-letter; ponovna isporuka (nakon pada procesa ili dok je prvo slanje u toku) ne šalje email drugi put (novo polje `CreatedAtTicks`); email je `multipart/alternative` (text + HTML) sa `Message-ID`; consumer se sam ponovo poveže kad ga broker otkaže.

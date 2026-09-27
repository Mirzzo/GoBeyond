# GoBeyond — API ugovor (izvor istine za backend, desktop i mobile)

Verzija 1 (27.09.2026). Backend implementira tačno ovaj ugovor. Ako backend mora odstupiti, ažurira ovaj fajl i upisuje izmjenu u sekciju **Changelog** na dnu, da je UI agenti vide.

## 0. Konvencije

- Base URL: desktop `http://localhost:5000`, Android emulator `http://10.0.2.2:5000`; oba Flutter klijenta čitaju `--dart-define=GO_BEYOND_API_URL=...`.
- JSON je camelCase, enumi su stringovi (`"Active"`), datumi ISO-8601 UTC (`"2026-09-27T10:00:00Z"`), a `dateOfBirth` je `"yyyy-MM-dd"`.
- Slike i fajlovi se vraćaju kao **relativne putanje** (`/uploads/...`, `/seed/...`). Klijent ih prefiksira base URL-om.
- Auth ide preko headera `Authorization: Bearer <accessToken>`. Access token traje 60 min. Refresh ide preko `POST /api/auth/refresh`.
- Liste vraćaju JSON niz, osim šifarnika koji vraćaju `{ "items": [...], "totalCount": n }` (generički BaseCRUD obrazac s nastave).
- Greške:
  - `400` `{ "message": "Provjerite unesene podatke.", "errors": { "email": ["Unesite validnu email adresu (npr. ime@domena.com)."] } }`, gdje su ključevi u `errors` camelCase imena polja.
  - `401` / `403` / `404` / `409` vraćaju `{ "message": "<poruka na bosanskom>" }`.
  - `500` vraća `{ "message": "Došlo je do greške na serveru. Pokušajte ponovo." }`.
- Sve poruke koje backend vraća su na **bosanskom (ijekavica)** i konkretne (npr. "Korisničko ime je već zauzeto.").
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
- `Payment { id, subscriptionId, amount, currency, stripePaymentIntentId, purpose (Initial|Renewal), status (Pending|Succeeded|Failed|Refunded), createdAt, paidAt?, refundedAt? }`
- `TrainingPlan { id, subscriptionId (1:1), mentorProfileId, clientProfileId, motivationalQuote? (≤300), status (Draft|Published|Archived), version (int), createdAt, updatedAt, publishedAt? }`
- `DayPlan { id, trainingPlanId, dayOfWeek (1=Ponedjeljak … 7=Nedjelja), trainingDurationMinutes (1–600), trainingDescription (10–8000), nutritionDurationMinutes? (1–1440), nutritionDescription (10–8000) }` — jedinstveno (trainingPlanId, dayOfWeek).
- `TrainingSession { id, trainingPlanId, dayPlanId, clientProfileId, completedAt, repetitions (1–10000), note? (≤500) }`
- `ProgressEntry { id, clientProfileId, year, month (jedinstveno po klijentu), photoUrl?, weightKg (30–300), measurements (2–300, "Obimi"), strength (2–300, "Snaga"), conditioning (2–300, "Kondicija"), trainingPlanId?, planSnapshotJson?, createdAt, updatedAt }`
- `Review { id, subscriptionId (jedna recenzija po pretplati), clientProfileId, mentorProfileId, rating (1–5), comment (10–1000), createdAt, updatedAt? }`
- `Notification { id, userId, title, body, type, isRead, createdAt, announcementId? }` — type: PlanPublished, PlanUpdated, NewCollaborationRequest, RequestAccepted, RequestRejected, SubscriptionExpiring, SubscriptionExpired, SubscriptionCancelled, MentorApproved, MentorRejected, PlanMissing, Inactivity, NewMessage, Announcement, PaymentSucceeded.
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
| POST | `/api/auth/change-password` | auth | `{ currentPassword, newPassword, confirmPassword }` → `{ message: "Lozinka je uspješno promijenjena." }` |

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
- `PUT /api/admin/users/{id}/block` / `.../unblock` → `AdminUser`. Blokiranje opoziva refresh tokene.
- `DELETE /api/admin/users/{id}` → 204. Soft delete: `isDeleted = true`, `isActive = false`, tokeni se opozivaju, a unblock ne može vratiti obrisanog korisnika. Za mentora se sve pretplate AwaitingMentor/Active prekidaju (Cancelled, `statusReason` "Mentor je uklonjen sa platforme.") i klijenti dobijaju obavijest. Njihovi planovi ostaju vidljivi klijentima (read-only) i više se ne mogu uređivati ni obnavljati.

Mentori i zahtjevi:
- `GET /api/admin/mentors?search=&trainingTypeId=&isActive=` → `[AdminMentor]`, gdje je `AdminMentor = { userId, mentorProfileId, fullName, nickname, username, email, profileImageUrl, trainingTypeName, monthlyPrice, averageRating, reviewCount, activeSubscribers, isActive }` (samo Approved).
- `GET /api/admin/mentor-requests?search=&trainingTypeId=` → `[MentorRequest]` (Pending), gdje je `MentorRequest = { mentorProfileId, userId, fullName, email, trainingTypeName, yearsOfExperience, requestedAt, certificateCount }`.
- `GET /api/admin/mentor-requests/{mentorProfileId}` → `MentorRequestDetail = MentorRequest + { nickname, bio, dateOfBirth, age, phoneNumber, monthlyPrice, specializationNames, profileImageUrl, certificates: [Certificate] }`, gdje je `Certificate = { id, fileName, fileUrl, uploadedAt, isVerified }`.
- `PUT /api/admin/mentor-requests/{mentorProfileId}/approve` → `{ message }`. Obavještava mentora (in-app + email).
- `PUT /api/admin/mentor-requests/{mentorProfileId}/reject` → `{ reason (10–500) }` → `{ message }`.
- `PUT /api/admin/certificates/{id}/verify` → `Certificate`.
- `GET /api/admin/mentors/{mentorProfileId}/certificates` → `[Certificate]`.

Klijenti:
- `GET /api/admin/clients?search=&fitnessGoalId=&isActive=` → `[AdminClient]`, gdje je `AdminClient = { userId, clientProfileId, fullName, username, email, profileImageUrl, fitnessGoalName, fitnessLevelName, activeMentorName?, isActive }`.

Pretplate:
- `GET /api/admin/subscriptions?search=&status=` → `[AdminSubscription]`, gdje je `AdminSubscription = { id, clientFullName, mentorFullName, trainingTypeName, status, price, currency, createdAt, startDate, endDate, statusReason }`.
- `PUT /api/admin/subscriptions/{id}/cancel` → `{ reason (5–300) }` → `AdminSubscription`. Obavještava klijenta i mentora.

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

- `GET /api/mentors/me/certificates` → `[Certificate]`. `POST .../certificates` (multipart `files`) → `[Certificate]`. `DELETE .../certificates/{id}` → 204 (bar jedan mora ostati).
- `GET /api/mentors/me/collaboration-requests?search=` → `[CollaborationRequest]`. Uključuje pretplate AwaitingMentor i Active bez objavljenog plana. `CollaborationRequest = { subscriptionId, clientFullName, clientPhotoUrl, status, requestedAt, planId?, planStatus? }`.
- `GET /api/mentors/me/collaboration-requests/{subscriptionId}` → `ClientDescription`:
  ```
  { subscriptionId, status, clientFullName, clientPhotoUrl, age, genderName, weightKg, heightCm, fitnessLevelName,
    trainingExperienceYears, fitnessGoalName, goalDescription, requestedAt,
    questionnaire: { primaryGoal, timeCommitment, healthIssues, medications, weeklySessions, outsideActivity } }
  ```
- `PUT /api/mentors/me/collaboration-requests/{subscriptionId}/accept` → `CollaborationRequest`. Subscription postaje Active sa `startDate = sada` i `endDate = sada + 30 dana`. Klijent dobija obavijest. Idempotentno ako je već Active.
- `PUT /api/mentors/me/collaboration-requests/{subscriptionId}/reject` → `{ reason (10–500) }` → `{ message }`. Status Rejected, uplata se refundira preko Stripe-a (Payment.status = Refunded), klijent dobija obavijest.
- `GET /api/mentors/me/subscribers?search=&status=` → `[Subscriber]`, gdje je `Subscriber = { subscriptionId, clientFullName, clientPhotoUrl, status, startDate, endDate, planId?, planStatus?, lastTrainingAt? }`.
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
- `POST /api/training-plans` (MentorOnly) → `{ subscriptionId, motivationalQuote? }` → `PlanDetail` (Draft). 409 ako plan za pretplatu već postoji. Pretplata mora biti Active; ako je AwaitingMentor, backend je prvo automatski prihvati (IZRADI PLAN = prihvatanje).
- `PUT /api/training-plans/{id}` (MentorOnly) → `{ motivationalQuote? }` → `PlanDetail`.
- `PUT /api/training-plans/{id}/days/{dayOfWeek}` (MentorOnly) → `{ trainingDurationMinutes, trainingDescription, nutritionDurationMinutes?, nutritionDescription }` → `PlanDetail`. Ako je plan Published, `version++` i klijent dobija obavijest PlanUpdated (najviše jedna obavijest u 10 minuta po planu).
- `DELETE /api/training-plans/{id}/days/{dayOfWeek}` (MentorOnly, samo Draft) → `PlanDetail`.
- `PUT /api/training-plans/{id}/publish` (MentorOnly) → `PlanDetail`. Zahtijeva svih 7 dana, inače 400 "Plan mora imati popunjenih svih 7 dana prije objave." Draft/Archived → Published, obavijest PlanPublished klijentu (in-app + email).
- `PUT /api/training-plans/{id}/archive` (MentorOnly) → `PlanDetail`. Published → Archived.
- Prijelazi statusa idu kroz postojeći State Machine obrazac (`StateMachineServices/TrainingPlans`). Nedozvoljen prijelaz vraća 400 s porukom.
- `canEdit = false` ako pretplata nije Active ili je mentor obrisan.

## 7. Klijent (ClientOnly, osim gdje piše anon)

Mentori i preporuke:
- `GET /api/mentors?trainingTypeId=&search=&sortBy=rating|name|price&sortDirection=asc|desc` (anon) → `[MentorSummary]`, gdje je `MentorSummary = { mentorProfileId, fullName, nickname, profileImageUrl, trainingTypeId, trainingTypeName, averageRating, reviewCount, monthlyPrice, currency, yearsOfExperience, age }`. Vraća samo Approved, aktivne i neobrisane mentore.
- `GET /api/mentors/{mentorProfileId}` (anon) → `MentorDetail = MentorSummary + { bio, specializationNames, reviews: [Review] (zadnjih 10) }`.
- `GET /api/mentors/{mentorProfileId}/reviews` (anon) → `[Review]`, gdje je `Review = { id, clientFullName, clientPhotoUrl, rating, comment, createdAt, isMine }`.
- `GET /api/mentors/{mentorProfileId}/similar?take=3` (anon) → `[MentorSummary]` (content-based sličnost mentor↔mentor).
- `GET /api/recommendations/mentors?take=5` → `[{ mentor: MentorSummary, score (0–1), reasons: [string] }]`.

Pretplate i plaćanje:
- `POST /api/subscriptions` → `{ mentorProfileId, questionnaire: { primaryGoal, timeCommitment, healthIssues, medications, weeklySessions, outsideActivity } }` → `Subscription` (PendingPayment). 409 ako klijent već ima PendingPayment/AwaitingMentor/Active pretplatu: "Već imate aktivnu ili započetu saradnju sa mentorom." Postojeću PendingPayment pretplatu kod istog mentora vraća umjesto 409.
- `GET /api/subscriptions/my?status=` → `[Subscription]`, gdje je `Subscription = { id, mentorProfileId, mentorFullName, mentorPhotoUrl, trainingTypeName, status, price, currency, createdAt, startDate, endDate, statusReason, canReview, reviewId?, canRenew, canCancel }`.
- `GET /api/subscriptions/my/{id}` → `Subscription + { questionnaire, payments: [{ amount, currency, purpose, status, createdAt, paidAt }] }`.
- `POST /api/subscriptions/{id}/cancel` → `Subscription`. Dozvoljeno iz PendingPayment ili Active, bez povrata novca. Mentor dobija obavijest.
- `POST /api/payments/create-intent` → `{ subscriptionId }` → `{ paymentId, clientSecret, publishableKey, amount, currency, purpose }`. Purpose je Initial za PendingPayment i Renewal za Active (produženje +30 dana).
- `POST /api/payments/{paymentId}/confirm` → `Subscription`. Backend dohvata PaymentIntent sa Stripe-a i tek kad je `succeeded` postavlja Payment Succeeded. Initial prebacuje pretplatu u AwaitingMentor i šalje mentoru obavijest NewCollaborationRequest. Renewal produžava `endDate` za 30 dana. Neuspjeh vraća 400 "Plaćanje nije završeno. Pokušajte ponovo."
- `POST /api/payments/webhook` (anon, Stripe potpis se obavezno verifikuje) — ista obrada, idempotentno.
- Nema demo/lažnog plaćanja. Bez konfigurisanog Stripe ključa `create-intent` vraća 400 "Stripe plaćanje nije konfigurisano na serveru."

Plan i treninzi:
- `GET /api/training-plans/my-current` → `PlanDetail` za zadnji Published ili Archived plan tekuće Active pretplate. Ako takav ne postoji, uzima se plan zadnje Cancelled pretplate prekinute brisanjem mentora. Inače 404 "Još nemate objavljen plan."
- `POST /api/training-plans/{planId}/days/{dayOfWeek}/sessions` → `{ repetitions (1–10000), note? }` → `TrainingSessionItem`. Dozvoljeno samo za Published plan.
- `GET /api/training-plans/{planId}/sessions` (klijent vlasnik ili mentor vlasnik) → `[TrainingSessionItem]`, gdje je `TrainingSessionItem = { id, dayOfWeek, dayName, completedAt, repetitions, note }`.

Napredak (historija treninga):
- `GET /api/progress/years` → `[int]` (godine sa unosima + tekuća godina).
- `GET /api/progress?year=` → `[ProgressEntryItem]`, gdje je `ProgressEntryItem = { id, year, month, monthName, photoUrl, weightKg, measurements, strength, conditioning, hasPlanSnapshot, createdAt, updatedAt }`.
- `GET /api/progress/{year}/{month}` → `ProgressEntryItem` ili 404.
- `PUT /api/progress/{year}/{month}` → `{ weightKg, measurements, strength, conditioning }` → `ProgressEntryItem` (upsert). Pri kreiranju se snima JSON snapshot trenutnog plana. Budući mjesec vraća 400.
- `POST /api/progress/{year}/{month}/photo` (multipart `file`) → `ProgressEntryItem`. Unos mora postojati.
- `GET /api/progress/{year}/{month}/plan` → `PlanDetail` iz snapshota ("HISTORIJA PLANA") ili 404.
- `GET /api/progress/chart` → `[{ year, month, weightKg }]` za grafikon.

Recenzije:
- `POST /api/reviews` → `{ subscriptionId, rating, comment }` → `Review`. Dozvoljeno za pretplatu koja je bila prihvaćena (Active, Expired, ili Cancelled nakon prihvatanja), jedna po pretplati.
- `PUT /api/reviews/{id}` → `{ rating, comment }` → `Review`. `DELETE /api/reviews/{id}` → 204. Samo vlastite.

## 8. Zajedničko (svi prijavljeni)

- Notifikacije: `GET /api/notifications?unreadOnly=&search=` → `[{ id, title, body, type, isRead, createdAt }]`. `PUT /api/notifications/{id}/read`, `PUT /api/notifications/read-all`, `GET /api/notifications/unread-count` → `{ count }`.
- Poruke:
  - `GET /api/messages/threads?search=` → `[{ subscriptionId, otherPartyName, otherPartyPhotoUrl, lastMessage, lastMessageAt, unreadCount, canSend }]`. Mentor vidi pretplate AwaitingMentor/Active/Expired/Cancelled, klijent svoje.
  - `GET /api/messages/threads/{subscriptionId}` → `[{ id, content, sentAt, isMine, senderName }]` i označava tuđe poruke pročitanim.
  - `POST /api/messages/threads/{subscriptionId}` → `{ content }` → poruka. Dozvoljeno dok je pretplata AwaitingMentor ili Active. Druga strana dobija NewMessage notifikaciju (in-app).
- Aktivnost: `POST /api/activity/heartbeat` → 204. Desktop i mobile ga zovu svakih 60 s dok je aplikacija aktivna i korisnik prijavljen. Backend dodaje najviše 90 s po pozivu u `UserActivity`.

## 9. Asinhrono (RabbitMQ)

- API upisuje `OutboxMessage` u istoj transakciji kao domensku promjenu. `OutboxDispatcher` (hosted service u API-ju) objavljuje `EmailNotificationMessage` na queue `gobeyond.notifications`.
- `GoBeyond.EmailConsumer` (poseban projekat i kontejner) konzumira poruke i šalje email preko SMTP-a (Mailpit u docker-compose). Nakon 5 neuspjelih pokušaja poruka ide u dead-letter ili se označava neuspjelom, bez beskonačnog requeue-a.
- `SubscriptionLifecycleService` (hosted service u API-ju, interval iz konfiguracije) radi sljedeće:
  - Active pretplate s prošlim `endDate` prebacuje u Expired i obavještava klijenta i mentora ("istek saradnje").
  - Klijentu šalje SubscriptionExpiring 3 dana prije isteka (jednom).
  - Mentoru šalje PlanMissing ako je pretplata Active duže od 48 h bez objavljenog plana (najviše jednom dnevno, "izostanak plana").
  - Klijentu šalje Inactivity nakon 7 dana bez heartbeat-a (najviše jednom sedmično, "neaktivnost").

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
  - **Poruke:** admin dobija prazan niz za `threads` i `403` za pojedinačnu nit. Druga strana ima najviše jednu nepročitanu NewMessage obavijest po pošiljaocu (ažurira se zadnjom porukom).
  - **Plaćanja:** povrat kod odbijanja zahtjeva ide preko Stripe Refund API-ja; seed uplate (`seed_pi_...`, nikad naplaćene preko Stripe-a) samo se označavaju kao Refunded. Webhook bez podešenog `Payments__WebhookSecret` ili sa neispravnim potpisom vraća `400`.
  - **Heartbeat:** pauza duža od 180 s tretira se kao nova sesija (pripisuje se 0 s).
  - **Emailovi (outbox → RabbitMQ → EmailConsumer → Mailpit)** šalju se i za registraciju klijenta ("Dobrodošli na GoBeyond") i mentora ("Registracija je primljena"), pored obavijesti iz ugovora.
  - Parametar `take` kod `similar` i `recommendations` je ograničen na 1–20. Paginacija šifarnika: `page` od 1, `pageSize` 1–100 (default 50).

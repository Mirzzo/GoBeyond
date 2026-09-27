# GoBeyond Mobile (klijent)

Flutter aplikacija za klijente GoBeyond platforme. Pokretanje cijelog sistema, pristupni podaci i Stripe ključevi opisani su u root `README.md`.

## Pokretanje (Android emulator)

```powershell
cd UI/gobeyond_mobile
flutter pub get
flutter run --dart-define=GO_BEYOND_API_URL=http://10.0.2.2:5000
```

- `10.0.2.2` je adresa host računara iz Android emulatora.
- Za fizički uređaj proslijedite IP adresu računara, npr. `--dart-define=GO_BEYOND_API_URL=http://192.168.1.10:5000`.
- Bez `--dart-define` koristi se `http://10.0.2.2:5000`. Adresa je definisana na jednom mjestu, u `lib/core/constants/app_constants.dart`.
- Stripe publishable ključ aplikacija dobija od API-ja pri kreiranju plaćanja, pa se u aplikaciji ne podešava.

Prijava: `mobile` / `test` ili `client` / `test`.

## Funkcionalnosti

- Meni (drawer): Moj profil, Početna, Moj plan, Pretplata, O nama, Ostalo.
- Početna: vrste treninga iz baze i preporučeni mentori (content-based preporuke).
- Mentori po vrsti treninga: pretraga, sortiranje (recenzije, ime, cijena), detalji mentora sa recenzijama i sličnim mentorima.
- KUPI PLAN: upitnik, potvrda i plaćanje preko Stripe PaymentSheet-a; produženje i otkazivanje pretplate.
- Moj plan: plan po danima (trening i ishrana, NASTAVI ČITATI), evidencija ponavljanja nakon treninga.
- Historija treninga po godini i mjesecu: slika, težina, obimi, snaga, kondicija, HISTORIJA PLANA i grafikon težine.
- Profil i promjena lozinke, recenzije mentora, obavijesti, poruke sa mentorom.

## Testovi

```powershell
flutter analyze
flutter test
```

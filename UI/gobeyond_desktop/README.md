# GoBeyond Desktop (Admin/Mentor)

Flutter desktop klijent za GoBeyond admin i mentor panel.

## Prerequisites

- Flutter SDK (Windows desktop enabled)
- Visual Studio 2022 with Desktop development with C++
- Pokrenut GoBeyond API na `http://localhost:5000`

## Run

```powershell
cd UI/gobeyond_desktop
flutter pub get
flutter run -d windows --dart-define=GO_BEYOND_API_URL=http://localhost:5000
```

## API configuration

Base URL koristi `GO_BEYOND_API_URL` (jedino mjesto u kodu: `lib/core/config/app_config.dart`).

Default:
- `http://localhost:5000`

Prijava (test nalozi, lozinka `test` za sve): `admin` / `desktop` (Admin), `mentor` (Mentor). Desktop aplikacija odbija prijavu klijentskih naloga ("Desktop aplikacija je namijenjena administratorima i mentorima.").

## Implementirano (prema docs/api-contract.md)

- Auth: login (username ili email), registracija mentora (multipart, certifikati), refresh-on-401 sa automatskim retry-em, logout, promjena lozinke, heartbeat aktivnosti svakih 60s.
- Obavijesti: bell sa unread brojačem u top baru, ekran Obavijesti (pretraga, pojedinačno/sve označi pročitanim).
- Admin: Zahtjevi za mentora (pregled + certifikati in-app + odobri/odbij), Mentori (pretraga/filter, uredi, izvještaj, blokiraj, resetuj lozinku, obriši), Klijenti (isti obrazac), Korisnici (dodjela/izmjena uloga), Šifarnici (vrste treninga/ciljevi/nivoi spreme/spolovi CRUD), Upravljanje pretplatama (pregled, otkaži), Sistemske obavijesti (CRUD), Kontrolna ploča (pregled + 6-mjesečna zarada + top 5 mentora + izvještaji mentora/klijenata sa PDF preuzimanjem i printanjem).
- Mentor: Zahtjevi za suradnju (pregled opisa klijenta, prihvati/odbij), Izrada/uređivanje trening plana (popup sa 7 dana, TRENING/ISHRANA tabovi, objavi plan), Izrađeni planovi (pregled, uredi, arhiviraj/objavi ponovo), Pretplatnici (detalji, evidencija treninga, napredak, poruke), Poruke (chat po pretplati), Kontrolna ploča (obavijesti + brojevi), profil sa certifikatima.
- PDF izvještaji koriste ugrađeni Noto Sans font (`assets/fonts`) radi ispravnog prikaza č/ć/š/đ/ž, bez oslanjanja na sistemski font.

## Verification

- `flutter analyze` → No issues found
- `flutter test` → svi testovi prolaze (validacija, login forma, plan-day dialog, šifarnici forma)
- `flutter build windows --debug` → zahtijeva Windows Developer Mode (symlink podrška za pluginove)

## Troubleshooting

### CMake cache mismatch (nakon premjestanja projekta)

Ako dobijes gresku tipa:
- `CMakeCache.txt directory is different...`
- `source ... does not match the source used to generate cache`

uradi:

```powershell
cd UI/gobeyond_desktop
flutter clean
Remove-Item -Recurse -Force build\windows\x64 -ErrorAction SilentlyContinue
flutter pub get
flutter run -d windows --dart-define=GO_BEYOND_API_URL=http://localhost:5000
```

### Port 5000 already in use

```powershell
netstat -ano | findstr :5000
taskkill /PID <PID> /F
```

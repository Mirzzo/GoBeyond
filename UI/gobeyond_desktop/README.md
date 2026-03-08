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
flutter run -d windows
```

## API configuration

Base URL je trenutno:
- `http://localhost:5000`

Definisano u:
- `lib/core/network/api_client.dart`

## Trenutno implementirano

- Login prema `/api/auth/login`
- Session token persist/sync
- Role-based desktop shell (Admin/Mentor)
- Otvaranje User Profile ekrana klikom na user ime u top baru
- User profile Save Changes flow prema `/api/user-profile/me`

## Placeholder / u izradi

- Vise admin i mentor dashboard ekrana je trenutno placeholder panel
- Dio backend endpointa za te ekrane vraca `501 Not Implemented`

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
flutter run -d windows
```

### Port 5000 already in use

```powershell
netstat -ano | findstr :5000
taskkill /PID <PID> /F
```

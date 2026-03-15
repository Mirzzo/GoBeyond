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

Base URL koristi `GO_BEYOND_API_URL`.

Default:
- `http://localhost:5000`

Definisano u:
- `lib/core/network/api_client.dart`

## Trenutno implementirano

- Login prema `/api/auth/login`
- Session token persist/sync
- Role-based desktop shell (Admin/Mentor)
- Otvaranje User Profile ekrana klikom na user ime u top baru
- User profile Save Changes flow prema `/api/user-profile/me`
- Admin dashboard overview report + mentor report dialog + CSV export
- Mentor requests screen sa approve/reject akcijama
- Mentors, clients i subscriptions listing + search + block/delete/detail akcije
- Mentor collaboration requests i subscribers pregled
- Mentor create draft / publish plan flow
- Mentor published plans pregled

## Verification

- `flutter analyze lib test` prolazi
- `flutter test test/widget_test.dart` prolazi

## Napomena

- Desktop MVP vise ne zavisi od placeholder panela ni `501` endpointa za glavne admin/mentor tokove.
- API base URL dolazi iz `--dart-define`, tako da isti build moze gadjati i drugi backend bez izmjene koda.

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

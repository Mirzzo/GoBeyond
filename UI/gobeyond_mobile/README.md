# GoBeyond Mobile (Client)

Flutter mobile klijent za client flow u GoBeyond aplikaciji.

## Prerequisites

- Flutter SDK
- Pokrenut GoBeyond API na `http://localhost:5000` ili drugi URL proslijedjen preko `--dart-define`

## Run

```powershell
cd UI/gobeyond_mobile
flutter pub get
flutter run --dart-define=GO_BEYOND_API_URL=http://localhost:5000
```

## API configuration

Base URL koristi `GO_BEYOND_API_URL`.

Default:
- `http://localhost:5000`

Definisano u:
- `lib/core/constants/app_constants.dart`

## Trenutno implementirano

- Login i client register prema stvarnim auth endpointima
- Persisted session i bootstrap preko auth scope-a
- Mentor browse, search, filter i detail pregled
- Recommendation flow prema `/api/mentors/recommended`
- Questionnaire + subscription create flow
- Payment confirmation/status pregled
- Current subscription i current training plan pregled
- Progress history + upload photo URL flow
- Client profile edit i logout

## Verification

- `flutter analyze lib test` prolazi
- `flutter test test/widget_test.dart` prolazi

## Napomena

- Mobile smoke test vise nije vezan za stari placeholder onboarding ekran.
- API URL se ne hardkodira nego dolazi iz `--dart-define`.

# GoBeyond

GoBeyond je RSII projekat sa .NET backendom i dva Flutter klijenta:
- desktop panel za Admin/Mentor
- mobilna app za Client

## Repository layout

```text
GoBeyond/
|- GoBeyond.API/
|  |- GoBeyond.sln
|  |- GoBeyond.API/                # ASP.NET Core Web API
|  |- GoBeyond.Core/
|  |- GoBeyond.Infrastructure/
|  |- GoBeyond.Contracts/
|  `- GoBeyond.EmailConsumer/
|- UI/
|  |- gobeyond_desktop/
|  `- gobeyond_mobile/
|- docker-compose.yml
|- GoBeyond-Plan.md
|- pravila.md
`- init.md
```

## Run backend (Visual Studio)

1. Otvori `GoBeyond.API/GoBeyond.sln`.
2. Postavi startup project: `GoBeyond.API`.
3. Pokreni `F5` ili `Ctrl+F5`.

Konfiguracija je na:
- `http://localhost:5000`
- Swagger auto-open (`/swagger`)

Napomena:
- Ako zaustavis debugging u Visual Studio, API proces se gasi.
- Ako samo zatvoris browser tab, API nastavlja da radi dok ga ne ugasis iz VS.

## Run backend (CLI)

```powershell
cd GoBeyond.API/GoBeyond.API
dotnet run --launch-profile http
```

Swagger: `http://localhost:5000/swagger`

## Seed test users (trenutno stanje)

- Admin: `admin@gobeyond.local` / `Admin123!`
- Mentor: `mentor@gobeyond.local` / `Mentor123!`
- Client: `client@gobeyond.local` / `Client123!`

## Run Flutter desktop

```powershell
cd UI/gobeyond_desktop
flutter pub get
flutter run -d windows --dart-define=GO_BEYOND_API_URL=http://localhost:5000
```

Desktop API URL koristi `GO_BEYOND_API_URL` i po defaultu pada na `http://localhost:5000`.

## Run Flutter mobile

```powershell
cd UI/gobeyond_mobile
flutter pub get
flutter run --dart-define=GO_BEYOND_API_URL=http://localhost:5000
```

## Current implementation status (short)

- Implementirano:
  - Auth (login/register/refresh/change-password)
  - Role policies (AdminOnly, MentorOnly, ClientOnly, MentorOrAdmin)
  - Desktop shell sa role-based navigacijom
  - User profile CRUD endpointi (`/api/user-profile/me`)
  - Desktop User Profile ekran + Save Changes flow
  - Admin flow:
    - mentor requests approve/reject
    - mentors/clients/subscriptions list + search + role actions
    - overview reporting + mentor drilldown + CSV export
  - Mentor flow:
    - collaboration requests
    - subscribers + client detail
    - create draft / publish training plan
    - published plans pregled
  - Mobile client flow:
    - login/register + persisted session
    - mentor browse/detail/recommendations
    - questionnaire + subscription + payment confirmation
    - current plan, progress history i profile edit
  - DTO/mapper cleanup:
    - `GoBeyond.Core.DTOs.Mvp` je flattenovan u `GoBeyond.Core.DTOs`
    - `MvpDtos.cs` je zamijenjen sa `AppDtos.cs`
    - `MvpMapper.cs` je zamijenjen sa `DtoMapper.cs`
  - Domen endpointi za prijavljeni scope vise ne vracaju `501 Not Implemented`
  - Flutter appovi koriste `--dart-define` za API base URL

- Jos nije zavrseno:
  - RabbitMQ consumer flow jos nije stvarno povezan na queue

## Verification

- Desktop:
  - `flutter analyze lib test` prolazi
  - `flutter test test/widget_test.dart` prolazi
- Mobile:
  - `flutter analyze lib test` prolazi
  - `flutter test test/widget_test.dart` prolazi
- Backend:
  - kontroleri i DTO/mapper reference su uskladjeni nakon zadnjih rename promjena
  - lokalni `dotnet build` i dalje moze pasti zbog ostecenog .NET 9 SDK/MSBuild okruzenja na masini, ne zbog prijavljenog compile error-a u repou

## Troubleshooting

### Port 5000 already in use

```powershell
netstat -ano | findstr :5000
taskkill /PID <PID> /F
```

### Flutter CMake cache mismatch nakon premjestanja u `UI/`

```powershell
cd UI/gobeyond_desktop
flutter clean
Remove-Item -Recurse -Force build\windows\x64 -ErrorAction SilentlyContinue
flutter pub get
flutter run -d windows --dart-define=GO_BEYOND_API_URL=http://localhost:5000
```

### .NET build pada bez jasne greske

Ako `dotnet build` stane na `Determining projects to restore...` bez compiler error-a, provjeri lokalni .NET SDK/MSBuild install.

Na ovoj masini je zabiljezen problem sa nedostajucim workload resolver folderima pod:
- `C:\Program Files\dotnet\sdk\9.0.200\Sdks\Microsoft.NET.SDK.WorkloadAutoImportPropsLocator`
- `C:\Program Files\dotnet\sdk\9.0.200\Sdks\Microsoft.NET.SDK.WorkloadManifestTargetsLocator`

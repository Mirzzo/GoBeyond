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
flutter run -d windows
```

Desktop API URL je trenutno postavljen na `http://localhost:5000`.

## Run Flutter mobile

```powershell
cd UI/gobeyond_mobile
flutter pub get
flutter run
```

## Current implementation status (short)

- Implementirano:
  - Auth (login/register/refresh/change-password)
  - Role policies (AdminOnly, MentorOnly, ClientOnly, MentorOrAdmin)
  - Desktop shell sa role-based navigacijom
  - User profile CRUD endpointi (`/api/user-profile/me`)
  - Desktop User Profile ekran + Save Changes flow

- Jos nije zavrseno:
  - Vise domen endpointa je i dalje `501 Not Implemented`
  - Dio desktop/mobile ekrana je placeholder
  - RabbitMQ consumer flow jos nije stvarno povezan na queue

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
flutter run -d windows
```

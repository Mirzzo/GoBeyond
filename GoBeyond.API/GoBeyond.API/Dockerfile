# Build context = korijen repozitorija (docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY appsettings.Shared.json ./
COPY GoBeyond.API/GoBeyond.API/GoBeyond.API.csproj GoBeyond.API/GoBeyond.API/
COPY GoBeyond.API/GoBeyond.Core/GoBeyond.Core.csproj GoBeyond.API/GoBeyond.Core/
COPY GoBeyond.API/GoBeyond.Infrastructure/GoBeyond.Infrastructure.csproj GoBeyond.API/GoBeyond.Infrastructure/
COPY GoBeyond.API/GoBeyond.Contracts/GoBeyond.Contracts.csproj GoBeyond.API/GoBeyond.Contracts/
RUN dotnet restore GoBeyond.API/GoBeyond.API/GoBeyond.API.csproj

COPY GoBeyond.API/GoBeyond.API/ GoBeyond.API/GoBeyond.API/
COPY GoBeyond.API/GoBeyond.Core/ GoBeyond.API/GoBeyond.Core/
COPY GoBeyond.API/GoBeyond.Infrastructure/ GoBeyond.API/GoBeyond.Infrastructure/
COPY GoBeyond.API/GoBeyond.Contracts/ GoBeyond.API/GoBeyond.Contracts/
RUN dotnet publish GoBeyond.API/GoBeyond.API/GoBeyond.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
# curl se koristi samo za Docker healthcheck (GET /health).
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
# wwwroot/seed (demo slike i certifikati) je dio image-a; wwwroot/uploads je Docker volume.
RUN mkdir -p /app/wwwroot/uploads
# aspnet image sluša na portu 8080 (ASPNETCORE_HTTP_PORTS).
EXPOSE 8080
ENTRYPOINT ["dotnet", "GoBeyond.API.dll"]

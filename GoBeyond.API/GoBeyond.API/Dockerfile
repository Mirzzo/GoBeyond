FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY GoBeyond.sln ./
COPY GoBeyond.API/GoBeyond.API/GoBeyond.API.csproj GoBeyond.API/GoBeyond.API/
COPY GoBeyond.API/GoBeyond.Core/GoBeyond.Core.csproj GoBeyond.API/GoBeyond.Core/
COPY GoBeyond.API/GoBeyond.Infrastructure/GoBeyond.Infrastructure.csproj GoBeyond.API/GoBeyond.Infrastructure/
COPY GoBeyond.API/GoBeyond.Contracts/GoBeyond.Contracts.csproj GoBeyond.API/GoBeyond.Contracts/
COPY GoBeyond.API/GoBeyond.EmailConsumer/GoBeyond.EmailConsumer.csproj GoBeyond.API/GoBeyond.EmailConsumer/

RUN dotnet restore GoBeyond.API/GoBeyond.API/GoBeyond.API.csproj

COPY . .
RUN dotnet publish GoBeyond.API/GoBeyond.API/GoBeyond.API.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "GoBeyond.API.dll"]

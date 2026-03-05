FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY GoBeyond.sln ./
COPY GoBeyond.API/GoBeyond.EmailConsumer/GoBeyond.EmailConsumer.csproj GoBeyond.API/GoBeyond.EmailConsumer/
COPY GoBeyond.API/GoBeyond.Contracts/GoBeyond.Contracts.csproj GoBeyond.API/GoBeyond.Contracts/

RUN dotnet restore GoBeyond.API/GoBeyond.EmailConsumer/GoBeyond.EmailConsumer.csproj

COPY . .
RUN dotnet publish GoBeyond.API/GoBeyond.EmailConsumer/GoBeyond.EmailConsumer.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:9.0
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "GoBeyond.EmailConsumer.dll"]

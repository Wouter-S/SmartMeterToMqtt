FROM mcr.microsoft.com/dotnet/core/aspnet:6.0-jammy-arm64v8 AS base
WORKDIR /app

VOLUME /data
FROM mcr.microsoft.com/dotnet/core/sdk:3.1-bionic AS build

WORKDIR /app
COPY ["*.csproj", "./"]
RUN dotnet restore
COPY . ./
WORKDIR "/app"

FROM build AS publish
RUN dotnet publish -c Release -o out

FROM base AS final
WORKDIR /app
COPY --from=publish /app/out .
ENTRYPOINT ["dotnet", "SmartMeterToMqtt.dll"]


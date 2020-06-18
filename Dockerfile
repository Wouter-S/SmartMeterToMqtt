FROM mcr.microsoft.com/dotnet/core/aspnet:3.0-bionic-arm32v7 AS base
WORKDIR /app

VOLUME /data
FROM mcr.microsoft.com/dotnet/core/sdk:3.0-bionic AS build

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


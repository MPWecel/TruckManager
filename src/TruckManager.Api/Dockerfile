# syntax=docker/dockerfile:1.7
# Build context: the source-tree root (TruckManager/). Configure in docker-compose.yml.

# ---- build stage ----
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Centralized config first — only changes when versions or build properties change.
COPY global.json Directory.Build.props Directory.Packages.props ./

# csproj files only — keeps the restore layer cached when only source changes.
COPY src/TruckManager.Common/TruckManager.Common.csproj ./src/TruckManager.Common/
COPY src/TruckManager.Domain/TruckManager.Domain.csproj ./src/TruckManager.Domain/
COPY src/TruckManager.Application/TruckManager.Application.csproj ./src/TruckManager.Application/
COPY src/TruckManager.Infrastructure/TruckManager.Infrastructure.csproj ./src/TruckManager.Infrastructure/
COPY src/TruckManager.Api/TruckManager.Api.csproj ./src/TruckManager.Api/

RUN dotnet restore src/TruckManager.Api/TruckManager.Api.csproj

# Now copy the rest of the source.
COPY src/ ./src/

WORKDIR /src/src/TruckManager.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# ---- runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

# The aspnet:9.0 image already sets USER $APP_UID and binds 8080.
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "TruckManager.Api.dll"]

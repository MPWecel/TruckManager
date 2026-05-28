# TruckManager

##  Overview:
A demo module of an ERP platform - first vertical slice contains a REST API for Truck management. 
The TruckManager module is an architectural template for potential future modules.

>   **Implementation Status:** 
        Phases 1–5 complete (Solution Skeleton, Shared Kernel, Domain, Persistence, CQRS). 
        Phase 6 (API Layer) starting — REST controllers, ProblemDetails, OpenAPI/Swagger land here. 
        See phases.md for the full roadmap.

---

##  Tech stack:
    >   .NET 9 (configured in `global.json`)
    >   ASP.NET Core WebAPI + EF Core
    >   PostgreSQL
    >   Serilog 
    >   FluentValidation + xUnit + AwesomeAssertions + Testcontainers
    >   Docker + docker-compose for local deployment of app suite
    >   Architecture summary: 
            Modular Monolith + Clean Architecture + Vertical Slice + CQRS (manual implementation, no MediatR) + Transactional Event Sourcing

Full architecture and Architectural Decision Log in design document.

---

##  Layout:
    ```
    TruckManager/                               //RepositoryRoot
        ├── global.json                         //global project settings - framework version and the like
        ├── Directory.Build.props               //-/-
        ├── Directory.Packages.props            //-/-
        ├── src/                                //All projects
        │   ├── TruckManager.Common/
        │   ├── TruckManager.Domain/
        │   ├── TruckManager.Application/
        │   ├── TruckManager.Infrastructure/
        │   ├── TruckManager.Api/
        │   ├── TruckManager.BlazorClient/      //deferred to V2 — directory not yet scaffolded
        │   └── TruckManager.ConsoleClient/     //deferred to V2 — directory not yet scaffolded
        ├── tests/
        │   ├── TruckManager.UnitTests/
        │   ├── TruckManager.IntegrationTests/
        │   └── TruckManager.ArchitectureTests/
        ├── docker/                             //Docker-Compose entry point
        │   └── docker-compose.yml
        └── solutions/                          //all solutions - needed for development, not for deploymenr
            └── TruckManager.WebApi.sln
    ```

---

##  Prerequisites:
    >   .NET 9 SDK
    >   Docker Desktop (for the Postgres container and potential other modules such as Client apps, messeging queues and the like)
    >   An IDE that understands central package management — Rider, Visual Studio 2022 17.8+, or VS Code with the C# Dev Kit

Verify the SDK is picked up correctly inside this directory:
    ```bash
    dotnet --version
    # expected: 9.0.314 (or a later 9.0.x patch)
    ```

---

## Quick start:

**Recommended path (docker-compose, API in container — host port 5000 → container 8080):**
    ```bash
    #   1.  Start Postgres + the API.
    #       The override file exposes ports and sets ASPNETCORE_ENVIRONMENT=Development.
    #       MigrationRunner auto-applies pending migrations on API startup (ADR-0018).
    docker compose \
        -f docker/docker-compose.yml \
        -f docker/docker-compose.override.yml \
        up -d

    #   2.  Liveness check (HealthController, always 200 if the process is up)
    curl http://localhost:5000/health
    # expected: {"status":"healthy"}

    #   3.  Readiness check (StatusBijectionHealthCheck — 200 only after dictionary bijection verified)
    curl http://localhost:5000/health/ready
    ```

**Alternative path (API on host, Postgres in container — host port 5221):**
    ```bash
    #   1.  Postgres only (host port 5432 exposed by docker-compose.override.yml).
    docker compose -f docker/docker-compose.yml -f docker/docker-compose.override.yml up -d postgres

    #   2.  Run the API on the host.
    dotnet run --project src/TruckManager.Api
    # Kestrel HTTP profile listens on http://localhost:5221 (see launchSettings.json).

    #   3.  Health check
    curl http://localhost:5221/health
    ```

Swagger UI will be at `/swagger` once Phase 6 lands (port 5000 via docker, 5221 via `dotnet run`).

---

## Tests:
    ```bash
    dotnet test solutions/TruckManager.WebApi.sln
    ```
    As of Phase 5 close: **197 unit + 45 integration tests passing.** ArchitectureTests project is scaffolded but holds no tests yet (lands in Phase 8).

    Three test projects:
    |   Project                             |   Covers                                                                              |
    |---------------------------------------|---------------------------------------------------------------------------------------|
    |   `TruckManager.UnitTests`            |   Domain invariants, value objects, validators, handler logic, pipeline behaviors     |
    |   `TruckManager.IntegrationTests`     |   Persistence (dual-write, soft-delete, concurrency) + CQRS pipeline smoke tests via Testcontainers Postgres |
    |   `TruckManager.ArchitectureTests`    |   Layer boundaries, banned APIs (e.g. `DateTime.UtcNow` outside `IDateTimeProvider`) — populated in Phase 8 |

---

## Deployment:
    **Local only.** 
    This project is not deployed to any hosted environment and has no CI pipeline — see [ADR-0018].

---

## Project documentation:
|   File                                                                                            |   Purpose                                                     |
|---------------------------------------------------------------------------------------------------|---------------------------------------------------------------|
|    `/TruckManagerDocumentation/docs/TruckManager_ErpApp_ArchitectureDesignDocument_V1.md`         |   Full architectural design specification (source of truth)   |   //TODO: Split into separate, smaller documents

---

## License

See [`LICENSE`](LICENSE).

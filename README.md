# TruckManager

##  Overview:
A demo module of an ERP platform - first vertical slice contains a REST API for Truck management. 
The TruckManager module is an architectural template for potential future modules.

>   **Implementation Status:** 
        Phase 1 (Solution Skeleton) in progress. 
        The source tree is being scaffolded. 
        Commands marked **(planned)** below will work once Phase 1 is complete — see roadmap in design document.

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
        │   ├── TruckManager.BlazorClient/      //current status: placeholder
        │   └── TruckManager.ConsoleClient/     //current status: placeholder
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

## Quick start *(planned — works once Phase 1 is complete)*:
    ```bash
    #   1.  Start Postgres (and later, the API) via docker-compose.
    #       The override file adds local-dev settings (exposed ports, Development env).
    docker compose \
        -f docker/docker-compose.yml \
        -f docker/docker-compose.override.yml \
        up -d
    
    #   2.  Apply EF Core migrations (added in Phase 4)
    dotnet ef database update \
        --project src/TruckManager.Infrastructure \
        --startup-project src/TruckManager.Api
    
    #   3.  Run the API
    dotnet run --project src/TruckManager.Api
    
    #   4.  Health check
    curl http://localhost:5000/health
    # expected: {"status":"healthy"}
    ```
Swagger UI will be at `http://localhost:5000/swagger` once Phase 6 lands.

---

## Tests *(planned — works once test projects are scaffolded)*:
    ```bash
    dotnet test solutions/TruckManager.WebApi.sln
    ```

    Three test projects:
    |   Project                             |   Covers                                                                              |
    |---------------------------------------|---------------------------------------------------------------------------------------|
    |   `TruckManager.UnitTests`            |   Domain invariants, value objects, validators, handler logic                         |
    |   `TruckManager.IntegrationTests`     |   API endpoints + persistence (Testcontainers Postgres)                               |
    |   `TruckManager.ArchitectureTests`    |   Layer boundaries, banned APIs (e.g. `DateTime.UtcNow` outside `IDateTimeProvider`)  |

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

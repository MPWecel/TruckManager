using TruckManager.Application;
using TruckManager.Infrastructure;
using TruckManager.Infrastructure.Workflows;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Phase 5 / Section A.  Application composition: CQRS dispatchers, handler assembly scan, FluentValidators.
// Pipeline behaviors land in Section B alongside IUnitOfWork.
builder.Services.AddTruckManagerApplication();

// Phase 4 / Section F.  Single composition-root call for the persistence + workflow + audit pipeline
// (DbContext + interceptors, transition policy, bijection check, audit + event hosted services, IDateTimeProvider, stub ICurrentUserService).
builder.Services.AddTruckManagerInfrastructure(builder.Configuration);

// Expose the bijection check at /health/ready. The check itself is the singleton registered by AddTruckManagerInfrastructure
// (same instance the IHostedService startup path uses), so this endpoint reads its _isReady flag without re-querying the DB.
builder.Services.AddHealthChecks()
                .AddCheck<StatusBijectionHealthCheck>(
                                                         name: "workflow_bijection",
                                                         tags: ["ready"]
                                                     );

WebApplication application = builder.Build();

// HTTPS redirection intentionally omitted: Phase 1 local stack runs HTTP-only inside Docker (port 8080).
// Add `application.UseHttpsRedirection()` back when a real TLS termination point exists.

application.UseAuthorization();

application.MapControllers();

// Phase 6 / Section A.1.  Health endpoints exposed via Minimal APIs.
// Liveness ("/health") - is the process up? Trivial, never touches the DB. Orchestrators
// (Docker, Kubernetes, load balancers) use this to decide whether to RESTART the container.
// Must succeed on every running instance regardless of dependency state.
application.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
// Readiness ("/health/ready") - is this instance ready to serve traffic? Backed by the
// StatusBijectionHealthCheck registered above; returns 503 until startup verification passes.
// Orchestrators use this to decide whether to ROUTE TRAFFIC to the instance (but never to restart).
// Intentionally NOT placed under /api/v{version}/ - health endpoints are infrastructure contracts
// for orchestrators, not API consumers, and must stay stable across API version bumps.
application.MapHealthChecks("/health/ready");

application.Run();

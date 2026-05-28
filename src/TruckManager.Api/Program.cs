using Asp.Versioning;

using TruckManager.Api.Middleware;
using TruckManager.Application;
using TruckManager.Infrastructure;
using TruckManager.Infrastructure.Workflows;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Phase 6 / Section D   URL-segment API versioning (`/api/v{version:apiVersion}/...`) [decision #1].
// DefaultApiVersion + AssumeDefaultVersionWhenUnspecified mean a request without an explicit version resolves to v1 (graceful — old clients that haven't adopted versioning still work).
// ReportApiVersions adds the `api-supported-versions` header to every response so clients can discover what's available.
// AddApiExplorer wires versioning into IApiVersionDescriptionProvider — Swagger (Section F) reads it to produce one OpenAPI document per version.
// SubstituteApiVersionInUrl rewrites the literal `v{version:apiVersion}` token in the route template into the concrete version string in the generated OpenAPI document (so the Swagger UI shows `/api/v1/trucks`, not `/api/v{version}/trucks`).
builder.Services.AddApiVersioning(
                                     o =>
                                     {
                                         o.DefaultApiVersion = new ApiVersion(1);
                                         o.AssumeDefaultVersionWhenUnspecified = true;
                                         o.ReportApiVersions = true;
                                     }
                                 )
                .AddMvc()
                .AddApiExplorer(
                                   o =>
                                   {
                                       o.GroupNameFormat = "'v'VVV";
                                       o.SubstituteApiVersionInUrl = true;
                                   }
                               );

// Phase 5 / Section A  Application composition: CQRS dispatchers, handler assembly scan, FluentValidators.
// Pipeline behaviors land in Section B alongside IUnitOfWork.
builder.Services.AddTruckManagerApplication();

// Phase 4 / Section F  Single composition-root call for the persistence + workflow + audit pipeline
// (DbContext + interceptors, transition policy, bijection check, audit + event hosted services, IDateTimeProvider, stub ICurrentUserService).
builder.Services.AddTruckManagerInfrastructure(builder.Configuration);

// Phase 6 / Section B  RFC 7807 ProblemDetails infrastructure + custom exception handler.
// AddProblemDetails registers IProblemDetailsService — used by UseStatusCodePages (turns naked 4xx/5xx into ProblemDetails bodies) and by GlobalExceptionHandler (writes the 500 body).
// Business failures (Result.Failure) NEVER reach this layer — ApiResultExtensions (Section C) maps them in-band before the framework sees an exception.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Expose the bijection check at /health/ready. The check itself is the singleton registered by AddTruckManagerInfrastructure (same instance the IHostedService startup path uses), so this endpoint reads its _isReady flag without re-querying the DB.
builder.Services.AddHealthChecks()
                .AddCheck<StatusBijectionHealthCheck>(
                                                         name: "workflow_bijection",
                                                         tags: ["ready"]
                                                     );

WebApplication application = builder.Build();

// HTTPS redirection intentionally omitted: Phase 1 local stack runs HTTP-only inside Docker (port 8080).
// Add `application.UseHttpsRedirection()` back when a real TLS termination point exists.

// Phase 6 / Section B.  Exception + status-code middleware first in the pipeline so they catch anything from later layers (auth, controllers, minimal APIs):
//   > UseExceptionHandler() iterates registered IExceptionHandlers in order; GlobalExceptionHandler handles every unhandled exception and writes a 500 ProblemDetails.
//   > UseStatusCodePages() turns any naked status-code response (e.g., 404 from no route match) into a ProblemDetails body of the matching shape — keeps the error envelope consistent across in-band Result failures, unhandled exceptions, and framework-generated status codes.
application.UseExceptionHandler();
application.UseStatusCodePages();

application.UseAuthorization();

application.MapControllers();

// Phase 6 / Section A.1    Health endpoints exposed via Minimal APIs.
// Liveness ("/health") - is the process up? Trivial, never touches the DB. Orchestrators (Docker, Kubernetes, load balancers) use this to decide whether to RESTART the container.
// Must succeed on every running instance regardless of dependency state.
application.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
// Readiness ("/health/ready") - is this instance ready to serve traffic? Backed by the StatusBijectionHealthCheck registered above; returns 503 until startup verification passes.
// Orchestrators use this to decide whether to ROUTE TRAFFIC to the instance (but never to restart).
// Intentionally NOT placed under /api/v{version}/ - health endpoints are infrastructure contracts for orchestrators, not API consumers, and must stay stable across API version bumps.
application.MapHealthChecks("/health/ready");

application.Run();

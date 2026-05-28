using Asp.Versioning;
using Asp.Versioning.ApiExplorer;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.PostgreSQL.ColumnWriters;
using Swashbuckle.AspNetCore.SwaggerGen;

using TruckManager.Api.Middleware;
using TruckManager.Api.Swagger;
using TruckManager.Application;
using TruckManager.Infrastructure;
using TruckManager.Infrastructure.Logging;
using TruckManager.Infrastructure.Workflows;

// Phase 7 / Section A   Bootstrap logger.
// Captures startup-failure logs (host configuration, DI registration explosions, etc.) before builder.Host.UseSerilog has read the appsettings `Serilog` section.
// The bootstrap logger writes to Console only — once UseSerilog wires up, Log.Logger is replaced with the host-configured one and the rest of the program (Log.X static calls, ILogger<T> injections) routes through that.
Log.Logger = new LoggerConfiguration().MinimumLevel.Information()
                                      .WriteTo.Console()
                                      .CreateBootstrapLogger();

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    // Add services to the container.

    // Phase 7 / Section A   Replace the default Microsoft.Extensions.Logging host with Serilog [ADR-0012, ADR-0021].
    // ReadFrom.Configuration loads the `Serilog` section from appsettings.json (Console + File sinks, min levels, enrichers);
    // ReadFrom.Services lets sinks/enrichers resolve framework services (e.g., IHttpContextAccessor for the Phase 7 Section B correlation enricher).
    // Enrich.FromLogContext picks up the per-request CorrelationId / TenantId / UserId that CorrelationMiddleware (Section B) will push.
    //
    // The PostgreSQL sink is wired here in code (not in appsettings) so the connection string stays single-sourced in IConfiguration.ConnectionStrings:Default — same value the API uses for its main DB.
    // Per database.md §12 the Logs table is operational, not part of the domain schema; needAutoCreateTable: true lets the sink provision it on first run.
    // The sink is skipped when no connection string is configured (e.g., a hypothetical no-DB run) — Console + File stay active so the failure mode is observable.
    builder.Host.UseSerilog(
                               (ctx, sp, cfg) =>
                               {
                                   cfg.ReadFrom.Configuration(ctx.Configuration)
                                      .ReadFrom.Services(sp)
                                      .Enrich.FromLogContext()
                                      // Phase 7 / Section D   Masks property values whose names match the sensitive-keyword list (password / token / secret / connection-string / cookie / …).
                                      // Wraps Serilog's default destructurer: returns false when nothing sensitive is present, so the common case has no overhead.
                                      .Destructure.With<SensitivePropertyDestructuringPolicy>();

                                   string? dbConnectionString = ctx.Configuration.GetConnectionString("Default");
                                   if (!String.IsNullOrWhiteSpace(dbConnectionString))
                                   {
                                       // Serilog.Sinks.Postgresql.Alternative 4.x ships two PostgreSQL extension overloads:
                                       // one taking IDictionary<string, ColumnWriterBase> (legacy) and one taking the newer DefaultColumnWriter / SinglePropertyColumnWriter pair.
                                       // Both default columnOptions to null, so the compiler reports the call as ambiguous.
                                       // Casting null to the legacy ColumnWriterBase dictionary type forces the older overload — we want the sink's built-in default column set anyway (Message / Level / Timestamp / Exception / Properties).
                                       cfg.WriteTo.PostgreSQL(
                                                                 connectionString: dbConnectionString,
                                                                 tableName: "Logs",
                                                                 columnOptions: (IDictionary<string, ColumnWriterBase>?)(null),
                                                                 needAutoCreateTable: true,
                                                                 restrictedToMinimumLevel: LogEventLevel.Information
                                                             );
                                   }
                               }
                           );

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

    // Phase 6 / Section F   Swagger / OpenAPI document generation [decision #2].
    // AddSwaggerGen registers the document generator; ConfigureSwaggerOptions adds one SwaggerDoc per API version via IApiVersionDescriptionProvider (registered above by AddApiExplorer).
    // The IConfigureOptions<> indirection bridges service-registration time (SwaggerGen config) and service-resolution time (when IApiVersionDescriptionProvider is available).
    builder.Services.AddSwaggerGen();
    builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();

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

    // Phase 6 / Section F   Swagger UI exposed in Development only.
    // Per-version endpoint loop: every API version discovered by IApiVersionDescriptionProvider gets its own Swagger UI dropdown entry pointing at /swagger/{groupName}/swagger.json.
    // When v2 lands alongside v1, the dropdown carries both with no code change here.
    if (application.Environment.IsDevelopment())
    {
        application.UseSwagger();
        application.UseSwaggerUI(
                                    uiOptions =>
                                    {
                                        IApiVersionDescriptionProvider versionProvider =
                                            application.Services.GetRequiredService<IApiVersionDescriptionProvider>();

                                        foreach (ApiVersionDescription description in versionProvider.ApiVersionDescriptions)
                                        {
                                            uiOptions.SwaggerEndpoint(
                                                                         url: $"/swagger/{description.GroupName}/swagger.json",
                                                                         name: $"TruckManager API {description.GroupName.ToUpperInvariant()}"
                                                                     );
                                        }
                                    }
                                );
    }

    // Phase 6 / Section B.  Exception + status-code middleware first in the pipeline so they catch anything from later layers (auth, controllers, minimal APIs):
    //   > UseExceptionHandler() iterates registered IExceptionHandlers in order; GlobalExceptionHandler handles every unhandled exception and writes a 500 ProblemDetails.
    //   > UseStatusCodePages() turns any naked status-code response (e.g., 404 from no route match) into a ProblemDetails body of the matching shape — keeps the error envelope consistent across in-band Result failures, unhandled exceptions, and framework-generated status codes.
    application.UseExceptionHandler();
    application.UseStatusCodePages();

    // Phase 7 / Section B   Correlation middleware.
    // Runs AFTER UseExceptionHandler so an exception thrown by a downstream middleware (auth, MVC, etc.) unwinds back through here on its way to UseExceptionHandler -
    // — by which point HttpContext.Items["TruckManager.CorrelationId"] is set and GlobalExceptionHandler (Section F) can read it via ICorrelationContext.
    // Runs BEFORE MapControllers / MapHealthChecks so the LogContext push wraps every actionable request.
    application.UseMiddleware<CorrelationMiddleware>();

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
}
catch (Exception ex)
{
    // Phase 7 / Section A   Last-chance log path. Anything fatal during host setup (DI explosions, configuration parse failures, port binding errors) lands here.
    // Re-throw so the process exit code reflects the failure for orchestrators / docker-compose.
    Log.Fatal(ex, "Host terminated unexpectedly");
    throw;
}
finally
{
    // Phase 7 / Section A   Flush + dispose Serilog on shutdown so any buffered log lines (especially the File and PostgreSQL sinks, which batch writes) reach their destinations before the process exits.
    Log.CloseAndFlush();
}

#region ProgramExposureForTests
// Phase 8 / Section F.   Make the implicit top-level Program class addressable as `typeof(Program)` for WebApplicationFactory<Program>.
// Without this declaration the auto-generated Program is internal, and the architecture tests + Web API integration tests in TruckManager.IntegrationTests can't reference it.
// `partial` matches the compiler-emitted class signature; the empty body just elevates visibility.
public partial class Program;
#endregion

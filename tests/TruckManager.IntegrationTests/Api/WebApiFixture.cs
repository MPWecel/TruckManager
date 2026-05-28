using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Xunit;

namespace TruckManager.IntegrationTests.Api;

// Phase 8 / Section F fixture.
//
// Spins up a fresh Postgres 16 container per test class, then boots the full ASP.NET Core host (the real `Program` from TruckManager.Api) on top of it via WebApplicationFactory<Program>.
//
// Why a separate fixture from PostgresFixture (Phase 4)?
//   >  PostgresFixture builds the Application + Infrastructure DI directly via ServiceCollection — no Web host.
//      Tests dispatch commands via ICommandDispatcher; no HTTP, no controllers, no middleware. Perfect for handler / persistence / pipeline tests.
//   >  WebApiFixture composes the same Testcontainers pattern with the actual Web host so the test surface is HTTP — controllers, ProblemDetails middleware, CorrelationMiddleware, Swagger, health endpoints.
// The DI graphs intentionally don't merge: this is two different testing surfaces.
//
// Per Phase 8 decision #4 the production HttpContextCorrelationContext stays in place — CorrelationMiddleware runs in-host, so HttpContext.Items is populated normally.
// Tests that need a deterministic correlation id send an X-Correlation-Id header.
//
// Per Phase 8 decision #2 the migration runner + StatusBijectionHealthCheck are the production registrations — host startup runs them in registration order, so by the time CreateClient() returns the schema is up and the readiness gate has flipped.
//
// Environment is forced to Development so the Swagger UI middleware (gated on application.Environment.IsDevelopment() in Program.cs) is active for SwaggerTests.
public sealed class WebApiFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public string ConnectionString { get; private set; } = String.Empty;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        // Touching Server / CreateClient triggers host startup — which runs the hosted services:
        //   1)   MigrationRunner   — applies InitialCreate, seeds the two dictionary tables.
        //   2)   StatusBijectionHealthCheck.StartAsync — validates the bijection + primes the transition policy.
        // After this line the API is ready to serve traffic. /health/ready will return 200.
        _ = Server;
    }

    // xUnit.v3's IAsyncLifetime only declares InitializeAsync; it extends IAsyncDisposable for shutdown.
    // WebApplicationFactory<T> already overrides IAsyncDisposable.DisposeAsync — we extend that override so both the host AND the per-class Postgres container shut down deterministically.
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _container.DisposeAsync();
    }

    // ConfigureWebHost runs BEFORE the host's IServiceProvider is built. Inject the container's connection string into IConfiguration here so AddTruckManagerInfrastructure resolves "ConnectionStrings:Default" to the per-class Postgres instance.
    // The Development environment activates Swagger UI; the per-class container guarantees test isolation.
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration(
                                             cfg =>
                                             {
                                                 cfg.AddInMemoryCollection(
                                                                              new Dictionary<string, string?>
                                                                              {
                                                                                  ["ConnectionStrings:Default"] = ConnectionString,
                                                                              }
                                                                          );
                                             }
                                         );
    }
}

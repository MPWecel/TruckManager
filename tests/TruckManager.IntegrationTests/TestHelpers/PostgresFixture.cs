using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Testcontainers.PostgreSql;

using TruckManager.Infrastructure;
using TruckManager.Infrastructure.Persistence;
using TruckManager.Infrastructure.Workflows;

using Xunit;

namespace TruckManager.IntegrationTests.TestHelpers;

// Phase 4 / Section G fixture.
//
// Spins up a Postgres 16 container, applies the InitialCreate migration (which also
// seeds the two dictionary tables), then runs the production StatusBijectionHealthCheck
// to initialise the singleton TruckStatusTransitionPolicy — i.e., the fixture goes
// through the same wiring the real API uses at boot. Tests resolve scoped services
// (ApplicationDbContext, IDateTimeProvider, …) from `Services` per test method.
//
// Used as IClassFixture for test classes that share the seeded DB and only mutate
// non-dictionary tables (TruckPersistenceTests). Tests that mutate the dictionary
// itself (StatusBijectionHealthCheckTests) get a fresh container per method via
// `BuildServices(connectionString)` instead.
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public string           ConnectionString { get; private set; } = string.Empty;
    public IServiceProvider Services         { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
        Services         = BuildServices(ConnectionString);

        using (IServiceScope scope = Services.CreateScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.MigrateAsync();
        }

        // Run the production startup path to load the singleton transition policy.
        StatusBijectionHealthCheck bijection = Services.GetRequiredService<StatusBijectionHealthCheck>();
        await bijection.StartAsync(CancellationToken.None);
    }

    public async ValueTask DisposeAsync()
    {
        if (Services is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else if (Services is IDisposable disposable)
            disposable.Dispose();

        await _container.DisposeAsync();
    }

    // Builds a fresh production-shape ServiceProvider against the given connection
    // string. Used by both the fixture (for class-shared DI) and the bijection-failure
    // tests (which need a fresh, uninitialised policy per test method).
    public static IServiceProvider BuildServices(string connectionString)
    {
        ServiceCollection services = new();

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = connectionString,
                }
            )
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddTruckManagerInfrastructure(configuration);

        return services.BuildServiceProvider();
    }
}

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Testcontainers.PostgreSql;

using TruckManager.Domain.Enums;
using TruckManager.Domain.Policies;
using TruckManager.Infrastructure.Persistence;
using TruckManager.Infrastructure.Workflows;
using TruckManager.IntegrationTests.TestHelpers;

using Xunit;

namespace TruckManager.IntegrationTests.HealthChecks;

// Phase 4 / Section G. Each test method gets a **fresh Postgres container** because the
// failure scenarios mutate the dictionary tables — sharing one container between tests
// would either require fragile cleanup or order-dependent tests. The startup cost (≈3-5s
// per test × 3 tests) is the price of isolation.
public sealed class StatusBijectionHealthCheckTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    private string           _connectionString = string.Empty;
    private IServiceProvider _services         = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        _connectionString = _container.GetConnectionString();
        _services         = PostgresFixture.BuildServices(_connectionString);

        // Apply migrations (seeds the dictionary), but DO NOT pre-run the bijection
        // check — each test method invokes it as part of its scenario.
        using IServiceScope  scope   = _services.CreateScope();
        ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_services is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync();
        else if (_services is IDisposable disposable)
            disposable.Dispose();

        await _container.DisposeAsync();
    }

    // ----------------------------------------------------------------------------------
    // 1. Healthy seed → policy initialised, /health/ready returns Healthy
    // ----------------------------------------------------------------------------------

    [Fact]
    public async Task Healthy_seed_initialises_policy_and_reports_Healthy()
    {
        StatusBijectionHealthCheck check  = _services.GetRequiredService<StatusBijectionHealthCheck>();
        ITruckStatusTransitionPolicy policy = _services.GetRequiredService<ITruckStatusTransitionPolicy>();

        await check.StartAsync(CancellationToken.None);

        // Policy now reflects the seeded transitions
        policy.IsAllowed(ETruckStatus.Loading,      ETruckStatus.ToJob).Should().BeTrue();
        policy.IsAllowed(ETruckStatus.OutOfService, ETruckStatus.AtJob).Should().BeTrue();
        policy.IsAllowed(ETruckStatus.Loading,      ETruckStatus.AtJob).Should().BeFalse();

        HealthCheckResult result = await check.CheckHealthAsync(new HealthCheckContext());
        result.Status.Should().Be(HealthStatus.Healthy);
    }

    // ----------------------------------------------------------------------------------
    // 2. Orphan TruckStatuses row → bijection fails (dictionary has no matching enum)
    // ----------------------------------------------------------------------------------

    [Fact]
    public async Task Orphan_TruckStatuses_row_fails_bijection()
    {
        using (IServiceScope setup = _services.CreateScope())
        {
            ApplicationDbContext context = setup.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Insert a row with an Id that no ETruckStatus member maps to.
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"TruckStatuses\" (\"Id\", \"Code\", \"Name\", \"Sequence\", \"IsSystem\", \"IsActive\") " +
                "VALUES (99, 'ORPHAN', 'Orphan', 99, false, true)"
            );
        }

        StatusBijectionHealthCheck check = _services.GetRequiredService<StatusBijectionHealthCheck>();

        Func<Task> act = async () => await check.StartAsync(CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
                          .WithMessage("*bijection violated*");

        HealthCheckResult result = await check.CheckHealthAsync(new HealthCheckContext());
        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    // ----------------------------------------------------------------------------------
    // 3. Transition row referencing an unknown status → second-stage validation fails
    // ----------------------------------------------------------------------------------

    [Fact]
    public async Task Transition_row_pointing_at_unknown_status_fails_validation()
    {
        using (IServiceScope setup = _services.CreateScope())
        {
            ApplicationDbContext context = setup.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // The schema-level FK would normally reject this. Drop it temporarily so we
            // can prove the application-level second-stage check still catches it (the
            // FK is defense in depth; this check is the user-facing error message).
            await context.Database.ExecuteSqlRawAsync(
                "ALTER TABLE \"TruckStatusTransitions\" DROP CONSTRAINT \"FK_TruckStatusTransitions_TruckStatuses_FromStatusId\""
            );

            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"TruckStatusTransitions\" (\"Id\", \"FromStatusId\", \"ToStatusId\", \"IsAllowed\") " +
                "VALUES (99, 99, 1, true)"
            );
        }

        StatusBijectionHealthCheck check = _services.GetRequiredService<StatusBijectionHealthCheck>();

        Func<Task> act = async () => await check.StartAsync(CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>()
                          .WithMessage("*unknown TruckStatuses.Id*");
    }
}

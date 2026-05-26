using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using TruckManager.Common.Abstractions;
using TruckManager.Domain.Aggregates.Trucks;
using TruckManager.Domain.Enums;
using TruckManager.Domain.ValueObjects;
using TruckManager.IntegrationTests.TestHelpers;
using TruckManager.Infrastructure.Persistence;
using TruckManager.Infrastructure.Persistence.Entities;

using Xunit;

namespace TruckManager.IntegrationTests.Persistence;

// Phase 4 / Section G. End-to-end persistence behaviour against a real Postgres via
// Testcontainers + the production DI graph (interceptors, transition policy, etc.).
public sealed class TruckPersistenceTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public TruckPersistenceTests(PostgresFixture fixture) => _fixture = fixture;

    // ----------------------------------------------------------------------------------
    // 1. VO round-trip
    // ----------------------------------------------------------------------------------

    [Fact]
    public async Task Create_then_load_round_trips_all_VO_fields()
    {
        Truck truck;
        using (IServiceScope scope = _fixture.Services.CreateScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            IDateTimeProvider    clock   = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

            truck = NewTruck(
                code:        "TROUNDTRIP",
                clock:       clock,
                description: TruckDescription.Create("Tipper, 6x4, EuroVI").Value!,
                status:      ETruckStatus.Loading
            );

            context.Trucks.Add(truck);
            await context.SaveChangesAsync();
        }

        using IServiceScope     loadScope = _fixture.Services.CreateScope();
        ApplicationDbContext    loadCtx   = loadScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Truck                   loaded    = await loadCtx.Trucks.SingleAsync(t => t.Id == truck.Id);

        loaded.Code.Value.Should().Be("TROUNDTRIP");
        loaded.Name.Value.Should().Be(truck.Name.Value);
        loaded.Description.Value.Should().Be("Tipper, 6x4, EuroVI");
        loaded.Status.Should().Be(ETruckStatus.Loading);
        loaded.TenantId.Should().Be(TenantId.Default);
        loaded.ConcurrencyStamp.Version.Should().Be(1);
        loaded.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Empty_description_round_trips_as_Empty_singleton()
    {
        Truck truck;
        using (IServiceScope scope = _fixture.Services.CreateScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            IDateTimeProvider    clock   = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

            truck = NewTruck(code: "TEMPTYDESC", clock: clock, description: TruckDescription.Empty);
            context.Trucks.Add(truck);
            await context.SaveChangesAsync();
        }

        using IServiceScope  loadScope = _fixture.Services.CreateScope();
        ApplicationDbContext loadCtx   = loadScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Truck                loaded    = await loadCtx.Trucks.SingleAsync(t => t.Id == truck.Id);

        loaded.Description.IsEmpty.Should().BeTrue();
    }

    // ----------------------------------------------------------------------------------
    // 2. Dual-write atomicity (ADR-0003)
    // ----------------------------------------------------------------------------------

    [Fact]
    public async Task Single_mutation_writes_state_and_event_atomically()
    {
        Truck truck;
        using (IServiceScope scope = _fixture.Services.CreateScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            IDateTimeProvider    clock   = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

            truck = NewTruck(code: "TDUALWRITE", clock: clock);
            context.Trucks.Add(truck);
            await context.SaveChangesAsync(); // raises TruckCreated
        }

        using (IServiceScope scope = _fixture.Services.CreateScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            IDateTimeProvider    clock   = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

            Truck loaded = await context.Trucks.SingleAsync(t => t.Id == truck.Id);
            loaded.Update(
                       new TruckUpdates(Name: TruckName.Create("Renamed").Value!),
                       clock,
                       updatedByUserId: Guid.CreateVersion7()
                   )
                  .IsSuccess.Should().BeTrue();
            await context.SaveChangesAsync();
        }

        using IServiceScope             verifyScope = _fixture.Services.CreateScope();
        ApplicationDbContext            verifyCtx   = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Truck reloaded = await verifyCtx.Trucks.SingleAsync(t => t.Id == truck.Id);
        reloaded.Name.Value.Should().Be("Renamed");
        reloaded.ConcurrencyStamp.Version.Should().Be(2UL);

        List<TruckDomainEvent> events = await verifyCtx.TruckDomainEvents
                                                       .Where(e => e.AggregateId == truck.Id.Value)
                                                       .OrderBy(e => e.AggregateVersion)
                                                       .ToListAsync();

        events.Should().HaveCount(2);
        events[0].EventType.Should().Be(nameof(Domain.Events.Trucks.TruckCreated));
        events[0].AggregateVersion.Should().Be(1L);
        events[1].EventType.Should().Be(nameof(Domain.Events.Trucks.TruckRenamed));
        events[1].AggregateVersion.Should().Be(2L);
    }

    // ----------------------------------------------------------------------------------
    // 3. Forced failure rolls both writes back
    // ----------------------------------------------------------------------------------

    [Fact]
    public async Task Save_failure_rolls_back_both_state_change_and_event()
    {
        Truck truck;
        using (IServiceScope scope = _fixture.Services.CreateScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            IDateTimeProvider    clock   = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

            truck = NewTruck(code: "TROLLBACK", clock: clock);
            context.Trucks.Add(truck);
            await context.SaveChangesAsync();
        }

        using (IServiceScope scope = _fixture.Services.CreateScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            IDateTimeProvider    clock   = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

            Truck loaded = await context.Trucks.SingleAsync(t => t.Id == truck.Id);
            loaded.Update(
                new TruckUpdates(Name: TruckName.Create("Should Rollback").Value!),
                clock,
                updatedByUserId: Guid.CreateVersion7()
            );

            // Force the save to fail by also inserting a second truck with the same code
            // → partial unique index violation on the second insert → whole tx rolls back.
            Truck conflicting = NewTruck(code: "TROLLBACK", clock: clock);
            context.Trucks.Add(conflicting);

            Func<Task> act = async () => await context.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateException>();
        }

        using IServiceScope  verifyScope = _fixture.Services.CreateScope();
        ApplicationDbContext verifyCtx   = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Truck reloaded = await verifyCtx.Trucks.SingleAsync(t => t.Id == truck.Id);
        reloaded.Name.Value.Should().Be(truck.Name.Value);           // un-renamed
        reloaded.ConcurrencyStamp.Version.Should().Be(1UL);          // stamp not bumped

        int eventCount = await verifyCtx.TruckDomainEvents.CountAsync(e => e.AggregateId == truck.Id.Value);
        eventCount.Should().Be(1);                                   // only TruckCreated; no TruckRenamed
    }

    // ----------------------------------------------------------------------------------
    // 4. Optimistic concurrency
    // ----------------------------------------------------------------------------------

    [Fact]
    public async Task Stale_concurrency_stamp_throws_DbUpdateConcurrencyException()
    {
        Truck truck;
        using (IServiceScope scope = _fixture.Services.CreateScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            IDateTimeProvider    clock   = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

            truck = NewTruck(code: "TCONCUR", clock: clock);
            context.Trucks.Add(truck);
            await context.SaveChangesAsync();
        }

        using IServiceScope     scopeA = _fixture.Services.CreateScope();
        using IServiceScope     scopeB = _fixture.Services.CreateScope();
        ApplicationDbContext    ctxA   = scopeA.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        ApplicationDbContext    ctxB   = scopeB.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        IDateTimeProvider       clockA = scopeA.ServiceProvider.GetRequiredService<IDateTimeProvider>();
        IDateTimeProvider       clockB = scopeB.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        Truck a = await ctxA.Trucks.SingleAsync(t => t.Id == truck.Id);
        Truck b = await ctxB.Trucks.SingleAsync(t => t.Id == truck.Id);

        a.Update(new TruckUpdates(Name: TruckName.Create("Winner").Value!), clockA, Guid.CreateVersion7());
        await ctxA.SaveChangesAsync();

        b.Update(new TruckUpdates(Name: TruckName.Create("Loser").Value!), clockB, Guid.CreateVersion7());
        Func<Task> act = async () => await ctxB.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    // ----------------------------------------------------------------------------------
    // 5. Soft delete + query filter (ADR-0009)
    // ----------------------------------------------------------------------------------

    [Fact]
    public async Task Soft_delete_query_filter_hides_deleted_truck_and_IgnoreQueryFilters_opts_in()
    {
        Truck truck;
        using (IServiceScope scope = _fixture.Services.CreateScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            IDateTimeProvider    clock   = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

            truck = NewTruck(code: "TSOFTDEL", clock: clock);
            context.Trucks.Add(truck);
            await context.SaveChangesAsync();

            Truck loaded = await context.Trucks.SingleAsync(t => t.Id == truck.Id);
            loaded.Delete(clock, deletedByUserId: Guid.CreateVersion7());
            await context.SaveChangesAsync();
        }

        using IServiceScope  verifyScope = _fixture.Services.CreateScope();
        ApplicationDbContext verifyCtx   = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Truck? hidden = await verifyCtx.Trucks.FirstOrDefaultAsync(t => t.Id == truck.Id);
        hidden.Should().BeNull();

        Truck? viaIgnore = await verifyCtx.Trucks.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == truck.Id);
        viaIgnore.Should().NotBeNull();
        viaIgnore!.IsDeleted.Should().BeTrue();
    }

    // ----------------------------------------------------------------------------------
    // 6. Partial unique index (ADR-0033) — soft delete frees the code
    // ----------------------------------------------------------------------------------

    [Fact]
    public async Task Soft_deleted_truck_frees_its_code_for_reuse_and_live_dup_still_fails()
    {
        // Create, soft-delete
        using (IServiceScope scope = _fixture.Services.CreateScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            IDateTimeProvider    clock   = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

            Truck first = NewTruck(code: "TREUSE", clock: clock);
            context.Trucks.Add(first);
            await context.SaveChangesAsync();

            first.Delete(clock, deletedByUserId: Guid.CreateVersion7());
            await context.SaveChangesAsync();
        }

        // Re-use the freed code — must succeed
        using (IServiceScope scope = _fixture.Services.CreateScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            IDateTimeProvider    clock   = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

            Truck second = NewTruck(code: "TREUSE", clock: clock);
            context.Trucks.Add(second);
            await context.SaveChangesAsync();
        }

        // Now try to add a THIRD with the same code while the second is alive — must fail
        using (IServiceScope scope = _fixture.Services.CreateScope())
        {
            ApplicationDbContext context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            IDateTimeProvider    clock   = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

            Truck third = NewTruck(code: "TREUSE", clock: clock);
            context.Trucks.Add(third);

            Func<Task> act = async () => await context.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateException>();
        }
    }

    // ----------------------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------------------

    private static Truck NewTruck(
                                     string             code,
                                     IDateTimeProvider  clock,
                                     TruckDescription?  description = null,
                                     ETruckStatus       status      = ETruckStatus.OutOfService
                                 )
    {
        return Truck.Create(
                       id:              new TruckId(Guid.CreateVersion7()),
                       tenantId:        TenantId.Default,
                       code:            TruckCode.Create(code).Value!,
                       name:            TruckName.Create($"Truck {code}").Value!,
                       description:     description ?? TruckDescription.Empty,
                       initialStatus:   status,
                       clock:           clock,
                       createdByUserId: Guid.CreateVersion7()
                   ).Value!;
    }
}

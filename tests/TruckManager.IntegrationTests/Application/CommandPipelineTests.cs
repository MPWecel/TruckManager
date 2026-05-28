using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

using TruckManager.Common.Results;
using TruckManager.Domain.Aggregates.Trucks;
using TruckManager.Domain.Enums;
using TruckManager.Domain.ValueObjects;
using TruckManager.Application.Abstractions.Cqrs;
using TruckManager.Application.Trucks.Commands.ChangeTruckStatus;
using TruckManager.Application.Trucks.Commands.CreateTruck;
using TruckManager.Application.Trucks.Commands.DeleteTruck;
using TruckManager.Application.Trucks.Commands.UpdateTruck;
using TruckManager.Infrastructure.Persistence;
using TruckManager.IntegrationTests.TestHelpers;

namespace TruckManager.IntegrationTests.Application;

// Phase 5 / Section G.
//
// Full pipeline smoke tests (Testcontainers-backed Postgres + production DI graph).
// Each test exercises one scenario end-to-end:     Dispatcher → ValidationBehavior → UnitOfWorkBehavior → Handler → EF Core → real Postgres.
//
// Truck codes are unique per test (UniqueCode()) to avoid conflicts in the shared container.
// The IClassFixture container is shared across all tests in the class; only non-dictionary tables (Trucks, TruckDomainEvents) are mutated here.
public sealed class CommandPipelineTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture = fixture;

    // Returns an 8-char uppercase alphanumeric code unique to each call.
    private static string UniqueCode() => Guid.NewGuid()
                                              .ToString("N")
                                              .ToUpperInvariant()[..8];

    // ---- CreateTruck ------------------------------------------------------

    [Fact]
    public async Task CreateTruck_writes_truck_row_and_TruckCreated_event_row()
    {
        // Arrange
        using IServiceScope scope = _fixture.Services.CreateScope();
        
        ICommandDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
        ApplicationDbContext ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        string code = UniqueCode();
        CreateTruckCommand command = new(
                                            TenantId: Guid.NewGuid(),
                                            Code: code,
                                            Name: "Integration Truck",
                                            Description: "Smoke test truck",
                                            InitialStatus: ETruckStatus.OutOfService
                                        );

        // Act
        Result<TruckId> result = await dispatcher.SendAsync(command, CancellationToken.None);

        // Assert — dispatcher returns success
        result.IsSuccess.Should()
                        .BeTrue();
        Guid truckId = result.Value!.Value;

        // Assert — state row written
        Truck? truck = await ctx.Trucks
                                .Where(t => t.Id == new TruckId(truckId))
                                .FirstOrDefaultAsync();
        
        truck.Should()
             .NotBeNull();
        truck!.Code.Value.Should()
                         .Be(code);
        truck.Status.Should()
                    .Be(ETruckStatus.OutOfService);
        truck.IsDeleted.Should()
                       .BeFalse();

        // Assert — TruckCreated event row written in the same transaction
        int eventCount = await ctx.TruckDomainEvents.CountAsync(e => e.AggregateId == truckId);
        
        eventCount.Should()
                  .Be(1);
        
        ctx.TruckDomainEvents.Single(e => e.AggregateId == truckId)
                             .EventType.Should()
                                       .Contain("TruckCreated");
    }

    [Fact]
    public async Task CreateTruck_with_invalid_code_returns_Validation_failure_and_writes_no_rows()
    {
        // Arrange
        using IServiceScope scope = _fixture.Services.CreateScope();
        
        ICommandDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
        ApplicationDbContext ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Guid.Empty triggers the TenantId NotEmpty rule at the validator stage
        CreateTruckCommand command = new(
                                            TenantId: Guid.Empty,
                                            Code: "OK001",
                                            Name: "Should Not Persist",
                                            Description: null,
                                            InitialStatus: ETruckStatus.OutOfService
                                        );

        // Act
        Result<TruckId> result = await dispatcher.SendAsync(command, CancellationToken.None);

        // Assert — dispatcher short-circuits at ValidationBehavior; handler never runs
        result.IsSuccess.Should()
                        .BeFalse();
        result.Errors.Should()
                     .Contain(e => e.Type == EErrorType.Validation);
    }

    // ---- UpdateTruck ------------------------------------------------------

    [Fact]
    public async Task UpdateTruck_mutates_truck_and_writes_TruckRenamed_event()
    {
        // Arrange — create a truck first so we have something to update
        Guid truckId = await CreateTruckAndGetIdAsync("Updated Name Truck");

        using IServiceScope scope = _fixture.Services.CreateScope();
        
        ICommandDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
        ApplicationDbContext ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        string newName = "Renamed Integration Truck";
        UpdateTruckCommand command = new(truckId, Name: newName, Description: null);

        // Act
        Result result = await dispatcher.SendAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should()
                        .BeTrue();

        Truck? truck = await ctx.Trucks
                                .Where(t => t.Id == new TruckId(truckId))
                                .FirstOrDefaultAsync();
        
        truck!.Name.Value.Should()
                         .Be(newName);

        // TruckCreated (version 1) + TruckRenamed (version 2)
        int eventCount = await ctx.TruckDomainEvents.CountAsync(e => e.AggregateId == truckId);
        eventCount.Should()
                  .Be(2);
    }

    // ---- ChangeTruckStatus ------------------------------------------------

    [Fact]
    public async Task ChangeTruckStatus_updates_status_and_writes_TruckStatusChanged_event()
    {
        // Arrange — truck starts OutOfService; transition to Loading is allowed
        Guid truckId = await CreateTruckAndGetIdAsync("Status Change Truck");

        using IServiceScope scope = _fixture.Services.CreateScope();
        
        ICommandDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
        ApplicationDbContext ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        ChangeTruckStatusCommand command = new(truckId, ETruckStatus.Loading);

        // Act
        Result result = await dispatcher.SendAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should()
                        .BeTrue();

        Truck? truck = await ctx.Trucks
                                .Where(t => t.Id == new TruckId(truckId))
                                .FirstOrDefaultAsync();
        truck!.Status.Should()
                     .Be(ETruckStatus.Loading);

        int eventCount = await ctx.TruckDomainEvents.CountAsync(e => e.AggregateId == truckId);
        eventCount.Should()
                  .Be(2); // TruckCreated + TruckStatusChanged
    }

    // ---- DeleteTruck ------------------------------------------------------

    [Fact]
    public async Task DeleteTruck_soft_deletes_truck_and_writes_TruckDeleted_event()
    {
        // Arrange
        Guid truckId = await CreateTruckAndGetIdAsync("Delete Me Truck");

        using IServiceScope scope = _fixture.Services.CreateScope();
        
        ICommandDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
        ApplicationDbContext ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        DeleteTruckCommand command = new(truckId);

        // Act
        Result result = await dispatcher.SendAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should()
                        .BeTrue();

        // Must use IgnoreQueryFilters to see deleted row
        Truck? truck = await ctx.Trucks
                                .IgnoreQueryFilters()
                                .Where(t => t.Id == new TruckId(truckId))
                                .FirstOrDefaultAsync();
        truck.Should()
             .NotBeNull();
        truck!.IsDeleted.Should()
                        .BeTrue();
        truck.DeletedAtUtc.Should()
                          .NotBeNull();

        int eventCount = await ctx.TruckDomainEvents.CountAsync(e => e.AggregateId == truckId);
        eventCount.Should()
                  .Be(2); // TruckCreated + TruckDeleted
    }

    // ---- Rollback path ----------------------------------------------------

    [Fact]
    public async Task Handler_returning_NotFound_triggers_rollback_and_writes_no_rows()
    {
        // Arrange — use a truck ID that was never created
        using IServiceScope scope = _fixture.Services.CreateScope();
        
        ICommandDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
        ApplicationDbContext ctx = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        Guid nonExistentId = Guid.NewGuid();
        UpdateTruckCommand command = new(nonExistentId, Name: "Ghost Truck", Description: null);

        // Act
        Result result = await dispatcher.SendAsync(command, CancellationToken.None);

        // Assert — failure returned
        result.IsSuccess.Should()
                        .BeFalse();
        result.Errors.Should()
                     .ContainSingle().Which.Type.Should()
                                                .Be(EErrorType.NotFound);

        // Assert — no rows written anywhere
        bool truckExists  = await ctx.Trucks.AnyAsync(t => t.Id == new TruckId(nonExistentId));
        
        bool eventExists  = await ctx.TruckDomainEvents.AnyAsync(e => e.AggregateId == nonExistentId);
        
        truckExists.Should()
                   .BeFalse();
        eventExists.Should()
                   .BeFalse();
    }

    // ---- Private helpers --------------------------------------------------

    // Creates a truck via the command pipeline and returns the new TruckId Guid.
    private async Task<Guid> CreateTruckAndGetIdAsync(string name)
    {
        using IServiceScope scope = _fixture.Services.CreateScope();
        
        ICommandDispatcher dispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();

        CreateTruckCommand command = new(
                                            TenantId: Guid.NewGuid(),
                                            Code: UniqueCode(),
                                            Name: name,
                                            Description: null,
                                            InitialStatus: ETruckStatus.OutOfService
                                        );

        Result<TruckId> result = await dispatcher.SendAsync(command, CancellationToken.None);
        
        result.IsSuccess.Should()
                        .BeTrue("prerequisite truck creation must succeed");
        
        return result.Value!.Value;
    }
}

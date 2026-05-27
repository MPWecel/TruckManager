using AwesomeAssertions;
using Xunit;

using Microsoft.EntityFrameworkCore;

using TruckManager.Common.Results;
using TruckManager.Domain.Enums;
using TruckManager.Infrastructure.Persistence;
using TruckManager.Application.Trucks.Commands.UpdateTruck;
using TruckManager.UnitTests.TestHelpers;

namespace TruckManager.UnitTests.Application.Tests.Trucks.Commands;

public class UpdateTruckHandlerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 13, 13, 37, 0, TimeSpan.Zero);

    // ---- Happy path -------------------------------------------------------

    [Fact]
    public async Task HandleAsync_returns_Success_when_truck_exists_and_name_is_updated()
    {
        // Arrange
        string dbName = Guid.NewGuid()
                            .ToString();
        FakeDateTimeProvider clock = new(T0);

        using (ApplicationDbContext seedCtx = TestDbContextFactory.Create(dbName))
        {
            seedCtx.Trucks.Add(TruckTestFactory.NewValid(clock));
            
            seedCtx.SaveChanges();
        }

        using ApplicationDbContext ctx = TestDbContextFactory.Create(dbName);
        Guid truckId = ctx.Trucks
                          .First()
                          .Id
                          .Value;

        UpdateTruckHandler handler = new(ctx, FakeCurrentUserService.Anonymous(), clock);
        UpdateTruckCommand command = new(truckId, Name: "Updated Name", Description: null);

        // Act
        Result result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should()
                        .BeTrue();
    }

    [Fact]
    public async Task HandleAsync_returns_Success_when_no_fields_change_no_op()
    {
        // Arrange
        string dbName = Guid.NewGuid()
                            .ToString();
        FakeDateTimeProvider clock = new(T0);

        using (ApplicationDbContext seedCtx = TestDbContextFactory.Create(dbName))
        {
            seedCtx.Trucks.Add(TruckTestFactory.NewValid(clock, nameRaw: "Stable Name"));
            
            seedCtx.SaveChanges();
        }

        using ApplicationDbContext ctx = TestDbContextFactory.Create(dbName);
        Guid truckId = ctx.Trucks
                          .First()
                          .Id
                          .Value;

        UpdateTruckHandler handler = new(ctx, FakeCurrentUserService.Anonymous(), clock);
        // Same name — aggregate returns Success with no mutation
        UpdateTruckCommand command = new(truckId, Name: "Stable Name", Description: null);

        // Act
        Result result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should()
                        .BeTrue();
    }

    // ---- Failure paths ----------------------------------------------------

    [Fact]
    public async Task HandleAsync_returns_NotFound_when_truck_does_not_exist()
    {
        // Arrange
        using ApplicationDbContext ctx = TestDbContextFactory.Create();
        FakeDateTimeProvider clock = new(T0);

        UpdateTruckHandler handler = new(ctx, FakeCurrentUserService.Anonymous(), clock);
        UpdateTruckCommand command = new(Guid.NewGuid(), Name: "Any", Description: null);

        // Act
        Result result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should()
                        .BeFalse();
        result.Errors.Should()
                     .ContainSingle()
                     .Which.Type.Should()
                                .Be(EErrorType.NotFound);
    }

    [Fact]
    public async Task HandleAsync_returns_Conflict_when_truck_is_deleted()
    {
        // Arrange
        string dbName = Guid.NewGuid()
                            .ToString();
        FakeDateTimeProvider clock = new(T0);

        using (ApplicationDbContext seedCtx = TestDbContextFactory.Create(dbName))
        {
            var truck = TruckTestFactory.NewValid(clock);
            truck.Delete(clock, Guid.NewGuid());
            seedCtx.Trucks.Add(truck);
            
            seedCtx.SaveChanges();
        }

        using ApplicationDbContext ctx = TestDbContextFactory.Create(dbName);
        // IgnoreQueryFilters is needed to find deleted trucks in handler
        Guid truckId = ctx.Trucks
                          .IgnoreQueryFilters()
                          .First()
                          .Id
                          .Value;

        UpdateTruckHandler handler = new(ctx, FakeCurrentUserService.Anonymous(), clock);
        UpdateTruckCommand command = new(truckId, Name: "Should Fail", Description: null);

        // Act
        Result result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should()
                        .BeFalse();
        result.Errors.Should()
                     .ContainSingle()
                     .Which.Type.Should()
                                .Be(EErrorType.Conflict);
    }
}

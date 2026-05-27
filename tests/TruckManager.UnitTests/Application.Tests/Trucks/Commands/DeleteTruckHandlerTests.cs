using AwesomeAssertions;
using Xunit;

using Microsoft.EntityFrameworkCore;

using TruckManager.Common.Results;
using TruckManager.Domain.Enums;
using TruckManager.Infrastructure.Persistence;
using TruckManager.Application.Trucks.Commands.DeleteTruck;
using TruckManager.UnitTests.TestHelpers;

namespace TruckManager.UnitTests.Application.Tests.Trucks.Commands;

public class DeleteTruckHandlerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 13, 13, 37, 0, TimeSpan.Zero);

    // ---- Happy path -------------------------------------------------------

    [Fact]
    public async Task HandleAsync_returns_Success_when_truck_exists()
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

        DeleteTruckHandler handler = new(ctx, FakeCurrentUserService.Anonymous(), clock);
        DeleteTruckCommand command = new(truckId);

        // Act
        Result result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    // ---- Failure paths ----------------------------------------------------

    [Fact]
    public async Task HandleAsync_returns_NotFound_when_truck_does_not_exist()
    {
        // Arrange
        using ApplicationDbContext ctx = TestDbContextFactory.Create();
        FakeDateTimeProvider clock = new(T0);

        DeleteTruckHandler handler = new(ctx, FakeCurrentUserService.Anonymous(), clock);
        DeleteTruckCommand command = new(Guid.NewGuid());

        // Act
        Result result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle()
              .Which.Type.Should().Be(EErrorType.NotFound);
    }

    [Fact]
    public async Task HandleAsync_returns_Conflict_when_truck_is_already_deleted()
    {
        // Arrange
        string dbName = Guid.NewGuid().ToString();
        FakeDateTimeProvider clock = new(T0);

        using (ApplicationDbContext seedCtx = TestDbContextFactory.Create(dbName))
        {
            var truck = TruckTestFactory.NewValid(clock);
            truck.Delete(clock, Guid.NewGuid());
            seedCtx.Trucks.Add(truck);
            seedCtx.SaveChanges();
        }

        using ApplicationDbContext ctx = TestDbContextFactory.Create(dbName);
        Guid truckId = ctx.Trucks.IgnoreQueryFilters().First().Id.Value;

        DeleteTruckHandler handler = new(ctx, FakeCurrentUserService.Anonymous(), clock);
        DeleteTruckCommand command = new(truckId);

        // Act
        Result result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle()
              .Which.Type.Should().Be(EErrorType.Conflict);
    }
}

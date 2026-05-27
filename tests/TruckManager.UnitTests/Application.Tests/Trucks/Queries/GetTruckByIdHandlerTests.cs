using AwesomeAssertions;
using Xunit;

using Microsoft.EntityFrameworkCore;

using TruckManager.Common.Results;
using TruckManager.Domain.Enums;
using TruckManager.Infrastructure.Persistence;
using TruckManager.Application.Trucks.DTOs;
using TruckManager.Application.Trucks.Queries.GetTruckById;
using TruckManager.UnitTests.TestHelpers;

namespace TruckManager.UnitTests.Application.Tests.Trucks.Queries;

public class GetTruckByIdHandlerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 13, 13, 37, 0, TimeSpan.Zero);

    // ---- Happy path -------------------------------------------------------

    [Fact]
    public async Task HandleAsync_returns_Success_with_correct_DTO_when_truck_exists()
    {
        // Arrange
        string dbName = Guid.NewGuid()
                            .ToString();
        FakeDateTimeProvider clock = new(T0);
        Guid truckGuid = Guid.NewGuid();

        using (ApplicationDbContext seedCtx = TestDbContextFactory.Create(dbName))
        {
            seedCtx.Trucks.Add(
                                  TruckTestFactory.NewValid(
                                                               clock, 
                                                               codeRaw: "TRUCK01", 
                                                               nameRaw: "My Truck", 
                                                               id: truckGuid
                                                           )
                              );
            seedCtx.SaveChanges();
        }

        using ApplicationDbContext ctx = TestDbContextFactory.Create(dbName);
        GetTruckByIdHandler handler = new(ctx);
        GetTruckByIdQuery query = new(truckGuid);

        // Act
        Result<TruckDto> result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should()
                        .BeTrue();
        result.Value.Should()
                    .NotBeNull();
        result.Value!.Id.Should()
                        .Be(truckGuid);
        result.Value.Code.Should()
                         .Be("TRUCK01");
        result.Value.Name.Should()
                         .Be("My Truck");
        result.Value.IsDeleted.Should()
                              .BeFalse();
    }

    // ---- Failure paths ----------------------------------------------------

    [Fact]
    public async Task HandleAsync_returns_NotFound_when_truck_does_not_exist()
    {
        // Arrange
        using ApplicationDbContext ctx = TestDbContextFactory.Create();
        GetTruckByIdHandler handler = new(ctx);
        GetTruckByIdQuery query = new(Guid.NewGuid());

        // Act
        Result<TruckDto> result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should()
                        .BeFalse();
        result.Errors.Should()
                     .ContainSingle().Which.Type.Should()
                                                .Be(EErrorType.NotFound);
    }

    [Fact]
    public async Task HandleAsync_returns_NotFound_for_soft_deleted_truck()
    {
        // Soft-delete filter is active for query handlers — deleted trucks are invisible.
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
        // Resolve the ID directly via the model's IgnoreQueryFilters to build the query
        Guid truckId = ctx.Trucks.IgnoreQueryFilters()
                                 .First()
                                 .Id.Value;
        GetTruckByIdHandler handler = new(ctx);
        GetTruckByIdQuery query = new(truckId);

        // Act
        Result<TruckDto> result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert — soft-deleted truck returns 404 to callers
        result.IsSuccess.Should()
                        .BeFalse();
        result.Errors.Should()
                     .ContainSingle().Which.Type.Should()
                                                .Be(EErrorType.NotFound);
    }
}

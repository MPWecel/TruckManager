using AwesomeAssertions;
using Xunit;

using TruckManager.Common.Results;
using TruckManager.Domain.Enums;
using TruckManager.Infrastructure.Persistence;
using TruckManager.Application.Trucks.Commands.ChangeTruckStatus;
using TruckManager.UnitTests.TestHelpers;

namespace TruckManager.UnitTests.Application.Tests.Trucks.Commands;

public class ChangeTruckStatusHandlerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 13, 13, 37, 0, TimeSpan.Zero);

    // ---- Happy path -------------------------------------------------------

    [Fact]
    public async Task HandleAsync_returns_Success_for_allowed_transition()
    {
        // Arrange
        string dbName = Guid.NewGuid().ToString();
        FakeDateTimeProvider clock = new(T0);

        using (ApplicationDbContext seedCtx = TestDbContextFactory.Create(dbName))
        {
            seedCtx.Trucks.Add(TruckTestFactory.NewValid(clock, initialStatus: ETruckStatus.OutOfService));
            seedCtx.SaveChanges();
        }

        using ApplicationDbContext ctx = TestDbContextFactory.Create(dbName);
        Guid truckId = ctx.Trucks.First().Id.Value;

        var policy = FakeTruckStatusTransitionPolicy.WithDefaultWorkflow();
        ChangeTruckStatusHandler handler = new(ctx, policy, FakeCurrentUserService.Anonymous(), clock, new FakeCorrelationContext());
        ChangeTruckStatusCommand command = new(truckId, ETruckStatus.Loading);

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

        ChangeTruckStatusHandler handler = new(ctx, FakeTruckStatusTransitionPolicy.AllowEverything(), FakeCurrentUserService.Anonymous(), clock, new FakeCorrelationContext());
        ChangeTruckStatusCommand command = new(Guid.NewGuid(), ETruckStatus.Loading);

        // Act
        Result result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle()
              .Which.Type.Should().Be(EErrorType.NotFound);
    }

    [Fact]
    public async Task HandleAsync_returns_Validation_failure_when_transition_is_not_allowed()
    {
        // Arrange
        string dbName = Guid.NewGuid().ToString();
        FakeDateTimeProvider clock = new(T0);

        using (ApplicationDbContext seedCtx = TestDbContextFactory.Create(dbName))
        {
            // Start at Loading — policy denies Loading → AtJob
            seedCtx.Trucks.Add(TruckTestFactory.NewValid(clock, initialStatus: ETruckStatus.Loading));
            seedCtx.SaveChanges();
        }

        using ApplicationDbContext ctx = TestDbContextFactory.Create(dbName);
        Guid truckId = ctx.Trucks.First().Id.Value;

        ChangeTruckStatusHandler handler = new(ctx, FakeTruckStatusTransitionPolicy.DenyAll(), FakeCurrentUserService.Anonymous(), clock, new FakeCorrelationContext());
        ChangeTruckStatusCommand command = new(truckId, ETruckStatus.AtJob);

        // Act
        Result result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Errors.Should().ContainSingle()
              .Which.Type.Should().Be(EErrorType.Validation);
    }
}

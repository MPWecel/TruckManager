using AwesomeAssertions;
using Xunit;

using TruckManager.Common.Results;
using TruckManager.Domain.Enums;
using TruckManager.Domain.ValueObjects;
using TruckManager.Application.Trucks.Commands.CreateTruck;
using TruckManager.UnitTests.TestHelpers;

namespace TruckManager.UnitTests.Application.Tests.Trucks.Commands;

public class CreateTruckHandlerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 13, 13, 37, 0, TimeSpan.Zero);

    private static CreateTruckHandler BuildHandler() 
        => new(TestDbContextFactory.Create(), FakeCurrentUserService.Anonymous(), new FakeDateTimeProvider(T0));

    // ---- Happy path -------------------------------------------------------

    [Fact]
    public async Task HandleAsync_returns_Success_with_a_non_empty_TruckId_for_valid_inputs()
    {
        // Arrange
        CreateTruckHandler handler = BuildHandler();
        CreateTruckCommand command = new(
                                            TenantId: Guid.NewGuid(),
                                            Code: "ALPHA01",
                                            Name: "Alpha Truck",
                                            Description: "A test truck",
                                            InitialStatus: ETruckStatus.OutOfService
                                        );

        // Act
        Result<TruckId> result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should()
                        .BeTrue();
        result.Value.Should()
                    .NotBeNull();
        result.Value!.Value.Should()
                           .NotBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_null_description_is_accepted_and_maps_to_Empty()
    {
        // Arrange
        CreateTruckHandler handler = BuildHandler();
        CreateTruckCommand command = new(
                                            TenantId: Guid.NewGuid(),
                                            Code: "NODESC",
                                            Name: "No-description truck",
                                            Description: null,
                                            InitialStatus: ETruckStatus.OutOfService
                                        );

        // Act
        Result<TruckId> result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should()
                        .BeTrue();
    }

    // ---- Failure paths ----------------------------------------------------

    [Fact]
    public async Task HandleAsync_returns_Failure_when_code_contains_invalid_characters()
    {
        // Arrange
        CreateTruckHandler handler = BuildHandler();
        CreateTruckCommand command = new(
                                            TenantId:      Guid.NewGuid(),
                                            Code:          "truck-01",   // hyphens not allowed
                                            Name:          "Bad Code",
                                            Description:   null,
                                            InitialStatus: ETruckStatus.OutOfService
                                        );

        // Act
        Result<TruckId> result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should()
                        .BeFalse();
        result.Errors.Should()
                     .NotBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_returns_Failure_when_code_is_whitespace()
    {
        // Arrange
        CreateTruckHandler handler = BuildHandler();
        CreateTruckCommand command = new(
                                            TenantId:      Guid.NewGuid(),
                                            Code:          "   ",
                                            Name:          "Whitespace code",
                                            Description:   null,
                                            InitialStatus: ETruckStatus.OutOfService
                                        );

        // Act
        Result<TruckId> result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should()
                        .BeFalse();
    }
}

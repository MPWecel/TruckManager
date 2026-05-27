using AwesomeAssertions;
using FluentValidation.Results;
using Xunit;

using TruckManager.Domain.Enums;
using TruckManager.Application.Trucks.Commands.ChangeTruckStatus;

namespace TruckManager.UnitTests.Application.Tests.Trucks.Commands;

public class ChangeTruckStatusValidatorTests
{
    private static readonly ChangeTruckStatusValidator Validator = new();

    private static ChangeTruckStatusCommand Valid() => new(
        TruckId:   Guid.NewGuid(),
        NewStatus: ETruckStatus.Loading
    );

    [Fact]
    public void Valid_command_passes_validation()
    {
        ValidationResult result = Validator.Validate(Valid());
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Empty_TruckId_fails()
    {
        ValidationResult result = Validator.Validate(Valid() with { TruckId = Guid.Empty });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ChangeTruckStatusCommand.TruckId));
    }

    [Fact]
    public void Undefined_NewStatus_fails()
    {
        ValidationResult result = Validator.Validate(Valid() with { NewStatus = (ETruckStatus)99 });
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(ChangeTruckStatusCommand.NewStatus));
    }

    [Theory]
    [InlineData(ETruckStatus.OutOfService)]
    [InlineData(ETruckStatus.Loading)]
    [InlineData(ETruckStatus.ToJob)]
    [InlineData(ETruckStatus.AtJob)]
    [InlineData(ETruckStatus.Returning)]
    public void All_defined_ETruckStatus_values_pass_validation(ETruckStatus status)
    {
        ValidationResult result = Validator.Validate(Valid() with { NewStatus = status });
        result.IsValid.Should().BeTrue();
    }
}

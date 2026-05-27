using AwesomeAssertions;
using FluentValidation.Results;
using Xunit;

using TruckManager.Application.Trucks.Commands.DeleteTruck;

namespace TruckManager.UnitTests.Application.Tests.Trucks.Commands;

public class DeleteTruckValidatorTests
{
    private static readonly DeleteTruckValidator Validator = new();

    [Fact]
    public void Valid_command_passes_validation()
    {
        ValidationResult result = Validator.Validate(new DeleteTruckCommand(Guid.NewGuid()));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Empty_TruckId_fails()
    {
        ValidationResult result = Validator.Validate(new DeleteTruckCommand(Guid.Empty));
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle()
              .Which.PropertyName.Should().Be(nameof(DeleteTruckCommand.TruckId));
    }
}

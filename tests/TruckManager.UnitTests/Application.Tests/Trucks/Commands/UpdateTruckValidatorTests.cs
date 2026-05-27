using AwesomeAssertions;
using FluentValidation.Results;
using Xunit;

using TruckManager.Domain.ValueObjects;
using TruckManager.Application.Trucks.Commands.UpdateTruck;

namespace TruckManager.UnitTests.Application.Tests.Trucks.Commands;

public class UpdateTruckValidatorTests
{
    private static readonly UpdateTruckValidator Validator = new();

    private static UpdateTruckCommand Valid() => new(
                                                        TruckId: Guid.NewGuid(),
                                                        Name: "New Name",
                                                        Description: "New description"
                                                    );

    [Fact]
    public void Valid_command_passes_validation()
    {
        //Arrange+Act
        ValidationResult result = Validator.Validate(Valid());
        
        //Assert
        result.IsValid.Should()
                      .BeTrue();
    }

    [Fact]
    public void Null_name_and_null_description_is_valid_no_op_command()
    {
        //Arrange+Act
        ValidationResult result = Validator.Validate(Valid() with { Name = null, Description = null });
        
        //Assert
        result.IsValid.Should()
                      .BeTrue();
    }

    [Fact]
    public void Empty_TruckId_fails()
    {
        //Arrange+act
        ValidationResult result = Validator.Validate(Valid() with { TruckId = Guid.Empty });
        
        //Assert
        result.IsValid.Should()
                      .BeFalse();
        result.Errors.Should()
                     .Contain(e => e.PropertyName == nameof(UpdateTruckCommand.TruckId));
    }

    [Fact]
    public void Empty_string_Name_fails_when_Name_is_not_null()
    {
        //Arrange+Act
        // Null → skip the rule; empty string → fail
        ValidationResult result = Validator.Validate(Valid() with { Name = "" });
        
        //assert
        result.IsValid.Should()
                      .BeFalse();
        result.Errors.Should()
                     .Contain(e => e.PropertyName == nameof(UpdateTruckCommand.Name));
    }

    [Fact]
    public void Name_exceeding_max_length_fails()
    {
        //arrange+act
        string longName = new('X', TruckName.MaxLength + 1);
        ValidationResult result = Validator.Validate(Valid() with { Name = longName });
        
        //assert
        result.IsValid.Should()
                      .BeFalse();
        result.Errors.Should()
                     .Contain(e => e.PropertyName == nameof(UpdateTruckCommand.Name));
    }

    [Fact]
    public void Description_exceeding_max_length_fails()
    {
        //Arrange+act
        string longDesc = new('D', TruckDescription.MaxLength + 1);
        ValidationResult result = Validator.Validate(Valid() with { Description = longDesc });
        
        //Assert
        result.IsValid.Should()
                      .BeFalse();
        result.Errors.Should()
                     .Contain(e => e.PropertyName == nameof(UpdateTruckCommand.Description));
    }
}

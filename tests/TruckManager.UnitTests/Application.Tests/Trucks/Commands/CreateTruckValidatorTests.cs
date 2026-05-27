using AwesomeAssertions;
using FluentValidation.Results;
using Xunit;

using TruckManager.Domain.Enums;
using TruckManager.Domain.ValueObjects;
using TruckManager.Application.Trucks.Commands.CreateTruck;

namespace TruckManager.UnitTests.Application.Tests.Trucks.Commands;

public class CreateTruckValidatorTests
{
    private static readonly CreateTruckValidator Validator = new();

    private static CreateTruckCommand Valid() 
        => new(
                  TenantId: Guid.NewGuid(),
                  Code: "VALID01",
                  Name: "Valid Name",
                  Description: null,
                  InitialStatus: ETruckStatus.OutOfService
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
    public void Empty_TenantId_fails()
    {
        //Arrange+Act
        ValidationResult result = Validator.Validate(Valid() with { TenantId = Guid.Empty });
        
        //Arrange
        result.IsValid.Should()
                      .BeFalse();
        result.Errors.Should()
                     .Contain(e => e.PropertyName == nameof(CreateTruckCommand.TenantId));
    }

    [Fact]
    public void Empty_Code_fails()
    {
        //Arrange+Act
        ValidationResult result = Validator.Validate(Valid() with { Code = "" });
        
        //Assert
        result.IsValid.Should()
                      .BeFalse();
        result.Errors.Should()
                     .Contain(e => e.PropertyName == nameof(CreateTruckCommand.Code));
    }

    [Fact]
    public void Code_exceeding_max_length_fails()
    {
        //Arrange+Act
        string longCode = new('A', TruckCode.MaxLength + 1);
        ValidationResult result = Validator.Validate(Valid() with { Code = longCode });
        
        //Assert
        result.IsValid.Should()
                      .BeFalse();
        result.Errors.Should()
                     .Contain(e => e.PropertyName == nameof(CreateTruckCommand.Code));
    }

    [Fact]
    public void Code_with_non_alphanumeric_characters_fails()
    {
        //Arrange+Act
        ValidationResult result = Validator.Validate(Valid() with { Code = "BAD-CODE" });
        
        //Assert
        result.IsValid.Should()
                      .BeFalse();
        result.Errors.Should()
                     .Contain(e => e.PropertyName == nameof(CreateTruckCommand.Code));
    }

    [Fact]
    public void Empty_Name_fails()
    {
        
        ValidationResult result = Validator.Validate(Valid() with { Name = "" });
        
        
        result.IsValid.Should()
                      .BeFalse();
        result.Errors.Should()
                     .Contain(e => e.PropertyName == nameof(CreateTruckCommand.Name));
    }

    [Fact]
    public void Name_exceeding_max_length_fails()
    {

        string longName = new('X', TruckName.MaxLength + 1);
        ValidationResult result = Validator.Validate(Valid() with { Name = longName });
        
        
        result.IsValid.Should()
                      .BeFalse();
        result.Errors.Should()
                     .Contain(e => e.PropertyName == nameof(CreateTruckCommand.Name));
    }

    [Fact]
    public void Description_exceeding_max_length_fails()
    {

        string longDesc = new('D', TruckDescription.MaxLength + 1);
        ValidationResult result = Validator.Validate(Valid() with { Description = longDesc });
        
        
        result.IsValid.Should()
                      .BeFalse();
        result.Errors.Should()
                     .Contain(e => e.PropertyName == nameof(CreateTruckCommand.Description));
    }

    [Fact]
    public void Invalid_InitialStatus_fails()
    {

        ValidationResult result = Validator.Validate(Valid() with { InitialStatus = (ETruckStatus)99 });
        
        
        result.IsValid.Should()
                      .BeFalse();
        result.Errors.Should()
                     .Contain(e => e.PropertyName == nameof(CreateTruckCommand.InitialStatus));
    }
}

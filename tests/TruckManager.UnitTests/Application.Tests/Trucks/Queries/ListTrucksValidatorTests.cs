using AwesomeAssertions;
using FluentValidation.Results;
using Xunit;

using TruckManager.Domain.Enums;
using TruckManager.Application.Trucks.Queries.ListTrucks;

namespace TruckManager.UnitTests.Application.Tests.Trucks.Queries;

public class ListTrucksValidatorTests
{
    private static readonly ListTrucksValidator Validator = new();

    private static ListTrucksQuery Valid() => new(Page: 1, PageSize: 20, StatusFilter: null);

    [Fact]
    public void Valid_query_passes_validation()
    {
        ValidationResult result = Validator.Validate(Valid());
        
        //Assert
        result.IsValid.Should()
                      .BeTrue();
    }

    [Fact]
    public void Valid_query_with_StatusFilter_passes_validation()
    {
        ValidationResult result = Validator.Validate(Valid() with { StatusFilter = ETruckStatus.Loading });
        
        //Assert
        result.IsValid.Should()
                      .BeTrue();
    }

    [Fact]
    public void Page_zero_fails()
    {
        ValidationResult result = Validator.Validate(Valid() with { Page = 0 });
        
        //Assert
        result.IsValid.Should()
                      .BeFalse();
        result.Errors.Should()
                     .Contain(e => e.PropertyName == nameof(ListTrucksQuery.Page));
    }

    [Fact]
    public void PageSize_zero_fails()
    {
        ValidationResult result = Validator.Validate(Valid() with { PageSize = 0 });
        
        //Assert
        result.IsValid.Should()
                      .BeFalse();
        result.Errors.Should()
                     .Contain(e => e.PropertyName == nameof(ListTrucksQuery.PageSize));
    }

    [Fact]
    public void PageSize_exceeding_100_fails()
    {
        ValidationResult result = Validator.Validate(Valid() with { PageSize = 101 });
        
        //Assert
        result.IsValid.Should()
                      .BeFalse();
        result.Errors.Should()
                     .Contain(e => e.PropertyName == nameof(ListTrucksQuery.PageSize));
    }

    [Fact]
    public void PageSize_of_100_passes_validation()
    {
        //Arrange+act
        ValidationResult result = Validator.Validate(Valid() with { PageSize = 100 });
        
        //Assert
        result.IsValid.Should()
                      .BeTrue();
    }

    [Fact]
    public void Undefined_StatusFilter_value_fails()
    {
        //Arrange+Act
        ValidationResult result = Validator.Validate(Valid() with { StatusFilter = (ETruckStatus)(99) });
        
        //Assert
        result.IsValid.Should()
                      .BeFalse();
        result.Errors.Should()
                     .Contain(
                                e => e.PropertyName
                                      .Contains(nameof(ListTrucksQuery.StatusFilter))
                             );
    }
}

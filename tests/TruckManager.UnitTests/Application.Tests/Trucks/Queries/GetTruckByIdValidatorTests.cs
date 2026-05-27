using AwesomeAssertions;
using FluentValidation.Results;
using Xunit;

using TruckManager.Application.Trucks.Queries.GetTruckById;

namespace TruckManager.UnitTests.Application.Tests.Trucks.Queries;

public class GetTruckByIdValidatorTests
{
    private static readonly GetTruckByIdValidator Validator = new();

    [Fact]
    public void Valid_query_passes_validation()
    {
        ValidationResult result = Validator.Validate(new GetTruckByIdQuery(Guid.NewGuid()));
        result.IsValid.Should()
                      .BeTrue();
    }

    [Fact]
    public void Empty_TruckId_fails()
    {
        ValidationResult result = Validator.Validate(new GetTruckByIdQuery(Guid.Empty));
        result.IsValid.Should()
                      .BeFalse();
        result.Errors.Should()
                     .ContainSingle().Which.PropertyName.Should()
                                                        .Be(nameof(GetTruckByIdQuery.TruckId));
    }
}

using AwesomeAssertions;
using Xunit;

using TruckManager.Common.Results;
using TruckManager.Domain.ValueObjects;

namespace TruckManager.UnitTests.Domain.Tests.Aggregates.Trucks;

public class TruckCodeTests
{
    [Fact]
    public void Create_throws_ArgumentNullException_when_raw_is_null()
    {
        //Arrange
        Action act = () => TruckCode.Create(null!);

        //Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Empty_input_fails_with_validation_error()
    {
        //Arrange
        Result<TruckCode> result = TruckCode.Create("");

        //Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle()
                              .Which.Code
                              .Should().Be("truck.code.empty");
    }

    [Fact]
    public void Whitespace_only_input_fails_after_normalization()
    {
        //Arrange
        Result<TruckCode> result = TruckCode.Create("    ");

        //Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle()
                              .Which.Code
                              .Should().Be("truck.code.empty");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(15)]
    [InlineData(TruckCode.MaxLength)]
    public void Length_within_boundary_succeeds(int length)
    {
        //Arrange
        string raw = new('A', length);
        Result<TruckCode> result = TruckCode.Create(raw);

        //Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Value.Length.Should().Be(length);
    }

    [Fact]
    public void Length_over_max_fails()
    {
        //Arrange
        string raw = new('A', TruckCode.MaxLength + 1);
        Result<TruckCode> result = TruckCode.Create(raw);

        //Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle()
                              .Which.Code
                              .Should().Be("truck.code.too_long");
    }

    [Fact]
    public void Lowercase_letters_are_normalized_to_uppercase()
    {
        //Arrange
        Result<TruckCode> result = TruckCode.Create("trk01");

        //Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Value.Should().Be("TRK01");
    }

    [Fact]
    public void Edge_whitespace_is_trimmed()
    {
        //Arrange
        Result<TruckCode> result = TruckCode.Create("  TRK01  ");

        //Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Value.Should().Be("TRK01");
    }

    [Theory]
    [InlineData("TRK-01")]
    [InlineData("TRK.01")]
    [InlineData("TRK 01")]
    [InlineData("TRK/01")]
    [InlineData("TRK_01")]
    [InlineData("TRK!01")]
    public void Non_alphanumeric_characters_are_rejected(string raw)
    {
        //Arrange
        Result<TruckCode> result = TruckCode.Create(raw);

        //Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle()
                              .Which.Code
                              .Should().Be("truck.code.invalid_charset");
    }

    [Fact]
    public void Pure_numeric_codes_are_allowed()
    {
        //Arrange
        Result<TruckCode> result = TruckCode.Create("12345");

        //Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Value.Should().Be("12345");
    }

    [Fact]
    public void Mixed_alphanumeric_codes_are_allowed()
    {
        //Arrange
        Result<TruckCode> result = TruckCode.Create("ABC123DEF456");

        //Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Value.Should().Be("ABC123DEF456");
    }

    [Fact]
    public void Two_TruckCodes_with_the_same_normalized_value_are_equal()
    {
        //Arrange
        TruckCode a = TruckCode.Create(" trk01 ").Value!;
        TruckCode b = TruckCode.Create("TRK01").Value!;

        //Assert
        int aHashCode = a.GetHashCode();
        int bHashCode = b.GetHashCode();
        a.Should().Be(b);
        aHashCode.Should().Be(bHashCode);
    }
}

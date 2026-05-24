using AwesomeAssertions;
using Xunit;

using TruckManager.Common.Results;
using TruckManager.Domain.ValueObjects;

namespace TruckManager.UnitTests.Domain.Tests.Aggregates.Trucks;

public class TruckNameTests
{
    [Fact]
    public void Create_throws_ArgumentNullException_when_raw_is_null()
    {
        //Arrange
        Action act = () => TruckName.Create(null!);

        //Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Empty_input_fails_with_validation_error()
    {
        //Arrange
        Result<TruckName> result = TruckName.Create("");

        //Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle()
                              .Which.Code.Should().Be("truck.name.empty");
    }

    [Fact]
    public void Whitespace_only_input_fails_after_trim()
    {
        //Arrange
        Result<TruckName> result = TruckName.Create("    ");

        //Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle()
                              .Which.Code.Should().Be("truck.name.empty");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(TruckName.MaxLength)]
    public void Length_within_boundary_succeeds(int length)
    {
        //Arrange
        string raw = new('a', length);
        Result<TruckName> result = TruckName.Create(raw);

        //Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Value.Length.Should().Be(length);
    }

    [Fact]
    public void Length_over_max_fails()
    {
        //Arrange
        string raw = new('a', TruckName.MaxLength + 1);
        Result<TruckName> result = TruckName.Create(raw);

        //Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle()
                              .Which.Code
                              .Should().Be("truck.name.too_long");
    }

    [Fact]
    public void Edge_whitespace_is_trimmed()
    {
        //Arrange
        Result<TruckName> result = TruckName.Create("  Mountain Hauler  ");

        //Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Value.Should().Be("Mountain Hauler");
    }

    [Theory]
    [InlineData("\0")]      // NUL
    [InlineData("\t")]      // tab
    [InlineData("\n")]      // newline
    [InlineData("\r")]      // CR
    [InlineData("")]       // unit separator
    [InlineData("")]        // DEL
    public void Control_characters_are_rejected(string controlChar)
    {
        //Arrange
        Result<TruckName> result = TruckName.Create($"Valid{controlChar}Name");

        //Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle()
                              .Which.Code
                              .Should().Be("truck.name.control_chars");
    }

    [Fact]
    public void International_characters_are_allowed()
    {
        //Arrange
        Result<TruckName> result = TruckName.Create("Wóz Drzymały");

        //Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Value.Should().Be("Wóz Drzymały");
    }

    [Fact]
    public void Two_TruckNames_with_the_same_value_are_equal()
    {
        //Arrange
        TruckName a = TruckName.Create("Same Name").Value!;
        TruckName b = TruckName.Create("Same Name").Value!;

        //Assert
        int aHashCode = a.GetHashCode();
        int bHashCode = b.GetHashCode();
        a.Should().Be(b);
        aHashCode.Should().Be(bHashCode);
    }
}

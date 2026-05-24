using AwesomeAssertions;
using Xunit;

using TruckManager.Common.Results;
using TruckManager.Domain.ValueObjects;

namespace TruckManager.UnitTests.Domain.Tests.Aggregates.Trucks;

public class TruckDescriptionTests
{
    [Fact]
    public void Null_input_yields_Empty()
    {
        //Arrange
        Result<TruckDescription> result = TruckDescription.Create(null);

        //Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(TruckDescription.Empty);
    }

    [Fact]
    public void Empty_string_yields_Empty()
    {
        //Arrange
        Result<TruckDescription> result = TruckDescription.Create("");

        //Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(TruckDescription.Empty);
    }

    [Fact]
    public void Whitespace_only_input_yields_Empty()
    {
        //Arrange
        Result<TruckDescription> result = TruckDescription.Create("   ");

        //Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(TruckDescription.Empty);
    }

    [Fact]
    public void Empty_static_field_reports_IsEmpty_true_and_has_empty_value()
    {
        //Assert
        TruckDescription.Empty.IsEmpty.Should().BeTrue();
        TruckDescription.Empty.Value.Should().Be(String.Empty);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(TruckDescription.MaxLength)]
    public void Length_within_boundary_succeeds(int length)
    {
        //Arrange
        string raw = new('x', length);
        Result<TruckDescription> result = TruckDescription.Create(raw);

        //Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Value.Length.Should().Be(length);
        result.Value!.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void Length_over_max_fails()
    {
        //Arrange
        string raw = new('x', TruckDescription.MaxLength + 1);
        Result<TruckDescription> result = TruckDescription.Create(raw);

        //Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle()
                              .Which.Code
                              .Should().Be("truck.description.too_long");
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\t")]
    [InlineData("\0")]
    [InlineData("")]
    public void Control_characters_are_rejected(string controlChar)
    {
        //Arrange
        Result<TruckDescription> result = TruckDescription.Create("Valid" + controlChar + "Text");

        //Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle()
                              .Which.Code
                              .Should().Be("truck.description.control_chars");
    }

    [Fact]
    public void Edge_whitespace_is_trimmed()
    {
        //Arrange
        Result<TruckDescription> result = TruckDescription.Create("  trimmed  ");

        //Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.Value.Should().Be("trimmed");
    }

    [Fact]
    public void Two_descriptions_with_the_same_content_are_equal()
    {
        //Arrange
        TruckDescription a = TruckDescription.Create("Same content").Value!;
        TruckDescription b = TruckDescription.Create("Same content").Value!;

        //Assert
        int aHashCode = a.GetHashCode();
        int bHashCode = b.GetHashCode();
        a.Should().Be(b);
        aHashCode.Should().Be(bHashCode);
    }
}

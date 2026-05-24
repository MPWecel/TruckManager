using AwesomeAssertions;
using Xunit;

using TruckManager.Common.Abstractions;
using TruckManager.Domain.ValueObjects;

namespace TruckManager.UnitTests.Domain.Tests.Aggregates.Trucks;

public class TruckIdTests
{
    [Fact]
    public void Two_TruckIds_with_the_same_Guid_are_equal()
    {
        //Arrange
        Guid guid = Guid.NewGuid();
        TruckId a = new(guid);
        TruckId b = new(guid);

        //Assert
        int aHash = a.GetHashCode();
        int bHash = b.GetHashCode();
        a.Should().Be(b);
        aHash.Should().Be(bHash);
    }

    [Fact]
    public void Two_TruckIds_with_different_Guids_are_not_equal()
    {
        //Arrange
        TruckId a = new(Guid.NewGuid());
        TruckId b = new(Guid.NewGuid());

        //Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void TruckId_implements_the_strongly_typed_id_marker_for_Guid()
    {
        //Arrange
        TruckId id = new(Guid.NewGuid());

        //Assert
        id.Should().BeAssignableTo<IStronglyTypedId<Guid>>();
    }

    [Fact]
    public void Value_property_exposes_the_backing_Guid()
    {
        //Arrange
        Guid guid = Guid.NewGuid();
        TruckId id = new(guid);

        //Assert
        id.Value.Should().Be(guid);
    }
}

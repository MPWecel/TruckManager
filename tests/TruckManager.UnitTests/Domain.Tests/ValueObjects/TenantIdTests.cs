using AwesomeAssertions;
using Xunit;

using TruckManager.Common.Abstractions;
using TruckManager.Common.Constants;
using TruckManager.Domain.ValueObjects;

namespace TruckManager.UnitTests.Domain.Tests.ValueObjects;

public class TenantIdTests
{
    [Fact]
    public void Two_TenantIds_with_the_same_Guid_are_equal()    //Verifies both reference and value equality
    {
        // Arrange
        Guid guid = Guid.NewGuid();
        TenantId a = new(guid);
        TenantId b = new(guid);

        // Assert
        a.Should().Be(b);

        int aHashCode = a.GetHashCode();
        int bHashCode = b.GetHashCode();
        aHashCode.Should().Be(bHashCode);
    }

    [Fact]
    public void Two_TenantIds_with_different_Guids_are_not_equal()
    {
        //Arrange
        TenantId a = new(Guid.NewGuid());
        TenantId b = new(Guid.NewGuid());

        // Assert
        a.Should().NotBe(b);
    }

    [Fact]
    public void Default_TenantId_Value_matches_the_DefaultTenantId_constant_value() => TenantId.Default.Value.Should().Be(Tenants.DefaultTenantId);

    [Fact]
    public void Default_is_stable_across_accesses()
    {
        //Arrange
        TenantId first = TenantId.Default;
        TenantId second = TenantId.Default;

        //Assert
        first.Should().BeSameAs(second);
    }

    [Fact]
    public void DefaultTenantId_constant_is_a_valid_UUIDv7()
    {
        // Expectation & Explanation:
        // Version nibble (upper nibble of the 7th byte / 13th hex char of the unhyphenated form) must be 7. The variant nibble (upper nibble of the 9th byte) must be 8, 9, A, or B.
        
        // Arrange
        Guid id = Tenants.DefaultTenantId;
        byte[] bytes = id.ToByteArray();

        // Guid serialization on .NET endian-flips the first three fields, but byte index 7 in the canonical RFC layout corresponds to byte[7] in ToByteArray() (4th byte of the 16-bit time_hi_and_version field — same after the Microsoft flip).
        byte versionNibble = (byte)(bytes[7] >> 4);
        byte variantNibble = (byte)(bytes[8] >> 4);

        byte expectedVersionNibble = (byte)(0x7);
        byte[] allowedVariantNibbles = [(byte)(0x8), (byte)(0x9), (byte)(0xA), (byte)(0xB)];

        //Assert
        versionNibble.Should().Be(expectedVersionNibble);
        variantNibble.Should().BeOneOf(allowedVariantNibbles);
    }

    [Fact]
    public void TenantId_implements_the_strongly_typed_id_marker_for_Guid()
    {
        // Arrange
        TenantId id = new(Guid.NewGuid());

        // Assert
        id.Should().BeAssignableTo<IStronglyTypedId<Guid>>();
    }

    [Fact]
    public void Value_property_exposes_the_backing_Guid()
    {
        // Arrange
        Guid guid = Guid.NewGuid();
        TenantId id = new(guid);

        // Assert
        id.Value.Should().Be(guid);
    }
}

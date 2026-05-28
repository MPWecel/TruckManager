using AwesomeAssertions;
using Xunit;

using TruckManager.Common.Abstractions;
using TruckManager.Domain.Common;
using TruckManager.Domain.ValueObjects;
using TruckManager.UnitTests.TestHelpers;

namespace TruckManager.UnitTests.Domain.Tests.Common;

public class BaseEntityTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 13, 13, 37, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_assigns_id_tenant_and_concurrency_stamp()
    {
        //Arrange
        TestEntityId id = new(Guid.NewGuid());
        TenantId tenant = TenantId.Default;
        FakeDateTimeProvider clock = new(T0);
        ConcurrencyStamp stamp = ConcurrencyStamp.Initial(clock);

        //Act
        TestEntity entity = new(id, tenant, stamp);

        //Assert
        entity.Id.Should()
                 .Be(id);
        entity.TenantId.Should()
                       .Be(tenant);
        entity.ConcurrencyStamp.Should()
                               .Be(stamp);
    }

    [Fact]
    public void Equality_is_reference_based_not_field_based()
    {
        //Arrange
        TestEntityId id = new(Guid.NewGuid());
        TenantId tenant = TenantId.Default;
        FakeDateTimeProvider clock = new(T0);
        ConcurrencyStamp stamp = ConcurrencyStamp.Initial(clock);

        //Act
        TestEntity a = new(id, tenant, stamp);
        TestEntity b = new(id, tenant, stamp);

        //Assert
        // Same id, tenant, and concurrency stamp — but distinct instances should not be equal.
        a.Should()
         .NotBe(b);
        a.Equals(b).Should()
                   .BeFalse();
    }

    [Fact]
    public void Same_instance_is_equal_to_itself()
    {
        //Arrange
        TestEntityId id = new(Guid.NewGuid());
        TestEntity entity = new(id, TenantId.Default, ConcurrencyStamp.Initial(new FakeDateTimeProvider(T0)));

        //Assert
        // ReSharper disable once EqualExpressionComparison
        entity.Equals(entity).Should()
                             .BeTrue();
        entity.GetHashCode().Should()
                            .Be(entity.GetHashCode());
    }

    [Fact]
    public void Constructor_throws_when_id_is_null()
    {
        //Arrange
        Action act = () => new TestEntity(null!, TenantId.Default, ConcurrencyStamp.Initial(new FakeDateTimeProvider(T0)));

        //Assert
        act.Should()
           .Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_throws_when_tenant_is_null()
    {
        //Arrange
        Action act = () => new TestEntity(new TestEntityId(Guid.NewGuid()), null!, ConcurrencyStamp.Initial(new FakeDateTimeProvider(T0)));

        //Assert
        act.Should()
           .Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_throws_when_concurrency_stamp_is_null()
    {
        //Arrange
        Action act = () => new TestEntity(new TestEntityId(Guid.NewGuid()), TenantId.Default, null!);

        //Assert
        act.Should()
           .Throw<ArgumentNullException>();
    }

    #region TestTypes

    private sealed record TestEntityId(Guid Value) : IStronglyTypedId<Guid>;

    private sealed class TestEntity(TestEntityId id, TenantId tenantId, ConcurrencyStamp concurrencyStamp) 
        : BaseEntity<TestEntityId>(id, tenantId, concurrencyStamp)
    { }

    #endregion
}

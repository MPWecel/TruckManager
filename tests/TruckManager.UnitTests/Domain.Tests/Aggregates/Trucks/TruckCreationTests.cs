using AwesomeAssertions;
using Xunit;

using TruckManager.Domain.Aggregates.Trucks;
using TruckManager.Domain.Enums;
using TruckManager.Domain.Events.Trucks;
using TruckManager.Domain.ValueObjects;
using TruckManager.UnitTests.TestHelpers;

namespace TruckManager.UnitTests.Domain.Tests.Aggregates.Trucks;

public class TruckCreationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 13, 13, 37, 0, TimeSpan.Zero);

    [Fact]
    public void Create_returns_a_non_null_truck_for_valid_inputs()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Truck truck = TruckTestFactory.NewValid(clock);

        //Assert
        truck.Should().NotBeNull();
    }

    [Fact]
    public void Created_truck_has_all_fields_populated_from_inputs()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Guid truckGuid = Guid.NewGuid();
        Guid creatorId = Guid.NewGuid();
        Truck truck = TruckTestFactory.NewValid(
                                                   clock: clock,
                                                   initialStatus: ETruckStatus.Loading,
                                                   codeRaw: "TRK42",
                                                   nameRaw: "Heavy Hauler",
                                                   descriptionRaw: "Spec description",
                                                   id: truckGuid,
                                                   createdByUserId: creatorId
                                               );

        //Assert
        truck.Id.Value.Should().Be(truckGuid);
        truck.TenantId.Should().Be(TenantId.Default);
        truck.Code.Value.Should().Be("TRK42");
        truck.Name.Value.Should().Be("Heavy Hauler");
        truck.Description.Value.Should().Be("Spec description");
        truck.Status.Should().Be(ETruckStatus.Loading);
    }

    [Fact]
    public void Created_truck_initialises_audit_fields_from_the_clock_and_user()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Guid creatorId = Guid.NewGuid();
        Truck truck = TruckTestFactory.NewValid(clock, createdByUserId: creatorId);

        //Assert
        truck.CreatedByUserId.Should().Be(creatorId);
        truck.UpdatedByUserId.Should().Be(creatorId);
        truck.CreatedAtUtc.Should().Be(T0);
        truck.UpdatedAtUtc.Should().Be(T0);
        truck.IsDeleted.Should().BeFalse();
        truck.DeletedAtUtc.Should().BeNull();
        truck.DeletedByUserId.Should().BeNull();
    }

    [Fact]
    public void Created_truck_starts_with_ConcurrencyStamp_version_1()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Truck truck = TruckTestFactory.NewValid(clock);

        //Assert
        truck.ConcurrencyStamp.Version.Should().Be(1UL);
        truck.ConcurrencyStamp.LastModifiedUtc.Should().Be(T0);
    }

    [Fact]
    public void Create_raises_a_single_TruckCreated_event_with_full_snapshot()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Guid creatorId = Guid.NewGuid();
        Guid correlationId = Guid.NewGuid();
        
        Truck truck = TruckTestFactory.NewValid(
                                                   clock: clock,
                                                   initialStatus: ETruckStatus.Loading,
                                                   codeRaw: "TRK42",
                                                   nameRaw: "Heavy Hauler",
                                                   descriptionRaw: "Spec description",
                                                   createdByUserId: creatorId,
                                                   correlationId: correlationId
                                               );
        
        TruckCreated evt = truck.DomainEvents.OfType<TruckCreated>().Single();

        //Assert
        truck.DomainEvents.Should().ContainSingle()
                                   .Which
                                   .Should().BeOfType<TruckCreated>();

        evt.AggregateId.Should().Be(truck.Id.Value);
        evt.AggregateVersion.Should().Be(1UL);
        evt.OccurredAtUtc.Should().Be(T0);
        evt.PerformedByUserId.Should().Be(creatorId);
        evt.TenantId.Should().Be(truck.TenantId);
        evt.CorrelationId.Should().Be(correlationId);
        evt.Code.Value.Should().Be("TRK42");
        evt.Name.Value.Should().Be("Heavy Hauler");
        evt.Description.Value.Should().Be("Spec description");
        evt.Status.Should().Be(ETruckStatus.Loading);
    }

    [Fact]
    public void Create_throws_ArgumentNullException_when_clock_is_null()
    {
        //Arrange
        Action act = () => Truck.Create(
                                           id: new TruckId(Guid.NewGuid()),
                                           tenantId: TenantId.Default,
                                           code: TruckCode.Create("TRK01").Value!,
                                           name: TruckName.Create("Test").Value!,
                                           description: TruckDescription.Create("Desc").Value!,
                                           initialStatus: ETruckStatus.OutOfService,
                                           clock: null!,
                                           createdByUserId: Guid.NewGuid()
                                       );

        //Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_throws_ArgumentNullException_when_id_is_null()
    {
        //Arrange
        Action act = () => Truck.Create(
                                           id: null!,
                                           tenantId: TenantId.Default,
                                           code: TruckCode.Create("TRK01").Value!,
                                           name: TruckName.Create("Test").Value!,
                                           description: TruckDescription.Create("Desc").Value!,
                                           initialStatus: ETruckStatus.OutOfService,
                                           clock: new FakeDateTimeProvider(T0),
                                           createdByUserId: Guid.NewGuid()
                                       );

        //Assert
        act.Should().Throw<ArgumentNullException>();
    }
}

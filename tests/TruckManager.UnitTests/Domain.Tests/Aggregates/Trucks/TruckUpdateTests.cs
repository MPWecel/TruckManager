using AwesomeAssertions;
using Xunit;

using TruckManager.Common.Results;
using TruckManager.Domain.Aggregates.Trucks;
using TruckManager.Domain.Events;
using TruckManager.Domain.Events.Trucks;
using TruckManager.Domain.ValueObjects;
using TruckManager.UnitTests.TestHelpers;

namespace TruckManager.UnitTests.Domain.Tests.Aggregates.Trucks;

public class TruckUpdateTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 13, 13, 37, 0, TimeSpan.Zero);

    [Fact]
    public void Update_with_both_fields_null_is_a_no_op()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Truck truck = TruckTestFactory.NewValid(clock);
        ConcurrencyStamp stampBefore = truck.ConcurrencyStamp;
        int eventCountBefore = truck.DomainEvents.Count;

        //Act
        clock.Advance(TimeSpan.FromMinutes(5));
        Result result = truck.Update(new TruckUpdates(), clock, Guid.NewGuid());

        //Assert
        result.IsSuccess.Should().BeTrue();
        truck.ConcurrencyStamp.Should().Be(stampBefore);
        truck.DomainEvents.Should().HaveCount(eventCountBefore);
    }

    [Fact]
    public void Update_with_fields_equal_to_current_values_is_a_no_op()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Truck truck = TruckTestFactory.NewValid(clock);
        ConcurrencyStamp stampBefore = truck.ConcurrencyStamp;
        int eventCountBefore = truck.DomainEvents.Count;

        //Act
        clock.Advance(TimeSpan.FromMinutes(5));
        Result result = truck.Update(
                                        new TruckUpdates(Name: truck.Name, Description: truck.Description),
                                        clock,
                                        Guid.NewGuid()
                                    );

        //Assert
        result.IsSuccess.Should().BeTrue();
        truck.ConcurrencyStamp.Should().Be(stampBefore);
        truck.DomainEvents.Should().HaveCount(eventCountBefore);
    }

    [Fact]
    public void Update_changing_only_name_raises_TruckRenamed_with_old_and_new_values()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Truck truck = TruckTestFactory.NewValid(clock, nameRaw: "Old Name");
        ConcurrencyStamp stampBefore = truck.ConcurrencyStamp;
        int eventCountBefore = truck.DomainEvents.Count;

        //Act
        clock.Advance(TimeSpan.FromMinutes(5));
        TruckName newName = TruckName.Create("New Name").Value!;
        Result result = truck.Update(new TruckUpdates(Name: newName), clock, Guid.NewGuid());

        //Assert
        result.IsSuccess.Should().BeTrue();
        truck.Name.Value.Should().Be("New Name");
        truck.ConcurrencyStamp.Version.Should().Be(stampBefore.Version + 1);
        truck.DomainEvents.Should().HaveCount(eventCountBefore + 1);

        TruckRenamed evt = truck.DomainEvents.OfType<TruckRenamed>().Single();
        evt.OldName.Value.Should().Be("Old Name");
        evt.NewName.Value.Should().Be("New Name");
        evt.AggregateVersion.Should().Be(truck.ConcurrencyStamp.Version);
    }

    [Fact]
    public void Update_changing_only_description_raises_TruckDescriptionChanged()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Truck truck = TruckTestFactory.NewValid(clock, descriptionRaw: "Old desc");
        ConcurrencyStamp stampBefore = truck.ConcurrencyStamp;

        //Act
        TruckDescription newDescription = TruckDescription.Create("New desc").Value!;
        Result result = truck.Update(new TruckUpdates(Description: newDescription), clock, Guid.NewGuid());

        //Assert
        result.IsSuccess.Should().BeTrue();
        truck.Description.Value.Should().Be("New desc");
        truck.ConcurrencyStamp.Version.Should().Be(stampBefore.Version + 1);

        TruckDescriptionChanged evt = truck.DomainEvents.OfType<TruckDescriptionChanged>().Single();
        evt.OldDescription.Value.Should().Be("Old desc");
        evt.NewDescription.Value.Should().Be("New desc");
    }

    [Fact]
    public void Update_changing_both_raises_two_events_sharing_one_AggregateVersion()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Truck truck = TruckTestFactory.NewValid(clock, nameRaw: "Old Name", descriptionRaw: "Old desc");
        ConcurrencyStamp stampBefore = truck.ConcurrencyStamp;
        int eventCountBefore = truck.DomainEvents.Count;
        TruckName newName = TruckName.Create("New Name").Value!;

        //Act
        TruckDescription newDescription = TruckDescription.Create("New desc").Value!;
        Result result = truck.Update(new TruckUpdates(Name: newName, Description: newDescription), clock, Guid.NewGuid());

        //Assert
        result.IsSuccess.Should().BeTrue();

        // Single stamp increment per logical mutation (ADR-0026).
        truck.ConcurrencyStamp.Version.Should().Be(stampBefore.Version + 1);

        // Two events.
        truck.DomainEvents.Should().HaveCount(eventCountBefore + 2);

        // Both events share the new AggregateVersion.
        ulong expectedVersion = stampBefore.Version + 1;
        IEnumerable<DomainEvent> newEvents = truck.DomainEvents.Skip(eventCountBefore);
        newEvents.Should().AllSatisfy(e => e.AggregateVersion.Should().Be(expectedVersion));

        truck.DomainEvents.OfType<TruckRenamed>().Should().ContainSingle();
        truck.DomainEvents.OfType<TruckDescriptionChanged>().Should().ContainSingle();
    }

    [Fact]
    public void Update_on_deleted_truck_returns_failure_with_Conflict()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Truck truck = TruckTestFactory.NewValid(clock);
        
        //Act
        truck.Delete(clock, Guid.NewGuid());

        ConcurrencyStamp stampBefore = truck.ConcurrencyStamp;
        int eventCountBefore = truck.DomainEvents.Count;

        TruckName newName = TruckName.Create("New").Value!;
        Result result = truck.Update(new TruckUpdates(Name: newName), clock, Guid.NewGuid());

        //Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle()
                              .Which.Type
                              .Should().Be(EErrorType.Conflict);
        truck.ConcurrencyStamp.Should().Be(stampBefore);
        truck.DomainEvents.Should().HaveCount(eventCountBefore);
    }
}

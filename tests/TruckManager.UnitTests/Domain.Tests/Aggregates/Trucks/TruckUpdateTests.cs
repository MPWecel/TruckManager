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

    // ---- Phase 8 / Section G gap-fill ------------------------------------------------

    [Fact]
    public void Update_throws_ArgumentNullException_when_updates_is_null()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Truck truck = TruckTestFactory.NewValid(clock);

        //Act
        Action act = () => truck.Update(null!, clock, Guid.NewGuid());

        //Assert
        act.Should().Throw<ArgumentNullException>()
                    .Which.ParamName.Should().Be("updates");
    }

    [Fact]
    public void Update_throws_ArgumentNullException_when_clock_is_null()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Truck truck = TruckTestFactory.NewValid(clock);

        //Act
        Action act = () => truck.Update(new TruckUpdates(), null!, Guid.NewGuid());

        //Assert
        act.Should().Throw<ArgumentNullException>()
                    .Which.ParamName.Should().Be("clock");
    }

    [Fact]
    public void Update_setting_description_from_Empty_to_non_empty_raises_TruckDescriptionChanged_with_Empty_old_value()
    {
        // Truck.Update treats a description change as "new value present AND not Equal to current". TruckDescription.Empty is a non-null singleton — going Empty→non-empty must produce an event whose OldDescription is the Empty singleton.
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Truck truck = TruckTestFactory.NewValid(clock, descriptionRaw: ""); // factory's TruckDescription.Create("") collapses to the Empty singleton
        truck.Description.Should().BeSameAs(TruckDescription.Empty, "precondition: a Truck created with empty description carries the Empty singleton (TruckDescription.Create normalizes empty/whitespace → Empty).");

        //Act
        TruckDescription newDesc = TruckDescription.Create("Now has a description").Value!;
        Result result = truck.Update(new TruckUpdates(Description: newDesc), clock, Guid.NewGuid());

        //Assert
        result.IsSuccess.Should().BeTrue();
        truck.Description.Value.Should().Be("Now has a description");

        TruckDescriptionChanged evt = truck.DomainEvents.OfType<TruckDescriptionChanged>().Single();
        evt.OldDescription.Should().BeSameAs(TruckDescription.Empty);
        evt.NewDescription.Should().Be(newDesc);
    }

    [Fact]
    public void Update_clearing_description_from_non_empty_to_Empty_raises_TruckDescriptionChanged_with_Empty_new_value()
    {
        // Symmetric to the previous test — the user can clear an existing description by passing TruckDescription.Empty (or equivalently a created-from-empty-string descriptor).
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Truck truck = TruckTestFactory.NewValid(clock, descriptionRaw: "Existing description");
        TruckDescription oldDesc = truck.Description;

        //Act
        Result result = truck.Update(new TruckUpdates(Description: TruckDescription.Empty), clock, Guid.NewGuid());

        //Assert
        result.IsSuccess.Should().BeTrue();
        truck.Description.Should().BeSameAs(TruckDescription.Empty);

        TruckDescriptionChanged evt = truck.DomainEvents.OfType<TruckDescriptionChanged>().Single();
        evt.OldDescription.Should().Be(oldDesc);
        evt.NewDescription.Should().BeSameAs(TruckDescription.Empty);
    }

    [Fact]
    public void Update_changing_only_name_does_not_raise_TruckDescriptionChanged()
    {
        // Negative assertion that complements Update_changing_only_name_raises_TruckRenamed_with_old_and_new_values — the rename event arrives but the description event does NOT.
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Truck truck = TruckTestFactory.NewValid(clock, nameRaw: "Old Name", descriptionRaw: "Unchanged desc");
        int descEventsBefore = truck.DomainEvents.OfType<TruckDescriptionChanged>().Count();

        //Act
        TruckName newName = TruckName.Create("New Name").Value!;
        Result result = truck.Update(new TruckUpdates(Name: newName), clock, Guid.NewGuid());

        //Assert
        result.IsSuccess.Should().BeTrue();
        truck.DomainEvents.OfType<TruckDescriptionChanged>().Count().Should().Be(descEventsBefore);
    }
}

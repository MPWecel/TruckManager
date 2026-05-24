using AwesomeAssertions;
using Xunit;

using TruckManager.Common.Results;
using TruckManager.Domain.Aggregates.Trucks;
using TruckManager.Domain.Enums;
using TruckManager.Domain.Events.Trucks;
using TruckManager.Domain.ValueObjects;
using TruckManager.UnitTests.TestHelpers;

namespace TruckManager.UnitTests.Domain.Tests.Aggregates.Trucks;

public class TruckDeleteTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 13, 13, 37, 0, TimeSpan.Zero);

    [Fact]
    public void Delete_marks_truck_deleted_and_raises_TruckDeleted_event()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Truck truck = TruckTestFactory.NewValid(clock);
        ConcurrencyStamp stampBefore = truck.ConcurrencyStamp;

        //Act
        clock.Advance(TimeSpan.FromMinutes(5));
        Guid actor = Guid.NewGuid();

        Result result = truck.Delete(clock, actor);

        //Assert
        result.IsSuccess.Should().BeTrue();
        truck.IsDeleted.Should().BeTrue();
        truck.DeletedByUserId.Should().Be(actor);
        truck.DeletedAtUtc.Should().NotBeNull();
        truck.ConcurrencyStamp.Version.Should().Be(stampBefore.Version + 1);

        TruckDeleted evt = truck.DomainEvents.OfType<TruckDeleted>().Single();
        evt.PerformedByUserId.Should().Be(actor);
        evt.AggregateVersion.Should().Be(truck.ConcurrencyStamp.Version);
    }

    [Fact]
    public void Second_Delete_on_already_deleted_truck_returns_Conflict()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Truck truck = TruckTestFactory.NewValid(clock);

        //Act
        truck.Delete(clock, Guid.NewGuid());
        ConcurrencyStamp stampBefore = truck.ConcurrencyStamp;
        int eventCountBefore = truck.DomainEvents.Count;

        Result result = truck.Delete(clock, Guid.NewGuid());

        //Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle()
                              .Which.Type
                              .Should().Be(EErrorType.Conflict);
        truck.ConcurrencyStamp.Should().Be(stampBefore);
        truck.DomainEvents.Should().HaveCount(eventCountBefore);
    }

    [Fact]
    public void Update_on_deleted_truck_is_refused()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Truck truck = TruckTestFactory.NewValid(clock);

        //Act
        truck.Delete(clock, Guid.NewGuid());
        TruckName newName = TruckName.Create("New").Value!;
        Result result = truck.Update(new TruckUpdates(Name: newName), clock, Guid.NewGuid());

        //Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle()
                              .Which.Type
                              .Should().Be(EErrorType.Conflict);
    }

    [Fact]
    public void ChangeStatus_on_deleted_truck_is_refused()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Truck truck = TruckTestFactory.NewValid(clock);

        //Act
        truck.Delete(clock, Guid.NewGuid());
        FakeTruckStatusTransitionPolicy policy = FakeTruckStatusTransitionPolicy.AllowEverything();
        Result result = truck.ChangeStatus(ETruckStatus.Loading, policy, clock, Guid.NewGuid());

        //Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle()
                              .Which.Type
                              .Should().Be(EErrorType.Conflict);
    }
}

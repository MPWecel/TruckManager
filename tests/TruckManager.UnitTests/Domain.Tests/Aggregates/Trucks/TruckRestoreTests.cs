using AwesomeAssertions;
using Xunit;

using TruckManager.Common.Results;
using TruckManager.Domain.Aggregates.Trucks;
using TruckManager.Domain.Enums;
using TruckManager.Domain.Events.Trucks;
using TruckManager.Domain.ValueObjects;
using TruckManager.UnitTests.TestHelpers;

namespace TruckManager.UnitTests.Domain.Tests.Aggregates.Trucks;

public class TruckRestoreTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 13, 13, 37, 0, TimeSpan.Zero);

    [Fact]
    public void Restore_on_deleted_truck_clears_delete_fields_and_raises_TruckRestored()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Truck truck = TruckTestFactory.NewValid(clock);

        //Act
        truck.Delete(clock, Guid.NewGuid());
        ConcurrencyStamp stampBeforeRestore = truck.ConcurrencyStamp;
        Guid restorer = Guid.NewGuid();

        clock.Advance(TimeSpan.FromMinutes(5));

        Result result = truck.Restore(clock, restorer);

        //Assert
        result.IsSuccess.Should().BeTrue();
        truck.IsDeleted.Should().BeFalse();
        truck.DeletedAtUtc.Should().BeNull();
        truck.DeletedByUserId.Should().BeNull();
        truck.ConcurrencyStamp.Version.Should().Be(stampBeforeRestore.Version + 1);

        TruckRestored evt = truck.DomainEvents.OfType<TruckRestored>().Single();
        evt.PerformedByUserId.Should().Be(restorer);
        evt.AggregateVersion.Should().Be(truck.ConcurrencyStamp.Version);
    }

    [Fact]
    public void Restore_on_non_deleted_truck_returns_Conflict_failure()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Truck truck = TruckTestFactory.NewValid(clock);
        ConcurrencyStamp stampBefore = truck.ConcurrencyStamp;
        int eventCountBefore = truck.DomainEvents.Count;

        //Act
        Result result = truck.Restore(clock, Guid.NewGuid());

        //Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle()
                              .Which.Type
                              .Should().Be(EErrorType.Conflict);
        truck.ConcurrencyStamp.Should().Be(stampBefore);
        truck.DomainEvents.Should().HaveCount(eventCountBefore);
    }

    [Fact]
    public void Mutations_work_again_after_Restore()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Truck truck = TruckTestFactory.NewValid(clock, initialStatus: ETruckStatus.Loading);
        FakeTruckStatusTransitionPolicy policy = FakeTruckStatusTransitionPolicy.DenyAll()
                                                                                .Allow(ETruckStatus.Loading, ETruckStatus.ToJob);

        //Act
        truck.Delete(clock, Guid.NewGuid());
        truck.Restore(clock, Guid.NewGuid());
        TruckName newName = TruckName.Create("Renamed After Restore").Value!;
        Result updateResult = truck.Update(new TruckUpdates(Name: newName), clock, Guid.NewGuid());
        Result statusResult = truck.ChangeStatus(ETruckStatus.ToJob, policy, clock, Guid.NewGuid());

        //Assert
        updateResult.IsSuccess.Should().BeTrue();
        statusResult.IsSuccess.Should().BeTrue();
        truck.Name.Value.Should().Be("Renamed After Restore");
        truck.Status.Should().Be(ETruckStatus.ToJob);
    }

    [Fact]
    public void Second_Restore_on_already_restored_truck_returns_Conflict()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Truck truck = TruckTestFactory.NewValid(clock);

        //Act
        truck.Delete(clock, Guid.NewGuid());
        truck.Restore(clock, Guid.NewGuid());

        Result result = truck.Restore(clock, Guid.NewGuid());

        //Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle()
                              .Which.Type
                              .Should().Be(EErrorType.Conflict);
    }
}

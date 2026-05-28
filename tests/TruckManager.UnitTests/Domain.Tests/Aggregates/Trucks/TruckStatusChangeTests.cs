using AwesomeAssertions;
using Xunit;

using TruckManager.Common.Results;
using TruckManager.Domain.Aggregates.Trucks;
using TruckManager.Domain.Enums;
using TruckManager.Domain.Events.Trucks;
using TruckManager.Domain.ValueObjects;
using TruckManager.UnitTests.TestHelpers;

namespace TruckManager.UnitTests.Domain.Tests.Aggregates.Trucks;

public class TruckStatusChangeTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 13, 13, 37, 0, TimeSpan.Zero);

    [Fact]
    public void Allowed_transition_succeeds_and_raises_TruckStatusChanged()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Truck truck = TruckTestFactory.NewValid(clock, initialStatus: ETruckStatus.Loading);
        FakeTruckStatusTransitionPolicy policy = FakeTruckStatusTransitionPolicy.DenyAll()
                                                                                .Allow(ETruckStatus.Loading, ETruckStatus.ToJob);
        ConcurrencyStamp stampBefore = truck.ConcurrencyStamp;

        //Act
        Result result = truck.ChangeStatus(ETruckStatus.ToJob, policy, clock, Guid.NewGuid());

        //Assert
        result.IsSuccess.Should().BeTrue();
        truck.Status.Should().Be(ETruckStatus.ToJob);
        truck.ConcurrencyStamp.Version.Should().Be(stampBefore.Version + 1);

        TruckStatusChanged evt = truck.DomainEvents.OfType<TruckStatusChanged>().Single();
        evt.FromStatus.Should().Be(ETruckStatus.Loading);
        evt.ToStatus.Should().Be(ETruckStatus.ToJob);
        evt.AggregateVersion.Should().Be(truck.ConcurrencyStamp.Version);
    }

    [Fact]
    public void Disallowed_transition_returns_Validation_failure_and_does_not_mutate()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Truck truck = TruckTestFactory.NewValid(clock, initialStatus: ETruckStatus.Loading);
        FakeTruckStatusTransitionPolicy policy = FakeTruckStatusTransitionPolicy.DenyAll();
        ConcurrencyStamp stampBefore = truck.ConcurrencyStamp;
        int eventCountBefore = truck.DomainEvents.Count;

        //Act
        Result result = truck.ChangeStatus(ETruckStatus.AtJob, policy, clock, Guid.NewGuid());

        //Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle()
                              .Which.Type
                              .Should().Be(EErrorType.Validation);
        truck.Status.Should().Be(ETruckStatus.Loading);
        truck.ConcurrencyStamp.Should().Be(stampBefore);
        truck.DomainEvents.Should().HaveCount(eventCountBefore);
    }

    [Fact]
    public void Same_status_change_is_a_no_op_without_consulting_policy()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Truck truck = TruckTestFactory.NewValid(clock, initialStatus: ETruckStatus.Loading);
        // Even a deny-all policy should not be consulted for X -> X.
        FakeTruckStatusTransitionPolicy policy = FakeTruckStatusTransitionPolicy.DenyAll();
        ConcurrencyStamp stampBefore = truck.ConcurrencyStamp;
        int eventCountBefore = truck.DomainEvents.Count;

        //Act
        Result result = truck.ChangeStatus(ETruckStatus.Loading, policy, clock, Guid.NewGuid());

        //Assert
        result.IsSuccess.Should().BeTrue();
        truck.ConcurrencyStamp.Should().Be(stampBefore);
        truck.DomainEvents.Should().HaveCount(eventCountBefore);
    }

    [Fact]
    public void ChangeStatus_on_deleted_truck_returns_Conflict_failure()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Truck truck = TruckTestFactory.NewValid(clock, initialStatus: ETruckStatus.OutOfService);
        truck.Delete(clock, Guid.NewGuid());
        FakeTruckStatusTransitionPolicy policy = FakeTruckStatusTransitionPolicy.AllowEverything();

        //Act
        Result result = truck.ChangeStatus(ETruckStatus.Loading, policy, clock, Guid.NewGuid());

        //Assert
        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle()
                              .Which.Type
                              .Should().Be(EErrorType.Conflict);
    }

    [Fact]
    public void Default_workflow_allows_LOADING_to_TOJOB()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Truck truck = TruckTestFactory.NewValid(clock, initialStatus: ETruckStatus.Loading);
        FakeTruckStatusTransitionPolicy policy = FakeTruckStatusTransitionPolicy.WithDefaultWorkflow();

        //Act
        Result result = truck.ChangeStatus(ETruckStatus.ToJob, policy, clock, Guid.NewGuid());

        //Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Default_workflow_forbids_LOADING_to_RETURNING()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Truck truck = TruckTestFactory.NewValid(clock, initialStatus: ETruckStatus.Loading);
        FakeTruckStatusTransitionPolicy policy = FakeTruckStatusTransitionPolicy.WithDefaultWorkflow();

        //Act
        Result result = truck.ChangeStatus(ETruckStatus.Returning, policy, clock, Guid.NewGuid());

        //Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Default_workflow_allows_OUT_OF_SERVICE_to_any_status()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        FakeTruckStatusTransitionPolicy policy = FakeTruckStatusTransitionPolicy.WithDefaultWorkflow();

        foreach (ETruckStatus target in Enum.GetValues<ETruckStatus>())
        {
            if (target == ETruckStatus.OutOfService)
                continue;

            //Act
            Truck truck = TruckTestFactory.NewValid(clock, initialStatus: ETruckStatus.OutOfService);
            Result result = truck.ChangeStatus(target, policy, clock, Guid.NewGuid());

            //Assert
            result.IsSuccess.Should().BeTrue(because: $"OutOfService -> {target} must be allowed");
        }
    }

    // ---- Phase 8 / Section G gap-fill ------------------------------------------------

    [Fact]
    public void ChangeStatus_throws_ArgumentNullException_when_policy_is_null()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Truck truck = TruckTestFactory.NewValid(clock, initialStatus: ETruckStatus.Loading);

        //Act
        Action act = () => truck.ChangeStatus(ETruckStatus.ToJob, null!, clock, Guid.NewGuid());

        //Assert
        act.Should().Throw<ArgumentNullException>()
                    .Which.ParamName.Should().Be("policy");
    }

    [Fact]
    public void ChangeStatus_throws_ArgumentNullException_when_clock_is_null()
    {
        //Arrange
        FakeDateTimeProvider clock = new(T0);
        Truck truck = TruckTestFactory.NewValid(clock, initialStatus: ETruckStatus.Loading);
        FakeTruckStatusTransitionPolicy policy = FakeTruckStatusTransitionPolicy.AllowEverything();

        //Act
        Action act = () => truck.ChangeStatus(ETruckStatus.ToJob, policy, null!, Guid.NewGuid());

        //Assert
        act.Should().Throw<ArgumentNullException>()
                    .Which.ParamName.Should().Be("clock");
    }
}

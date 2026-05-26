using AwesomeAssertions;

using TruckManager.Domain.Enums;
using TruckManager.Infrastructure.Persistence.Entities;
using TruckManager.Infrastructure.Workflows;

using Xunit;

namespace TruckManager.IntegrationTests.Workflows;

// Pure-logic coverage for the policy's HashSet behaviour (no DB needed). The seeded-DB
// round-trip (Section G's TruckStatusTransitionPolicyTests-with-Testcontainers) is a
// separate, broader test that ALSO exercises the StatusBijectionHealthCheck startup
// path. Lives in IntegrationTests because the production class is Infrastructure code.
public sealed class TruckStatusTransitionPolicyTests
{
    [Fact]
    public void IsAllowed_throws_when_LoadFrom_has_not_been_called()
    {
        TruckStatusTransitionPolicy sut = new();

        Action act = () => sut.IsAllowed(ETruckStatus.Loading, ETruckStatus.ToJob);

        act.Should().Throw<InvalidOperationException>()
                    .WithMessage("*has not been initialized*");
    }

    [Fact]
    public void LoadFrom_called_twice_throws()
    {
        TruckStatusTransitionPolicy sut = new();
        sut.LoadFrom(WorkflowSeed());

        Action act = () => sut.LoadFrom(WorkflowSeed());

        act.Should().Throw<InvalidOperationException>()
                    .WithMessage("*already initialized*");
    }

    [Fact]
    public void IsInitialized_flips_after_LoadFrom()
    {
        TruckStatusTransitionPolicy sut = new();
        sut.IsInitialized.Should().BeFalse();

        sut.LoadFrom(WorkflowSeed());

        sut.IsInitialized.Should().BeTrue();
    }

    [Theory]
    [InlineData(ETruckStatus.Loading,      ETruckStatus.ToJob,     true)]
    [InlineData(ETruckStatus.ToJob,        ETruckStatus.AtJob,     true)]
    [InlineData(ETruckStatus.AtJob,        ETruckStatus.Returning, true)]
    [InlineData(ETruckStatus.Returning,    ETruckStatus.Loading,   true)]
    [InlineData(ETruckStatus.OutOfService, ETruckStatus.Loading,   true)]
    [InlineData(ETruckStatus.Loading,      ETruckStatus.OutOfService, true)]
    [InlineData(ETruckStatus.Loading,      ETruckStatus.AtJob,     false)]  // skips ToJob
    [InlineData(ETruckStatus.Returning,    ETruckStatus.ToJob,     false)]  // wrong direction
    [InlineData(ETruckStatus.Loading,      ETruckStatus.Loading,   false)]  // same status not seeded
    public void IsAllowed_matches_seeded_workflow(ETruckStatus from, ETruckStatus to, bool expected)
    {
        TruckStatusTransitionPolicy sut = new();
        sut.LoadFrom(WorkflowSeed());

        sut.IsAllowed(from, to).Should().Be(expected);
    }

    [Fact]
    public void LoadFrom_excludes_rows_with_IsAllowed_false()
    {
        TruckStatusTransitionPolicy sut = new();

        // A denied row in the dictionary must not become an allowed entry in the HashSet.
        sut.LoadFrom(
            [
                new TruckStatusTransition(1, (int)ETruckStatus.Loading, (int)ETruckStatus.ToJob,     IsAllowed: true),
                new TruckStatusTransition(2, (int)ETruckStatus.AtJob,   (int)ETruckStatus.Loading,   IsAllowed: false),
            ]
        );

        sut.IsAllowed(ETruckStatus.Loading, ETruckStatus.ToJob).Should().BeTrue();
        sut.IsAllowed(ETruckStatus.AtJob,   ETruckStatus.Loading).Should().BeFalse();
    }

    // Mirrors TruckStatusTransitionConfiguration.BuildSeed (Section A).
    private static TruckStatusTransition[] WorkflowSeed()
    {
        int outOfService = (int)ETruckStatus.OutOfService;
        int[] others =
        [
            (int)ETruckStatus.Loading,
            (int)ETruckStatus.ToJob,
            (int)ETruckStatus.AtJob,
            (int)ETruckStatus.Returning,
        ];

        List<TruckStatusTransition> rows = new(capacity: others.Length * 2 + 4);
        int id = 1;

        foreach (int other in others)
        {
            rows.Add(new TruckStatusTransition(id++, outOfService, other,        IsAllowed: true));
            rows.Add(new TruckStatusTransition(id++, other,        outOfService, IsAllowed: true));
        }

        rows.Add(new TruckStatusTransition(id++, (int)ETruckStatus.Loading,   (int)ETruckStatus.ToJob,     IsAllowed: true));
        rows.Add(new TruckStatusTransition(id++, (int)ETruckStatus.ToJob,     (int)ETruckStatus.AtJob,     IsAllowed: true));
        rows.Add(new TruckStatusTransition(id++, (int)ETruckStatus.AtJob,     (int)ETruckStatus.Returning, IsAllowed: true));
        rows.Add(new TruckStatusTransition(id++, (int)ETruckStatus.Returning, (int)ETruckStatus.Loading,   IsAllowed: true));

        return rows.ToArray();
    }
}

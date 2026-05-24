using TruckManager.Domain.Enums;
using TruckManager.Domain.Policies;

namespace TruckManager.UnitTests.TestHelpers;

// In-memory test double for ITruckStatusTransitionPolicy. Backed by a HashSet of allowed (from, to) pairs.
// Use `Allow(...)` to set up specific transitions, or `WithDefaultWorkflow()` for the architecture workflow rules (see design doc).
internal sealed class FakeTruckStatusTransitionPolicy : ITruckStatusTransitionPolicy
{
    private readonly HashSet<(ETruckStatus From, ETruckStatus To)> _allowed = new();

    public FakeTruckStatusTransitionPolicy Allow(ETruckStatus from, ETruckStatus to)
    {
        _allowed.Add((from, to));
        return this;
    }

    public bool IsAllowed(ETruckStatus from, ETruckStatus to) => _allowed.Contains((from, to));

    public static FakeTruckStatusTransitionPolicy DenyAll() => new();

    public static FakeTruckStatusTransitionPolicy AllowEverything()
    {
        FakeTruckStatusTransitionPolicy policy = new();
        foreach (ETruckStatus from in Enum.GetValues<ETruckStatus>())
        {
            foreach (ETruckStatus to in Enum.GetValues<ETruckStatus>())
            {
                policy.Allow(from, to);
            }
        }
        return policy;
    }

    //  Mirrors the workflow rules in architecture in design doc 3.4.
    //      OutOfService    <->     ANY  (bidirectional)
    //      Loading         ->      ToJob
    //      ToJob           ->      AtJob
    //      AtJob           ->      Returning
    //      Returning       ->      Loading
    public static FakeTruckStatusTransitionPolicy WithDefaultWorkflow()
    {
        FakeTruckStatusTransitionPolicy policy = new();

        // OutOfService <-> ANY
        foreach (ETruckStatus other in Enum.GetValues<ETruckStatus>())
        {
            if (other == ETruckStatus.OutOfService) 
                continue;

            policy.Allow(ETruckStatus.OutOfService, other);
            policy.Allow(other, ETruckStatus.OutOfService);
        }

        // Forward workflow.
        policy.Allow(ETruckStatus.Loading, ETruckStatus.ToJob);
        policy.Allow(ETruckStatus.ToJob, ETruckStatus.AtJob);
        policy.Allow(ETruckStatus.AtJob, ETruckStatus.Returning);
        policy.Allow(ETruckStatus.Returning, ETruckStatus.Loading);

        return policy;
    }
}

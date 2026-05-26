using TruckManager.Domain.Enums;
using TruckManager.Domain.Policies;
using TruckManager.Infrastructure.Persistence.Entities;

namespace TruckManager.Infrastructure.Workflows;

// [ADR-0027]   Infrastructure-side implementation of ITruckStatusTransitionPolicy.
// Backed by a HashSet<(from, to)> loaded once at startup from the TruckStatusTransitions
// dictionary (via StatusBijectionHealthCheck). V1 has no admin-mutated dictionary, so
// load-once-no-invalidation is sufficient. Registered as a singleton (Section F).
//
// Thread-safety: LoadFrom must be called exactly once at startup before any IsAllowed
// reader. The lock guards against accidental double-initialization; IsAllowed itself is
// lock-free because HashSet reads are safe once the set is fully populated and no
// further writes occur.
public sealed class TruckStatusTransitionPolicy : ITruckStatusTransitionPolicy
{
    private readonly object _initLock = new();
    private HashSet<(ETruckStatus From, ETruckStatus To)>? _allowed;

    public bool IsInitialized => _allowed is not null;

    public void LoadFrom(IEnumerable<TruckStatusTransition> transitions)
    {
        ArgumentNullException.ThrowIfNull(transitions);

        lock (_initLock)
        {
            if (_allowed is not null)
                throw new InvalidOperationException(
                    "TruckStatusTransitionPolicy is already initialized; load-once semantics per ADR-0027."
                );

            HashSet<(ETruckStatus, ETruckStatus)> set = new();
            foreach (TruckStatusTransition row in transitions)
            {
                if (!row.IsAllowed)
                    continue;

                set.Add(((ETruckStatus)row.FromStatusId, (ETruckStatus)row.ToStatusId));
            }

            _allowed = set;
        }
    }

    public bool IsAllowed(ETruckStatus from, ETruckStatus to)
    {
        HashSet<(ETruckStatus From, ETruckStatus To)>? allowed = _allowed;

        if (allowed is null)
            throw new InvalidOperationException(
                "TruckStatusTransitionPolicy has not been initialized. The StatusBijectionHealthCheck hosted service calls LoadFrom(...) at startup; ensure it ran before any aggregate consumes the policy."
            );

        return allowed.Contains((from, to));
    }
}

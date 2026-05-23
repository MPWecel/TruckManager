using TruckManager.Domain.Enums;

namespace TruckManager.Domain.Policies;

// [ADR-0027]   Validates Truck status workflow transitions. Interface lives in Domain;
// the concrete implementation lives in Infrastructure and is backed by the
// TruckStatusTransitions dictionary table + an in-memory cache (load-once-at-startup
// HashSet<(ETruckStatus, ETruckStatus)>, no invalidation in V1).
//
// The aggregate consumes this via its mutating instance methods (Truck.ChangeStatus)
// — per ADR-0010 the aggregate never queries the DB directly; the handler hands the
// policy in.
public interface ITruckStatusTransitionPolicy
{
    bool IsAllowed(ETruckStatus from, ETruckStatus to);
}

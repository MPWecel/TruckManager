namespace TruckManager.Domain.Enums;

// [ADR-0025]   Workflow source of truth for the Truck aggregate.
// Numeric values are explicit and aligned with the TruckStatuses dictionary IDs
// in Phase 4 (Persistence). NEVER renumber or remove members — mark with [Obsolete]
// and set the dictionary row IsActive = false instead. The Phase 4 startup health-check
// asserts bijection between this enum and the dictionary rows.
//
// Members appear in workflow sequence:
//   OutOfService  ↔  ANY  (bidirectional with any status)
//   Loading       →  ToJob
//   ToJob         →  AtJob
//   AtJob         →  Returning
//   Returning     →  Loading
//
// Allowed transitions are validated by ITruckStatusTransitionPolicy (interface in Domain,
// implementation in Infrastructure per ADR-0027).
public enum ETruckStatus : int
{
    OutOfService = 1,
    Loading      = 2,
    ToJob        = 3,
    AtJob        = 4,
    Returning    = 5,
}

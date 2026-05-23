namespace TruckManager.Domain.Enums;

// [ADR-0025]   Workflow source of truth for the Truck aggregate.
// Numeric values are explicit (IMPORTANT) and (TODO) aligned with the TruckStatuses dictionary Ids in Phase 4 (Persistence).
// |IMPORTANT|  NEVER renumber or remove members — mark with [Obsolete] and set the dictionary row IsActive = false instead.
// This is crucial for DB Dictionary table connection. Protected by healthchecks and tests. Source code is source of truth, and DB schema is built and seeded based on it, but when DB is already set up - changes can break mappings. Hence healthchecks and tests.
// The Phase 4 startup health-check asserts bijection between this enum and the dictionary rows.
//
// Members appear in workflow sequence:
//   OutOfService   <->  ANY  (bidirectional with any status)
//   Loading        ->   ToJob
//   ToJob          ->   AtJob
//   AtJob          ->   Returning
//   Returning      ->   Loading
//
// [ADR-0027]   Allowed transitions are validated by ITruckStatusTransitionPolicy (interface in Domain, implementation in Infrastructure).
public enum ETruckStatus : int
{
    OutOfService = 1,
    Loading      = 2,
    ToJob        = 3,
    AtJob        = 4,
    Returning    = 5,
}

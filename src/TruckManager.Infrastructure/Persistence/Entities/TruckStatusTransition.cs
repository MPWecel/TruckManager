namespace TruckManager.Infrastructure.Persistence.Entities;

// [ADR-0007 / ADR-0027]   Dictionary row defining an allowed (or denied) workflow step
// between two TruckStatuses. The Phase 4 startup health-check verifies that every
// FromStatusId / ToStatusId points at an existing TruckStatuses row and therefore at a
// known ETruckStatus member.
//
// V1 seed contains only IsAllowed = true rows (forbidden transitions are modelled by
// absence). The IsAllowed column is kept so future fine-grained policies can express
// explicit denials without migration churn.
public sealed record TruckStatusTransition(
                                              int  Id,
                                              int  FromStatusId,
                                              int  ToStatusId,
                                              bool IsAllowed
                                          );

using TruckManager.Domain.Enums;
using TruckManager.Domain.ValueObjects;

namespace TruckManager.Domain.Events.Trucks;

// [ADR-0024]   Raised by Truck.ChangeStatus on an allowed transition (per ITruckStatusTransitionPolicy).
// Carries both endpoints so subscribers can filter/replay on either side of the transition.
public sealed record TruckStatusChanged(
                                           Guid EventId,
                                           Guid AggregateId,
                                           ulong AggregateVersion,
                                           DateTimeOffset OccurredAtUtc,
                                           Guid? PerformedByUserId,
                                           TenantId TenantId,
                                           Guid? CorrelationId,
                                           Guid? CausationId,
                                           ETruckStatus FromStatus,
                                           ETruckStatus ToStatus
                                       ) : DomainEvent(
                                                          EventId,
                                                          AggregateId,
                                                          AggregateVersion,
                                                          OccurredAtUtc,
                                                          PerformedByUserId,
                                                          TenantId,
                                                          CorrelationId,
                                                          CausationId
                                                      );

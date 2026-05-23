using TruckManager.Domain.ValueObjects;

namespace TruckManager.Domain.Events.Trucks;

// [ADR-0009], [ADR-0024]   Raised by Truck.Delete (soft delete).
// No extra payload — the PerformedByUserId + OccurredAtUtc base fields plus the AggregateId identify who did what when.
// The DeletedAt / DeletedBy audit fields on the aggregate row carry the same info for query-time access.
public sealed record TruckDeleted(
                                     Guid EventId,
                                     Guid AggregateId,
                                     ulong AggregateVersion,
                                     DateTimeOffset OccurredAtUtc,
                                     Guid? PerformedByUserId,
                                     TenantId TenantId,
                                     Guid? CorrelationId,
                                     Guid? CausationId
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

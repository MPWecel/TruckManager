using TruckManager.Domain.ValueObjects;

namespace TruckManager.Domain.Events.Trucks;

// [ADR-0024]   Raised by Truck.Restore on a previously soft-deleted truck.
// No extra payload - the act of restoring is itself the audit signal.
// After this event, the truck is fully mutable again (audit fields show the restore, ConcurrencyStamp increments once).
public sealed record TruckRestored(
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

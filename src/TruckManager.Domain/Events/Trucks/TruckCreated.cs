using TruckManager.Domain.Enums;
using TruckManager.Domain.ValueObjects;

namespace TruckManager.Domain.Events.Trucks;

// [ADR-0024]   Raised by Truck.Create when a new Truck aggregate is born.
// Payload is the full initial snapshot (Code, Name, Description, Status) — replay-self-sufficient (see design doc 12.4).
// This event is what a future projection would consume to reconstruct the initial state of a Truck without joining back to the current-state table.
public sealed record TruckCreated(
                                     Guid EventId,
                                     Guid AggregateId,
                                     ulong AggregateVersion,
                                     DateTimeOffset OccurredAtUtc,
                                     Guid? PerformedByUserId,
                                     TenantId TenantId,
                                     Guid? CorrelationId,
                                     Guid? CausationId,
                                     TruckCode Code,
                                     TruckName Name,
                                     TruckDescription Description,
                                     ETruckStatus Status
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

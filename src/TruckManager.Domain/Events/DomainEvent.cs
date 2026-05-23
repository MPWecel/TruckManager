using TruckManager.Domain.ValueObjects;

namespace TruckManager.Domain.Events;

// [ADR-0024]   Base abstract record for all domain events
// Concrete events derive and forward these fields via positional constructor, while also adding their own payload
// Field set reflects the TruckDomainEvents table in database
public abstract record DomainEvent(
                                      Guid EventId,
                                      Guid AggregateId,
                                      ulong AggregateVersion,
                                      DateTimeOffset OccurredAtUtc,
                                      Guid? PerformedByUserId,
                                      TenantId TenantId,
                                      Guid? CorrelationId,
                                      Guid? CausationId
                                  );

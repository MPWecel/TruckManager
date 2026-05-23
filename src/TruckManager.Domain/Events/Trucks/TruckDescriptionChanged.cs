using TruckManager.Domain.ValueObjects;

namespace TruckManager.Domain.Events.Trucks;

// [ADR-0024, ADR-0026]   Raised by Truck.Update when Description actually changed.
// May be raised alongside TruckRenamed from the same Update call — both events share the same AggregateVersion and are persisted in one transaction (single stamp increment per call).
public sealed record TruckDescriptionChanged(
                                                Guid EventId,
                                                Guid AggregateId,
                                                ulong AggregateVersion,
                                                DateTimeOffset OccurredAtUtc,
                                                Guid? PerformedByUserId,
                                                TenantId TenantId,
                                                Guid? CorrelationId,
                                                Guid? CausationId,
                                                TruckDescription OldDescription,
                                                TruckDescription NewDescription
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

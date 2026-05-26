namespace TruckManager.Infrastructure.Persistence.Entities;

// [ADR-0003 / ADR-0030]   Append-only row in `TruckDomainEvents`. Materialised by the
// DomainEventPersistenceInterceptor (Section C) from the in-memory event queue on each
// aggregate, in the same transaction as the state change. PayloadJson is the
// authoritative event payload (full record serialised per ADR-0030); the relational
// columns mirror the base DomainEvent fields for cheap querying.
//
// AggregateVersion is stored as `long` (Postgres `bigint`); the Domain side uses `ulong`
// (ConcurrencyStamp.Version). The cast is safe — Version is 1-based and increments by 1
// per mutation, so overflow into long-negative would take 2^63 mutations on one truck.
//
// TenantId is stored as a raw Guid here; the strongly-typed TenantId VO lives only on the
// Domain side and is unwrapped by the interceptor when constructing rows.
public sealed record TruckDomainEvent(
                                         Guid           EventId,
                                         Guid           AggregateId,
                                         long           AggregateVersion,
                                         string         EventType,
                                         DateTimeOffset OccurredAtUtc,
                                         Guid?          PerformedByUserId,
                                         Guid           TenantId,
                                         Guid?          CorrelationId,
                                         Guid?          CausationId,
                                         string         PayloadJson
                                     );

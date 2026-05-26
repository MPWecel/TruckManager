using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using TruckManager.Domain.Common;
using TruckManager.Infrastructure.Persistence.Entities;
using TruckManager.Infrastructure.Persistence.Serialization;

namespace TruckManager.Infrastructure.Persistence.Interceptors;

// [ADR-0003 / ADR-0032]   Walks every tracked IAggregateRoot just before SaveChanges,
// serialises each pending domain event via IDomainEventSerializer, adds the resulting
// TruckDomainEvent rows to the DbContext, then drains each aggregate's queue. All of
// this happens inside the same DB transaction EF Core opens around SaveChanges, so the
// state + events writes are atomic by construction (the foundation of ADR-0003).
//
// Order discipline: serialise + enqueue ALL rows first, then clear queues in a single
// second pass. A mid-loop exception leaves aggregates with their events still queued,
// not a half-cleared state (the transaction rolls back; aggregates stay consistent).
//
// V1 scope: Truck is the only aggregate, so every event maps to a TruckDomainEvent row.
// When future modules add their own aggregates (JobDomainEvent, etc.) this interceptor
// will need a routing step — flagged in next-steps.md / phases.md when Phase X lands.
public sealed class DomainEventPersistenceInterceptor : SaveChangesInterceptor
{
    private readonly IDomainEventSerializer _serializer;

    public DomainEventPersistenceInterceptor(IDomainEventSerializer serializer)
    {
        ArgumentNullException.ThrowIfNull(serializer);
        _serializer = serializer;
    }

    public override InterceptionResult<int> SavingChanges(
                                                              DbContextEventData      eventData,
                                                              InterceptionResult<int> result
                                                          )
    {
        if (eventData.Context is not null)
            PersistAndDrain(eventData.Context);

        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                                                                             DbContextEventData      eventData,
                                                                             InterceptionResult<int> result,
                                                                             CancellationToken       cancellationToken = default
                                                                         )
    {
        if (eventData.Context is not null)
            PersistAndDrain(eventData.Context);

        return ValueTask.FromResult(result);
    }

    private void PersistAndDrain(DbContext context)
    {
        List<IAggregateRoot> aggregatesWithEvents = context.ChangeTracker
                                                           .Entries<IAggregateRoot>()
                                                           .Select(e => e.Entity)
                                                           .Where(a => a.DomainEvents.Count > 0)
                                                           .ToList();

        if (aggregatesWithEvents.Count == 0)
            return;

        List<TruckDomainEvent> rows = [];
        foreach (IAggregateRoot aggregate in aggregatesWithEvents)
        {
            foreach (Domain.Events.DomainEvent evt in aggregate.DomainEvents)
            {
                (string eventType, string payloadJson) = _serializer.Serialize(evt);

                rows.Add(
                            new TruckDomainEvent(
                                                    EventId:           evt.EventId,
                                                    AggregateId:       evt.AggregateId,
                                                    AggregateVersion:  (long)evt.AggregateVersion,
                                                    EventType:         eventType,
                                                    OccurredAtUtc:     evt.OccurredAtUtc,
                                                    PerformedByUserId: evt.PerformedByUserId,
                                                    TenantId:          evt.TenantId.Value,
                                                    CorrelationId:     evt.CorrelationId,
                                                    CausationId:       evt.CausationId,
                                                    PayloadJson:       payloadJson
                                                )
                        );
            }
        }

        context.AddRange(rows);

        foreach (IAggregateRoot aggregate in aggregatesWithEvents)
            aggregate.ClearDomainEvents();
    }
}

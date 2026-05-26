using TruckManager.Common.Abstractions;
using TruckManager.Domain.Events;
using TruckManager.Domain.ValueObjects;

namespace TruckManager.Domain.Common;

// [ADR-0024]   Marks aggregate boundary and owns the pending domain-event queue
// [ADR-0003]   Events are raised from instance methods on the concrete aggregate via the protected RaiseDomainEvent helper; the application handler drains and clears them after persisting (in the same transaction).
// [ADR-0032]   Implements the non-generic IAggregateRoot marker so the Infrastructure SaveChangesInterceptor can enumerate aggregates via ChangeTracker.Entries<IAggregateRoot>().
public abstract class AggregateRoot<TId> : AuditableEntity<TId>, IAggregateRoot where TId : IStronglyTypedId<Guid>
{
    private readonly List<DomainEvent> _domainEvents = new();

    public IReadOnlyCollection<DomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected AggregateRoot(
                               TId id,
                               TenantId tenantId,
                               ConcurrencyStamp concurrencyStamp,
                               DateTimeOffset createdAtUtc,
                               Guid createdByUserId
                           ) : base(id, tenantId, concurrencyStamp, createdAtUtc, createdByUserId)
    { }

    // EF Core materialization constructor — see BaseEntity for details. The
    // _domainEvents list initialiser runs for both constructors, so the queue is empty
    // and usable as soon as EF finishes populating an aggregate from the database.
    protected AggregateRoot() : base() { }

    protected void RaiseDomainEvent(DomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}

using TruckManager.Domain.Events;

namespace TruckManager.Domain.Common;

// [ADR-0032]   Non-generic marker so Infrastructure's SaveChangesInterceptor can locate
// aggregates via ChangeTracker.Entries<IAggregateRoot>() without reflecting over the
// closed generic AggregateRoot<TId>. Implemented by AggregateRoot<TId>; no concrete
// aggregate should implement this directly.
public interface IAggregateRoot
{
    IReadOnlyCollection<DomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}

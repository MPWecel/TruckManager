using Microsoft.EntityFrameworkCore;

using TruckManager.Domain.Aggregates.Trucks;

namespace TruckManager.Application.Abstractions.Persistence;

// [ADR-0040]   Application-layer facade over the DbContext.
// Exposes DbSet<T> properties that command / query handlers need, without SaveChangesAsync - that lives inside IUnitOfWork exclusively (see [ADR-0039]) so handlers can never accidentally commit.
//
// V1 exposes only Trucks; TruckStatuses / TruckStatusTransitions / TruckDomainEvents are Infrastructure row-entity types and Phase 5 handlers don't query them directly.
// Extend when a handler genuinely needs one of those sets (and resolve the entity-type placement at that point - those types live in Infrastructure today).
//
// Infrastructure's ApplicationDbContext implements this interface; the same scoped instance is proxied here and to IUnitOfWork so all tracked changes in a request are visible to both.
public interface IApplicationDbContext
{
    DbSet<Truck> Trucks { get; }
}

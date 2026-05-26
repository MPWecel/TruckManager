using TruckManager.Common.Abstractions;
using TruckManager.Domain.ValueObjects;

namespace TruckManager.Domain.Common;

// Abstract Base for all entities.
// Tenant-scoped, optimistically-locked.
// DDD guidelines advise reference-based equality is intentional, therefore class, not record.
public abstract class BaseEntity<TId> where TId : IStronglyTypedId<Guid>
{
    public TId Id { get; protected set; }
    public TenantId TenantId { get; protected set; }
    public ConcurrencyStamp ConcurrencyStamp { get; protected set; }

    protected BaseEntity(TId id, TenantId tenantId, ConcurrencyStamp concurrencyStamp)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(tenantId);
        ArgumentNullException.ThrowIfNull(concurrencyStamp);

        Id = id;
        TenantId = tenantId;
        ConcurrencyStamp = concurrencyStamp;
    }

    // EF Core materialization constructor — NEVER invoke from Domain code. EF reaches it
    // via reflection during load and immediately populates each property via its setter,
    // so the transient null state never escapes the materializer.
    protected BaseEntity()
    {
        Id               = default!;
        TenantId         = default!;
        ConcurrencyStamp = default!;
    }
}

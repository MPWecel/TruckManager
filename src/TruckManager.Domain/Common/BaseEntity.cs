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
}

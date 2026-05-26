using TruckManager.Common.Abstractions;
using TruckManager.Domain.ValueObjects;

namespace TruckManager.Domain.Common;

// Abstraction built on top of BaseEntity
// Adds fields for operational audit and soft-deletes
// UpdatedAtUtc / UpdatedByUserId are non-nullable and initially set to the same values as CreatedAtUtc / CreatedByUserId. Still unsure about that choice, tho.
// [ADR-0031]   Implements IAuditableEntity so the Infrastructure CreatedAuditFillerInterceptor can locate auditable rows without reflection.
public abstract class AuditableEntity<TId> : BaseEntity<TId>, IAuditableEntity where TId : IStronglyTypedId<Guid>
{
    public DateTimeOffset CreatedAtUtc { get; protected set; }
    public Guid CreatedByUserId { get; protected set; }
    public DateTimeOffset UpdatedAtUtc { get; protected set; }
    public Guid UpdatedByUserId { get; protected set; }
    public DateTimeOffset? DeletedAtUtc { get; protected set; }
    public Guid? DeletedByUserId { get; protected set; }
    public bool IsDeleted { get; protected set; }

    protected AuditableEntity(
                                 TId id,
                                 TenantId tenantId,
                                 ConcurrencyStamp concurrencyStamp,
                                 DateTimeOffset createdAtUtc,
                                 Guid createdByUserId
                             ) : base(id, tenantId, concurrencyStamp)
    {
        CreatedAtUtc = createdAtUtc;
        CreatedByUserId = createdByUserId;
        UpdatedAtUtc = createdAtUtc;
        UpdatedByUserId = createdByUserId;
        IsDeleted = false;
        DeletedAtUtc = null;
        DeletedByUserId = null;
    }

    // EF Core materialization constructor — see BaseEntity for details.
    protected AuditableEntity() : base() { }
}

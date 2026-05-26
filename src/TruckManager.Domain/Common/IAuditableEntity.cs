namespace TruckManager.Domain.Common;

// [ADR-0031]   Non-generic marker so Infrastructure's CreatedAuditFillerInterceptor can
// enumerate auditable entities via ChangeTracker.Entries<IAuditableEntity>() without
// reflecting over the closed generic AuditableEntity<TId>. Exposes only the read-only
// Created* fields — the interceptor mutates via EF Core's PropertyEntry API, which
// bypasses CLR access modifiers, so no internal mutator is needed on the interface.
public interface IAuditableEntity
{
    DateTimeOffset CreatedAtUtc { get; }
    Guid CreatedByUserId { get; }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using TruckManager.Application.Abstractions;
using TruckManager.Common.Abstractions;
using TruckManager.Domain.Common;

namespace TruckManager.Infrastructure.Persistence.Interceptors;

// [ADR-0031]   Defense-in-depth filler for the CreatedAt/By audit pair only.
//
// The aggregate's factory (Truck.Create) and ApplyMutation already set the right values
// on the normal write path — this interceptor's job is to catch entities that reach
// SaveChanges with default CreatedAt/By (e.g., data-import code that constructs an
// aggregate via reflection, or a manual seeding script that bypasses the factory).
// On every EntityState.Added IAuditableEntity entry where CreatedAtUtc is default OR
// CreatedByUserId is Guid.Empty, the interceptor fills both Created* and the matching
// Updated* via EF Core's PropertyEntry API (bypasses CLR access modifiers).
//
// Does NOT touch UpdatedAt/By on modified entities — that is aggregate territory.
public sealed class CreatedAuditFillerInterceptor : SaveChangesInterceptor
{
    private readonly IDateTimeProvider   _clock;
    private readonly ICurrentUserService _currentUser;

    public CreatedAuditFillerInterceptor(IDateTimeProvider clock, ICurrentUserService currentUser)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(currentUser);
        _clock       = clock;
        _currentUser = currentUser;
    }

    public override InterceptionResult<int> SavingChanges(
                                                              DbContextEventData      eventData,
                                                              InterceptionResult<int> result
                                                          )
    {
        if (eventData.Context is not null)
            FillMissingCreatedFields(eventData.Context);

        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                                                                             DbContextEventData      eventData,
                                                                             InterceptionResult<int> result,
                                                                             CancellationToken       cancellationToken = default
                                                                         )
    {
        if (eventData.Context is not null)
            FillMissingCreatedFields(eventData.Context);

        return ValueTask.FromResult(result);
    }

    private void FillMissingCreatedFields(DbContext context)
    {
        DateTimeOffset? now    = null;
        Guid?           userId = null;

        foreach (var entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State != EntityState.Added)
                continue;

            IAuditableEntity entity     = entry.Entity;
            bool             needsAt    = entity.CreatedAtUtc == default;
            bool             needsByUid = entity.CreatedByUserId == Guid.Empty;

            if (!needsAt && !needsByUid)
                continue;

            now    ??= _clock.UtcNow;
            userId ??= _currentUser.UserId ?? Guid.Empty;

            if (needsAt)
            {
                entry.Property(nameof(IAuditableEntity.CreatedAtUtc)).CurrentValue = now.Value;
                entry.Property("UpdatedAtUtc").CurrentValue                        = now.Value;
            }

            if (needsByUid)
            {
                entry.Property(nameof(IAuditableEntity.CreatedByUserId)).CurrentValue = userId.Value;
                entry.Property("UpdatedByUserId").CurrentValue                        = userId.Value;
            }
        }
    }
}

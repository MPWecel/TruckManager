using TruckManager.Common.Abstractions;
using TruckManager.Common.Results;
using TruckManager.Domain.Common;
using TruckManager.Domain.Enums;
using TruckManager.Domain.Events.Trucks;
using TruckManager.Domain.Policies;
using TruckManager.Domain.ValueObjects;

namespace TruckManager.Domain.Aggregates.Trucks;

// The Truck aggregate root. Pure-domain — no persistence, no HTTP, no DI.
//
// Mutation contract: validate preconditions -> mutate state -> ApplyMutation (single ConcurrencyStamp + audit bump) -> raise event(s).
// [ADR-0026]   One mutation may raise 0/1/2+ events, all sharing the same AggregateVersion.
// [ADR-0028]   Business failures return Result.Failure (Validation / Conflict); invariant violations (null required arg) throw ArgumentNullException.
//
// Timestamp discipline: each mutating method reads the clock exactly once (via ApplyMutation -> ConcurrencyStamp.Next).
// The returned `now` is reused for audit fields, event OccurredAtUtc, and Guid.CreateVersion7(now) — all timestamps inside a single mutation are identical, no skew under SystemDateTimeProvider.
public sealed class Truck : AggregateRoot<TruckId>
{
    // [design doc 3.3] Code is immutable post-creation in V1 — no method sets it outside the ctor. Phase 8 architecture test will enforce.
    public TruckCode Code { get; private set; }
    public TruckName Name { get; private set; }
    public TruckDescription Description { get; private set; }
    public ETruckStatus Status { get; private set; }

    private Truck(
                     TruckId           id,
                     TenantId          tenantId,
                     ConcurrencyStamp  concurrencyStamp,
                     DateTimeOffset    createdAtUtc,
                     Guid              createdByUserId,
                     TruckCode         code,
                     TruckName         name,
                     TruckDescription  description,
                     ETruckStatus      status
                 ) : base(id, tenantId, concurrencyStamp, createdAtUtc, createdByUserId)
    {
        Code = code;
        Name = name;
        Description = description;
        Status = status;
    }

    // EF Core materialization constructor — see Domain/Common/BaseEntity.cs. NEVER invoke
    // from Domain code; the factory Create(...) is the only public construction path.
    private Truck() : base()
    {
        Code        = default!;
        Name        = default!;
        Description = default!;
        Status      = default;
    }

    // ----------------------------------------------------------------------------------
    // Factory
    // ----------------------------------------------------------------------------------

    public static Result<Truck> Create(
                                          TruckId id, 
                                          TenantId tenantId, 
                                          TruckCode code, 
                                          TruckName name,
                                          TruckDescription description, 
                                          ETruckStatus initialStatus, 
                                          IDateTimeProvider clock, 
                                          Guid createdByUserId,
                                          Guid? correlationId = null
                                      )
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(tenantId);
        ArgumentNullException.ThrowIfNull(code);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(clock);

        ConcurrencyStamp stamp = ConcurrencyStamp.Initial(clock);
        DateTimeOffset now = stamp.LastModifiedUtc;

        Truck truck = new(
                            id: id, 
                            tenantId: tenantId,
                            concurrencyStamp: stamp,
                            createdAtUtc: now,
                            createdByUserId: createdByUserId,
                            code: code,
                            name: name,
                            description: description,
                            status: initialStatus
                         );

        // [decision #8 / design doc §12.4]   Full snapshot payload.
        truck.RaiseDomainEvent(
                                  new TruckCreated(
                                                      EventId: Guid.CreateVersion7(now),
                                                      AggregateId: id.Value,
                                                      AggregateVersion: stamp.Version,
                                                      OccurredAtUtc: now,
                                                      PerformedByUserId: createdByUserId,
                                                      TenantId: tenantId,
                                                      CorrelationId: correlationId,
                                                      CausationId: null,
                                                      Code: code,
                                                      Name: name,
                                                      Description: description,
                                                      Status: initialStatus
                                                  )
                              );

        return Result<Truck>.Success(truck);
    }

    // ----------------------------------------------------------------------------------
    // Mutations
    // ----------------------------------------------------------------------------------

    // [ADR-0026]   Unified update. Raises 0, 1, or 2 events based on what actually
    // changed; all share the same AggregateVersion and one ConcurrencyStamp increment.
    // No-op (no field changed) → Result.Success with NO event and NO stamp increment.
    public Result Update( TruckUpdates updates, IDateTimeProvider clock, Guid updatedByUserId, Guid? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(updates);
        ArgumentNullException.ThrowIfNull(clock);

        if (IsDeleted)
            return Result.Failure(DeletedConflict("update"));

        bool nameChanged = updates.Name is not null && 
                           !Name.Equals(updates.Name);
        bool descChanged = updates.Description is not null && 
                           !Description.Equals(updates.Description);

        if (!nameChanged && !descChanged)
            return Result.Success();

        TruckName oldName = Name;
        TruckDescription oldDescription = Description;

        if (nameChanged) 
            Name = updates.Name!;
        
        if (descChanged) 
            Description = updates.Description!;

        DateTimeOffset now = ApplyMutation(clock, updatedByUserId);
        ulong version = ConcurrencyStamp.Version;

        if (nameChanged) 
            RaiseDomainEvent(
                                new TruckRenamed(
                                                    EventId: Guid.CreateVersion7(now),
                                                    AggregateId: Id.Value,
                                                    AggregateVersion: version,
                                                    OccurredAtUtc: now,
                                                    PerformedByUserId: updatedByUserId,
                                                    TenantId: TenantId,
                                                    CorrelationId: correlationId,
                                                    CausationId: null,
                                                    OldName: oldName,
                                                    NewName: Name
                                                )
                            );
        

        if (descChanged)
            RaiseDomainEvent(
                                new TruckDescriptionChanged(
                                                               EventId: Guid.CreateVersion7(now),
                                                               AggregateId: Id.Value,
                                                               AggregateVersion: version,
                                                               OccurredAtUtc: now,
                                                               PerformedByUserId: updatedByUserId,
                                                               TenantId: TenantId,
                                                               CorrelationId: correlationId,
                                                               CausationId: null,
                                                               OldDescription: oldDescription,
                                                               NewDescription: Description
                                                           )
                            );
        

        return Result.Success();
    }

    public Result ChangeStatus(ETruckStatus newStatus, ITruckStatusTransitionPolicy policy, IDateTimeProvider clock, Guid updatedByUserId, Guid? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(clock);

        if (IsDeleted)
            return Result.Failure(DeletedConflict("change status of"));

        // Same-status is a no-op — consistent with Update. Policy is not consulted for X -> X.
        if (Status == newStatus)
            return Result.Success();

        if (!policy.IsAllowed(Status, newStatus))
            return Result.Failure(
                                     new Error(
                                                  Code: "truck.invalid_transition",
                                                  Message: $"Cannot transition Truck from {Status} to {newStatus}.",
                                                  Type: EErrorType.Validation
                                              )
                                 );


        ETruckStatus oldStatus = Status;
        Status = newStatus;

        DateTimeOffset now = ApplyMutation(clock, updatedByUserId);

        RaiseDomainEvent(
                            new TruckStatusChanged(
                                                      EventId: Guid.CreateVersion7(now),
                                                      AggregateId: Id.Value,
                                                      AggregateVersion: ConcurrencyStamp.Version,
                                                      OccurredAtUtc: now,
                                                      PerformedByUserId: updatedByUserId,
                                                      TenantId: TenantId,
                                                      CorrelationId: correlationId,
                                                      CausationId: null,
                                                      FromStatus: oldStatus,
                                                      ToStatus: newStatus
                                                  )
                        );

        return Result.Success();
    }

    public Result Delete(IDateTimeProvider clock, Guid deletedByUserId, Guid? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (IsDeleted)
            return Result.Failure(
                                    new Error(
                                                 Code: "truck.already_deleted",
                                                 Message: "Truck is already deleted.",
                                                 Type: EErrorType.Conflict
                                             )
                                 );


        IsDeleted = true;
        DeletedByUserId = deletedByUserId;

        DateTimeOffset now = ApplyMutation(clock, deletedByUserId);
        DeletedAtUtc = now;

        RaiseDomainEvent(
                            new TruckDeleted(
                                                EventId: Guid.CreateVersion7(now),
                                                AggregateId: Id.Value,
                                                AggregateVersion: ConcurrencyStamp.Version,
                                                OccurredAtUtc: now,
                                                PerformedByUserId: deletedByUserId,
                                                TenantId: TenantId,
                                                CorrelationId: correlationId,
                                                CausationId: null
                                            )
                        );

        return Result.Success();
    }

    public Result Restore(IDateTimeProvider clock, Guid restoredByUserId, Guid? correlationId = null)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (!IsDeleted)
            return Result.Failure(
                                    new Error(
                                                 Code: "truck.not_deleted",
                                                 Message: "Cannot restore a Truck that is not deleted.",
                                                 Type: EErrorType.Conflict
                                             )
                                 );


        IsDeleted = false;
        DeletedAtUtc = null;
        DeletedByUserId = null;

        DateTimeOffset now = ApplyMutation(clock, restoredByUserId);

        RaiseDomainEvent(
                            new TruckRestored(
                                                 EventId:            Guid.CreateVersion7(now),
                                                 AggregateId:        Id.Value,
                                                 AggregateVersion:   ConcurrencyStamp.Version,
                                                 OccurredAtUtc:      now,
                                                 PerformedByUserId:  restoredByUserId,
                                                 TenantId:           TenantId,
                                                 CorrelationId:      correlationId,
                                                 CausationId:        null
                                             )
                        );

        return Result.Success();
    }

    // ----------------------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------------------

    // Single point that bumps ConcurrencyStamp + audit fields. ApplyMutation is the ONLY way to advance the version inside Truck — guarantees one stamp increment per logical mutation [ADR-0026].
    // Returns the `now` it sourced so callers can reuse it for event timestamps without re-reading the clock.
    private DateTimeOffset ApplyMutation(IDateTimeProvider clock, Guid userId)
    {
        ConcurrencyStamp = ConcurrencyStamp.Next(clock);
        DateTimeOffset now = ConcurrencyStamp.LastModifiedUtc;
        UpdatedAtUtc = now;
        UpdatedByUserId = userId;
        return now;
    }

    private static Error DeletedConflict(string operation) 
        => new(
                  Code: "truck.deleted",
                  Message: $"Cannot {operation} a deleted Truck.",
                  Type: EErrorType.Conflict
              );
}

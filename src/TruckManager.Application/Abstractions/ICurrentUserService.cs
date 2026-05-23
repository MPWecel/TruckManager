namespace TruckManager.Application.Abstractions;

// Authenticated user context: feeds audit fields (CreatedByUserId, UpdatedByUserId, DeletedByUserId), domain-event metadata (PerformedByUserId), and authorisation checks.
// Implementation deferred until Phase 9 for which JWT / claims wiring is scheduled.
public interface ICurrentUserService
{
    // Null on anonymous paths (no auth token, or token missing user-id claim).
    Guid? UserId { get; }

    // Permission-based authorisation. False for anonymous or unauthorised users.
    // Permission strings follow the dotted convention: "truck.read", "truck.write", "truck.status.change", "truck.delete", and so on...
    bool HasPermission(string permission);
}

using TruckManager.Application.Abstractions;

namespace TruckManager.Infrastructure.Auth;

// [ADR-0014]   Phase 4 placeholder implementation of ICurrentUserService.
//
// Phase 9 will replace this with a real JWT-claims-backed reader that pulls the user-id
// claim and the permission set from the current HttpContext. Until then the stub:
//   - returns `null` for UserId — caller code must treat null as "anonymous / system".
//   - returns `false` for every permission — no one is authorised yet.
//
// Lifetime is **scoped** so consumers (e.g., CreatedAuditFillerInterceptor) compose
// correctly with the per-request auth state that Phase 9 will introduce; the stub itself
// is stateless and any lifetime would work for V1.
public sealed class StubCurrentUserService : ICurrentUserService
{
    public Guid? UserId => null;

    public bool HasPermission(string permission) => false;
}

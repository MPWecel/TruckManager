namespace TruckManager.Application.Abstractions;

// Phase 7 / Section B.   Request-scoped correlation identifier.
//
// The CorrelationId is set once at request start by CorrelationMiddleware (Api layer) and read by:
//   > Serilog's LogContext — every Log.X / ILogger.LogX call in the request scope is auto-enriched with `CorrelationId` (plus `TenantId`, `UserId`) via the same middleware push.
//   > Command handlers (Section E) — pass the value to aggregate mutation methods so raised domain events carry the matching CorrelationId in the persisted event row.
//   > GlobalExceptionHandler (Section F) — used as the `traceId` extension on 500 ProblemDetails, so a client-facing 500 correlates to log lines + event rows by a single UUIDv7.
//
// End-to-end invariant after Phase 7: a single HTTP request produces N log lines + M event rows that all share the same CorrelationId.
//
// Mirrors the ICurrentUserService pattern — abstraction in Application, implementation in the layer with the runtime context (here: Infrastructure/Http, with FrameworkReference Microsoft.AspNetCore.App for IHttpContextAccessor).
public interface ICorrelationContext
{
    Guid CorrelationId { get; }
}

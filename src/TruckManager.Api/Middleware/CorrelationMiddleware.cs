using Serilog.Context;

using TruckManager.Common.Abstractions;
using TruckManager.Common.Constants;
using TruckManager.Infrastructure.Http;

namespace TruckManager.Api.Middleware;

// Phase 7 / Section B.   Establishes the per-request correlation identifier and pushes it (plus TenantId and UserId) into Serilog's LogContext for ambient enrichment of every log line in the request scope.
//
// Flow per request:
//   1. Read X-Correlation-Id request header. If it parses to a Guid, use it (cross-service correlation). Else generate a UUIDv7 from the clock (sortable by time, unique by construction).
//   2. Write the id into HttpContext.Items via HttpContextCorrelationContext.Set — that's what handlers, GlobalExceptionHandler, and any other ICorrelationContext consumer reads.
//   3. Schedule a response-header callback (Response.OnStarting) so the client receives the same id back in X-Correlation-Id -
//      — happens just before headers flush, even if the action wrote no body or short-circuited.
//   4. Push CorrelationId + TenantId + UserId into Serilog's LogContext for the rest of the request.
//      Every Log.X / ILogger.LogX call inside the scope gets these properties for free (Enrich.FromLogContext picks them up in Program.cs).
//
// V1 stubs:
//   > TenantId = Tenants.DefaultTenantId (single-tenant per ADR-0008).
//   > UserId   = null                    (anonymous per ICurrentUserService stub).
// Phase 9 will swap these for real claim reads — only this middleware needs to change; the downstream consumers (handlers, sinks, etc.) keep working unchanged because the property names and types are stable.
public sealed class CorrelationMiddleware
{
    public const string CorrelationHeader = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    public CorrelationMiddleware(RequestDelegate next)
    {
        ArgumentNullException.ThrowIfNull(next);
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(clock);

        Guid correlationId = ResolveCorrelationId(context, clock);

        HttpContextCorrelationContext.Set(context, correlationId);

        context.Response.OnStarting(
                                       () =>
                                       {
                                           context.Response.Headers[CorrelationHeader] = correlationId.ToString();
                                           return Task.CompletedTask;
                                       }
                                   );

        // V1 stubs. Pushing the property names (even with null UserId) means logs already carry the
        // schema — when Phase 9 lands, only the source of these values changes; sinks + dashboards
        // do not.
        Guid? userId = null;
        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("TenantId", Tenants.DefaultTenantId))
        using (LogContext.PushProperty("UserId", userId))
        {
            await _next(context);
        }
    }

    private static Guid ResolveCorrelationId(HttpContext context, IDateTimeProvider clock)
    {
        if (context.Request.Headers.TryGetValue(CorrelationHeader, out var headerValues))
        {
            string? raw = headerValues.FirstOrDefault();
            if (Guid.TryParse(raw, out Guid parsed))
                return parsed;
        }

        return Guid.CreateVersion7(clock.UtcNow);
    }
}

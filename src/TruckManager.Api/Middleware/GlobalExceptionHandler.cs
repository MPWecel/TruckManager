using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

using TruckManager.Application.Abstractions;

namespace TruckManager.Api.Middleware;

// Phase 6 / Section B  Catches any exception that escapes the MVC + minimal-API pipelines.
//
// Business failures (Result.Failure) NEVER route through here — they're mapped in-band by ApiResultExtensions (Section C) before reaching the framework.
// This handler exists for genuine bugs and infrastructure faults (DB unreachable, deserialization crashes, invariant violations per ADR-0028, etc.).
//
// Response shape: RFC 7807 ProblemDetails with a generic Detail + a `traceId` extension for correlation with logs.
// The raw exception is INTENTIONALLY not surfaced in the response — Exception.Message can leak stack traces, internal field names, connection strings, or PII.
// The traceId lets ops correlate the client-facing error with the structured log line written below.
//
// Logging: emits LogLevel.Error via Serilog (host-configured by Program.cs since Phase 7 / Section A).
// CorrelationId / TenantId / UserId are auto-enriched via Serilog's LogContext (pushed by CorrelationMiddleware — Phase 7 / Section B), so the LogError call below only needs to supply request-specific properties (Path).
//
// The `traceId` extension on the ProblemDetails response carries the same CorrelationId that the log line
// is enriched with, so clients reporting a 500 can be looked up by a single UUIDv7 in both log lines and
// TruckDomainEvents rows from the same request.
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(IProblemDetailsService problemDetailsService, ILogger<GlobalExceptionHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(problemDetailsService);
        ArgumentNullException.ThrowIfNull(logger);

        _problemDetailsService = problemDetailsService;
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        // Log the raw exception. CorrelationId / TenantId / UserId are auto-enriched via Serilog's LogContext (pushed by CorrelationMiddleware — Phase 7 / Section B) — no explicit args needed.
        // Path stays as an explicit structured property because it's request-specific and not enriched by middleware.
        const string errorMessage = "Unhandled exception caught by GlobalExceptionHandler. Path={Path}";
        _logger.LogError(exception, errorMessage, httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        const string problemTitle = "An unexpected error occurred.";
        const string problemDetail = "An internal error has occurred. If the problem persists, contact support with the traceId from this response.";
        ProblemDetails problemDetails = new()
        {
            Type      = ProblemDetailsTypes.Unexpected,
            Title     = problemTitle,
            Status    = StatusCodes.Status500InternalServerError,
            Detail    = problemDetail,
            Instance  = httpContext.Request.Path,
        };
        // Phase 7 / Section F   The client-facing traceId is the same UUIDv7 CorrelationMiddleware (Section B)
        // pushed into Serilog's LogContext, so a 500 response correlates 1:1 with the log line emitted above
        // and with any TruckDomainEvents.CorrelationId rows from the same request.
        // GlobalExceptionHandler is registered Singleton by AddExceptionHandler<T>(), but ICorrelationContext is
        // Scoped — direct ctor injection would throw at startup. httpContext.RequestServices resolves the
        // scoped instance for this request, which is the idiomatic pattern for Singleton handlers needing
        // per-request services.
        ICorrelationContext correlation = httpContext.RequestServices.GetRequiredService<ICorrelationContext>();
        problemDetails.Extensions["traceId"] = correlation.CorrelationId.ToString();

        ProblemDetailsContext problemContext = new()
        {
            HttpContext     = httpContext,
            ProblemDetails  = problemDetails,
        };

        // Delegates to IProblemDetailsService (registered via AddProblemDetails) so the response carries Content-Type: application/problem+json and any global ProblemDetails customizations are applied.
        // Returns false if no writer accepts the context — in our setup that should never happen, but we propagate the bool so middleware can fall back to the framework default.
        bool result = await _problemDetailsService.TryWriteAsync(problemContext);
        return result;
    }
}

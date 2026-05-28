using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

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
// Logging: emits LogLevel.Error via the framework ILogger so the exception is observable in development even before Serilog wires up.
// Phase 7 adds Serilog enrichment (CorrelationId / CausationId / TenantId / UserId) — the call site here doesn't change.
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

        // Log the raw exception with the traceId so log lines correlate with the client-facing error.
        // [Phase 7: Serilog enrichment adds CorrelationId / CausationId / TenantId / UserId here.]
        const string errorMessage = "Unhandled exception caught by GlobalExceptionHandler. TraceId={TraceId}, Path={Path}";
        _logger.LogError(exception, errorMessage, httpContext.TraceIdentifier, httpContext.Request.Path);

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
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

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

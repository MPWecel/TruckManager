using System.Diagnostics;
using Microsoft.Extensions.Logging;

using TruckManager.Application.Abstractions.Cqrs;
using TruckManager.Common.Results;

namespace TruckManager.Application.Behaviors;

// Phase 7 / Section C.   Outermost pipeline behavior — wraps every command / query in a structured-log scope.
//
// Registered FIRST on both dispatchers in AddTruckManagerApplication so it sees the request both going IN (before Validation / UoW) and coming OUT (after the handler returns, after Commit / Rollback).
// Pairs with CorrelationMiddleware (Section B): every log line this behavior emits is auto-enriched with CorrelationId / TenantId / UserId via Serilog's LogContext (Enrich.FromLogContext wired in Program.cs).
//
// Log shape (all structured properties; no string interpolation):
//   >  Debug        "{RequestType} starting"
//   >  Information  "{RequestType} succeeded in {ElapsedMs}ms"
//   >  Warning      "{RequestType} failed in {ElapsedMs}ms with {ErrorCount} error(s); first error: {ErrorCode}"
//   >  Error        "{RequestType} threw {ExceptionType} after {ElapsedMs}ms"      (exception is re-thrown)
//
// Outcome detection uses the same IResult-marker trick as UnitOfWorkBehavior [ADR-0039], so the behavior stays truly generic over TResult.
// For failure logging we cast to the non-generic Result base to read the Errors collection — both Result and Result<T> inherit from it.
//
// V1: no request-body logging (privacy + log volume; revisit only on concrete debugging need).
public sealed class LoggingBehavior<TRequest, TResult> : IPipelineBehavior<TRequest, TResult>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResult>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResult>> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public async Task<TResult> HandleAsync(TRequest request, Func<Task<TResult>> next, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        string requestTypeName = typeof(TRequest).Name;

        _logger.LogDebug("{RequestType} starting", requestTypeName);

        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            TResult result = await next();
            stopwatch.Stop();

            LogOutcome(requestTypeName, stopwatch.ElapsedMilliseconds, result);
            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            long elapsedMilliseconds = stopwatch.ElapsedMilliseconds;
            string exceptionType = ex.GetType().Name;
            _logger.LogError(
                                ex,
                                "{RequestType} threw {ExceptionType} after {ElapsedMs}ms",
                                requestTypeName,
                                exceptionType,
                                elapsedMilliseconds
                            );
            throw;
        }
    }

    private void LogOutcome(string requestTypeName, long elapsedMs, TResult result)
    {
        if (result is IResult { IsSuccess: true })
        {
            _logger.LogInformation(
                                      "{RequestType} succeeded in {ElapsedMs}ms",
                                      requestTypeName,
                                      elapsedMs
                                  );
            return;
        }

        // Failed Result / Result<T> — both inherit from non-generic Result, which exposes Errors. The Result invariant guarantees Errors.Count > 0 on a failed result, so errors[0] is safe.
        if (result is Result failedResult)
        {
            Error first = failedResult.Errors[0];
            _logger.LogWarning(
                                  "{RequestType} failed in {ElapsedMs}ms with {ErrorCount} error(s); first error: {ErrorCode}",
                                  requestTypeName,
                                  elapsedMs,
                                  failedResult.Errors.Count,
                                  first.Code
                              );
            return;
        }

        // Defensive: a TResult that isn't IResult / Result shouldn't reach the dispatcher (handlers always return Result or Result<T>), but if a future type ever does, log it instead of swallowing silently.
        _logger.LogInformation(
                                  "{RequestType} completed in {ElapsedMs}ms (non-Result return shape)",
                                  requestTypeName,
                                  elapsedMs
                              );
    }
}

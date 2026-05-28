using Microsoft.AspNetCore.Http;

using TruckManager.Application.Abstractions;

namespace TruckManager.Infrastructure.Http;

// Phase 7 / Section B.   ICorrelationContext implementation backed by HttpContext.Items.
//
// Producer: CorrelationMiddleware (Api/Middleware/CorrelationMiddleware.cs) calls Set(httpContext, id) at request entry to write the value.
// Consumer: handlers, GlobalExceptionHandler, etc. inject ICorrelationContext and read the CorrelationId property — which fetches HttpContext via IHttpContextAccessor and reads back the same item.
// The ItemKey is exposed as a public const so producer + consumer share one source — eliminates the "magic string in two places" failure mode.
//
// Errors:
//   >  Accessed outside an HTTP request scope (HttpContext is null) -> InvalidOperationException. Indicates a misuse: handler invoked outside the request pipeline (e.g., from a hosted service).
//   >  Accessed before CorrelationMiddleware ran -> InvalidOperationException with a different message. Indicates the middleware ordering in Program.cs is wrong (must be before the consumer's middleware).
//
// Mirror of the ICurrentUserService pattern — abstraction lives in Application.Abstractions, impl here.
public sealed class HttpContextCorrelationContext : ICorrelationContext
{
    // Key used in HttpContext.Items. Namespaced to avoid collisions with any third-party middleware that might use a bare "CorrelationId" key for its own purposes.
    public const string ItemKey = "TruckManager.CorrelationId";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCorrelationContext(IHttpContextAccessor httpContextAccessor)
    {
        ArgumentNullException.ThrowIfNull(httpContextAccessor);
        _httpContextAccessor = httpContextAccessor;
    }

    // Called by CorrelationMiddleware at request entry. Idempotent — overwrites if called twice.
    public static void Set(HttpContext httpContext, Guid correlationId)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        httpContext.Items[ItemKey] = correlationId;
    }

    public Guid CorrelationId
    {
        get
        {
            HttpContext? httpContext = _httpContextAccessor.HttpContext;
            if (httpContext is null)
            {
                const string httpContextNullExceptionMessage = """
                                                                   ICorrelationContext accessed outside an HTTP request scope.
                                                                   CorrelationMiddleware must run inside the request pipeline; this consumer ran outside it (e.g., from a hosted service or a unit test without proper setup).
                                                               """;
                throw new InvalidOperationException(httpContextNullExceptionMessage);
            }

            if (httpContext.Items.TryGetValue(ItemKey, out object? value) && value is Guid id)
                return id;

            const string noIdFoundExceptionMessage = $"""
                                                          CorrelationId not present in HttpContext.Items["{ItemKey}"]. 
                                                          CorrelationMiddleware must run before this consumer — check Program.cs middleware ordering.
                                                      """;
            throw new InvalidOperationException(noIdFoundExceptionMessage);
        }
    }
}

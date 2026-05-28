namespace TruckManager.Api;

// Phase 6 / Section B.   String constants for the `type` URI on every ProblemDetails response.
// Mapped 1:1 to EErrorType per architecture §14a — the single source of truth consumed by both:
//   >  ApiResultExtensions (Result -> HTTP), landing in Section C, and
//   >  GlobalExceptionHandler (unhandled exceptions -> HTTP), in this section.
// The URI values are placeholders; when the public ProblemDetails type registry is published (Phase 6 exit gate -> api-contracts.md), the base URI is replaced consistently here without touching any call site.
// URIs themselves are part of the API contract — never rename without versioning the API or providing a redirect.
public static class ProblemDetailsTypes
{
    public const string ValidationError = "/problems/validation-error";
    public const string NotFound = "/problems/not-found";
    public const string Conflict = "/problems/conflict";
    public const string ConcurrencyConflict = "/problems/concurrency-conflict";
    public const string Unauthorized = "/problems/unauthorized";
    public const string Forbidden = "/problems/forbidden";
    public const string Unexpected = "/problems/unexpected";
}

using TruckManager.Application.Abstractions;

namespace TruckManager.UnitTests.TestHelpers;

// Phase 7 / Section E test helper.   Stand-in for the production HttpContextCorrelationContext.
//
// Unit-test handlers don't run inside an HTTP request scope, so the production impl (which reads
// HttpContext.Items via IHttpContextAccessor) would throw. This fake returns a stable Guid per
// instance — either auto-generated (default ctor → Guid.NewGuid()) or explicitly supplied via
// WithId(...) for assertion-driven tests that need to compare the value against the raised
// domain event's CorrelationId.
//
// Mirrors the FakeCurrentUserService shape (instance + static factories).
internal sealed class FakeCorrelationContext : ICorrelationContext
{
    public Guid CorrelationId { get; }

    public FakeCorrelationContext()
        => CorrelationId = Guid.NewGuid();

    public FakeCorrelationContext(Guid correlationId)
        => CorrelationId = correlationId;

    public static FakeCorrelationContext Random()             => new();
    public static FakeCorrelationContext WithId(Guid id)      => new(id);
}

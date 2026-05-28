using TruckManager.Application.Abstractions;

namespace TruckManager.IntegrationTests.TestHelpers;

// Phase 7 / Section E test helper.   Stand-in for the production HttpContextCorrelationContext.
//
// Integration-test dispatchers call command handlers without going through CorrelationMiddleware,
// so HttpContext.Items["TruckManager.CorrelationId"] is never set. The production impl would
// throw on first access. PostgresFixture registers this fake as Scoped so each request scope
// (= each test) gets its own stable CorrelationId — tests can resolve ICorrelationContext from
// the scope and assert that the persisted TruckDomainEvents row carries the same Guid.
internal sealed class FakeCorrelationContext : ICorrelationContext
{
    public Guid CorrelationId { get; } = Guid.NewGuid();
}

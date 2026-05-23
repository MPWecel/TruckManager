using TruckManager.Common.Abstractions;

namespace TruckManager.Infrastructure.Time;

// The only allowed direct reference to DateTimeOffset.UtcNow in the entire solution
// ADR-0015:    To be enforced via ArchitectureTesting across the whole solution - deferred until Phase 8
// Resolution:  DependencyInjection of this implementation of IDateTimeProvider
public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

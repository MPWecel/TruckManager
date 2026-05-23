using TruckManager.Common.Abstractions;

namespace TruckManager.Domain.ValueObjects;

// [ADR-0006]   Application-managed concurrency stamp ValueObject. Every aggregate mutation increments Version; Versions are a 1-based index, as rheir goal is to live in database.
public sealed record ConcurrencyStamp(ulong Version, DateTimeOffset LastModifiedUtc)
{
    public static ConcurrencyStamp Initial(IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return new ConcurrencyStamp(1, clock.UtcNow);
    }

    public ConcurrencyStamp Next(IDateTimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);
        return new ConcurrencyStamp(Version + 1, clock.UtcNow);
    }
}

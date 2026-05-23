using TruckManager.Common.Abstractions;

namespace TruckManager.UnitTests.TestHelpers;

// Test-only IDateTimeProvider with an explicit settable(via constructor or public setter) and advancable clock.
// [ADR-0015]   Deterministic - Avoids any reliance on the real clock
internal sealed class FakeDateTimeProvider : IDateTimeProvider
{
    public DateTimeOffset UtcNow { get; set; }

    public FakeDateTimeProvider(DateTimeOffset initialTime) => UtcNow = initialTime;

    public void Advance(TimeSpan span) => UtcNow = UtcNow.Add(span);
}

namespace TruckManager.Common.Abstractions;

// ADR-0015:    never call DateTime.UtcNow / DateTimeOffset.UtcNow directly Inject this instead. Implementations of this interface allow for consistent DateTime generation across the solution
// Potential for extension? Timestamps. Timespans? In such a case - maybe a rename to IClockProvider or something along those lines...
public interface IDateTimeProvider
{
    DateTimeOffset UtcNow { get; }
}

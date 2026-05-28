using Microsoft.Extensions.Logging;

namespace TruckManager.UnitTests.TestHelpers;

// Phase 7 / Section C test helper.   Minimal in-memory ILogger<T> that records every Log call for later inspection.
// Used by LoggingBehaviorTests to assert on log level + rendered message + attached exception without pulling Microsoft.Extensions.Logging.Testing or any other dep.
// Threading: not thread-safe; tests run single-threaded.
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<LogEntry> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull 
        => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
                               LogLevel logLevel,
                               EventId eventId,
                               TState state,
                               Exception? exception,
                               Func<TState, Exception?, string> formatter
                           )
    {
        ArgumentNullException.ThrowIfNull(formatter);

        string message = formatter(state, exception);
        Entries.Add(new LogEntry(logLevel, message, exception));
    }
}

internal sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);

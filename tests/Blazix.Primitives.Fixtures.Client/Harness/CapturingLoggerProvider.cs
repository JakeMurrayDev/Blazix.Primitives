using Microsoft.Extensions.Logging;

namespace Blazix.Primitives.Fixtures.Client.Harness;

/// <summary>
/// Captures log entries of <see cref="LogLevel.Information"/> and above into the session that is rendering, so a
/// test reads the library's development warnings for its own page (ADR 06 §2 "Diagnostics"). Entries written
/// outside any session's dispatcher (host start-up, thread-pool timers) are dropped.
/// </summary>
public sealed class CapturingLoggerProvider(FixtureSessionRegistry registry) : ILoggerProvider
{
    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, registry);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private sealed class CapturingLogger(string category, FixtureSessionRegistry registry) : ILogger
    {
        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel) && registry.TryGetCurrent(out FixtureSession session))
            {
                session.Record(new Diagnostic(logLevel.ToString(), category, formatter(state, exception)));
            }
        }
    }
}

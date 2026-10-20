using Microsoft.Extensions.Logging;

namespace Blazix.Primitives.Tests.TestSupport;

/// <summary>The captured logger of ADR 06 §2 "Diagnostics": records every message written to it, in order.</summary>
public sealed class CapturingLogger : ILogger
{
    private readonly List<(LogLevel Level, string Message)> entries = [];

    public IReadOnlyList<(LogLevel Level, string Message)> Entries => entries;

    public IReadOnlyList<string> Messages => entries.Select(entry => entry.Message).ToArray();

    /// <inheritdoc />
    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        entries.Add((logLevel, formatter(state, exception)));
    }

    public void Clear() => entries.Clear();
}

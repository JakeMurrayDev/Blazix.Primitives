using Microsoft.Extensions.Logging;

namespace Blazix.Primitives.Tests.Components;

/// <summary>The captured logger of ADR 06 §2 "Diagnostics" for one category: records every message written to it, in order.</summary>
/// <typeparam name="TCategory">The type whose name is the logger's category.</typeparam>
public sealed class CapturingLogger<TCategory> : ILogger<TCategory>
{
    private readonly List<(LogLevel Level, string Message)> entries = [];

    public IReadOnlyList<(LogLevel Level, string Message)> Entries => entries;

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
}

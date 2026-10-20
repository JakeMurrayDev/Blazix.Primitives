using System.Text.Json;
using Microsoft.Extensions.Time.Testing;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Fixtures.Client.Harness;

/// <summary>
/// The per-page harness state (ADR 06 §2 as amended 2026-10-20): a scoped service, so one per Server circuit and
/// one per WebAssembly app instance. It owns the fake <see cref="TimeProvider"/> and the captured diagnostics,
/// and is reached from the tests through the token the fixture container renders as <c>data-session</c>:
/// the page hook <c>window.blazixFixture</c> calls the <c>[JSInvokable]</c> methods on the renderer's dispatcher.
/// </summary>
public sealed class FixtureSession : IDisposable
{
    private const int DiagnosticsCapacity = 1000;

    private readonly FixtureSessionRegistry registry;
    private readonly List<Diagnostic> diagnostics = [];
    private readonly Lock gate = new();
    private DotNetObjectReference<FixtureSession>? reference;

    public FixtureSession(FixtureSessionRegistry registry)
    {
        this.registry = registry;
        registry.Add(this);
    }

    public string Token { get; } = Guid.NewGuid().ToString("N");

    /// <summary>The clock every C# timer of this session runs under, reached through <see cref="HarnessTimeProvider"/>.</summary>
    public FakeTimeProvider Clock { get; } = new();

    /// <summary>The object reference the page hook holds; created once.</summary>
    public DotNetObjectReference<FixtureSession> Reference => reference ??= DotNetObjectReference.Create(this);

    public void Record(Diagnostic diagnostic)
    {
        lock (gate)
        {
            if (diagnostics.Count < DiagnosticsCapacity)
            {
                diagnostics.Add(diagnostic);
            }
        }
    }

    public Diagnostic[] Snapshot()
    {
        lock (gate)
        {
            return [.. diagnostics];
        }
    }

    /// <summary>Advances the session clock; timers due fire inline, on the dispatcher this call arrived on.</summary>
    [JSInvokable]
    public void AdvanceTime(double milliseconds)
    {
        Clock.Advance(TimeSpan.FromMilliseconds(milliseconds));
    }

    /// <summary>The captured diagnostics as JSON (the WebAssembly channel; Server tests use the endpoint).</summary>
    [JSInvokable]
    public string DiagnosticsJson()
    {
        return JsonSerializer.Serialize(Snapshot(), FixtureJsonContext.Default.DiagnosticArray);
    }

    public void Dispose()
    {
        registry.Remove(this);
        reference?.Dispose();
    }
}

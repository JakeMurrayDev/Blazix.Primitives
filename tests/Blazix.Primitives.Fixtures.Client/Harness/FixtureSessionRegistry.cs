using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Blazix.Primitives.Fixtures.Client.Harness;

/// <summary>
/// The singleton index of live sessions. Playwright workers run in parallel against one host process, so every
/// per-page channel resolves its session here: the endpoints by token, the logger provider by the renderer's
/// <see cref="SynchronizationContext"/> (Blazor Server runs each circuit's component code on its own; on
/// WebAssembly there is no context and exactly one session).
/// </summary>
public sealed class FixtureSessionRegistry
{
    private readonly ConcurrentDictionary<string, FixtureSession> byToken = new(StringComparer.Ordinal);
    private readonly ConditionalWeakTable<SynchronizationContext, FixtureSession> byContext = [];

    public int Count => byToken.Count;

    public void Add(FixtureSession session)
    {
        byToken[session.Token] = session;
    }

    public void Remove(FixtureSession session)
    {
        byToken.TryRemove(session.Token, out _);
    }

    /// <summary>Called by the fixture container while it renders, so the current dispatcher maps to its session.</summary>
    public void BindCurrentContext(FixtureSession session)
    {
        if (SynchronizationContext.Current is { } context)
        {
            byContext.AddOrUpdate(context, session);
        }
    }

    public bool TryGet(string token, out FixtureSession session)
    {
        return byToken.TryGetValue(token, out session!);
    }

    /// <summary>
    /// The session of the code running right now, when it can be told: on Server by the circuit's dispatcher, on
    /// WebAssembly (single-threaded, no dispatcher context) the app's one session. Never a guess on Server, so
    /// Kestrel and SignalR threads are never attributed to a circuit.
    /// </summary>
    public bool TryGetCurrent(out FixtureSession session)
    {
        if (SynchronizationContext.Current is { } context && byContext.TryGetValue(context, out session!))
        {
            return true;
        }

        if (OperatingSystem.IsBrowser() && byToken.Count == 1)
        {
            session = byToken.Values.First();
            return true;
        }

        session = null!;
        return false;
    }
}

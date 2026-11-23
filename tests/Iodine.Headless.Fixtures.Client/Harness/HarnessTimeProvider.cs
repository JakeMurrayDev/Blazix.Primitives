namespace Blazix.Primitives.Fixtures.Client.Harness;

/// <summary>
/// The <see cref="TimeProvider"/> both hosts register (ADR 06 §2 "Clocks"). It is a singleton because Kestrel and
/// SignalR consume <see cref="TimeProvider"/> from singletons, so the per-circuit fake clock cannot be the DI
/// registration itself. Calls made on a renderer's dispatcher (component code, twin callbacks, <c>[JSInvokable]</c>
/// methods) reach that circuit's <see cref="FixtureSession.Clock"/>; every other caller gets the system clock, so the
/// server's own keep-alive and timeouts keep running on real time.
/// </summary>
public sealed class HarnessTimeProvider(FixtureSessionRegistry registry) : TimeProvider
{
    private TimeProvider Current => registry.TryGetCurrent(out FixtureSession? session) ? session.Clock : System;

    /// <inheritdoc />
    public override TimeZoneInfo LocalTimeZone => Current.LocalTimeZone;

    /// <inheritdoc />
    public override long TimestampFrequency => Current.TimestampFrequency;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => Current.GetUtcNow();

    /// <inheritdoc />
    public override long GetTimestamp() => Current.GetTimestamp();

    /// <inheritdoc />
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        return Current.CreateTimer(callback, state, dueTime, period);
    }
}

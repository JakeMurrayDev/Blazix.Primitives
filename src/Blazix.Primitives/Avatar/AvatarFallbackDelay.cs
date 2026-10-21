namespace Blazix.Primitives;

// AVA-ANI-07/08, AVA-EDGE-05; avatar/fallback/AvatarFallback.tsx:23-35 and the Timeout of UTL-EVT-01..03 (one pending
// timeout, replaced by a restart, cleared on dispose). ADR 01 decision 4 ("macrotask / N ms": a C# timer, since the
// result is state); ADR 06 §1 (TimeProvider, FakeTimeProvider in the tests).
/// <summary>The delay before an avatar fallback may show: a timer under a <see cref="TimeProvider"/> whose expiry is permanent.</summary>
internal sealed class AvatarFallbackDelay : IDisposable
{
    private readonly TimeProvider timeProvider;
    private readonly Action elapsed;
    private ITimer? timer;

    /// <summary>Initializes a new instance of the <see cref="AvatarFallbackDelay"/> class.</summary>
    /// <param name="timeProvider">The clock the timer runs under.</param>
    /// <param name="delay">The delay of the first render, in milliseconds; 0 counts as already elapsed.</param>
    /// <param name="elapsed">Called when a timer expires, on the timer's thread; never called after <see cref="Dispose"/>.</param>
    public AvatarFallbackDelay(TimeProvider timeProvider, double delay, Action elapsed)
    {
        this.timeProvider = timeProvider;
        this.elapsed = elapsed;
        Passed = delay == 0;
    }

    /// <summary>Gets a value indicating whether the delay has elapsed.</summary>
    /// <value><see langword="true"/> once elapsed; it stays <see langword="true"/> for the lifetime of the instance.</value>
    public bool Passed { get; private set; }

    /// <inheritdoc />
    /// <remarks>Cancels the pending timer.</remarks>
    public void Dispose()
    {
        timer?.Dispose();
        timer = null;
    }

    /// <summary>Starts the delay again after a render that mounted the fallback or changed the delay.</summary>
    /// <param name="delay">The delay in milliseconds.</param>
    /// <returns><see langword="true"/> when this call marked the delay as elapsed; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// A positive delay replaces the pending timer, if any; any other value marks the delay as elapsed at once. Once
    /// elapsed, a later positive delay starts no timer.
    /// </remarks>
    public bool Restart(double delay)
    {
        timer?.Dispose();
        timer = null;
        if (Passed)
        {
            return false;
        }

        if (delay > 0)
        {
            timer = timeProvider.CreateTimer(static state => ((AvatarFallbackDelay)state!).elapsed(), this, TimeSpan.FromMilliseconds(delay), Timeout.InfiniteTimeSpan);
            return false;
        }

        Passed = true;
        return true;
    }

    /// <summary>Marks the delay as elapsed, which the owner does on its own thread after the elapsed callback.</summary>
    /// <returns><see langword="true"/> when the delay was not elapsed before; otherwise, <see langword="false"/>.</returns>
    public bool MarkPassed()
    {
        if (Passed)
        {
            return false;
        }

        timer?.Dispose();
        timer = null;
        Passed = true;
        return true;
    }
}

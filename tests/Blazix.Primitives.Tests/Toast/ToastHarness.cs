using Microsoft.Extensions.Time.Testing;

// Family tests use the project's root namespace: a `Toast` namespace would read as a family type.
namespace Blazix.Primitives.Tests;

// The toast store, a manager and one provider link at L1 (ADR 06 §1: C# timers under a FakeTimeProvider; no renderer).
// The link's invoke runs the work inline, as a Provider's InvokeAsync does on its renderer's context.
internal sealed class ToastHarness<TData> : IDisposable
{
    public ToastHarness(double timeout = 5000, int limit = 3, FakeTimeProvider? clock = null, IEnumerable<ToastObject<TData>>? toasts = null, Func<Action, Task>? invoke = null, ToastManager<TData>? manager = null)
    {
        Clock = clock ?? new FakeTimeProvider();
        Store = new ToastStore<TData>(timeout, limit, Clock, invoke, toasts);
        Manager = manager ?? new ToastManager<TData>();
        Link = new ToastProviderLink<TData>(Manager, Store, invoke ?? Inline);
    }

    public FakeTimeProvider Clock { get; }

    public ToastStore<TData> Store { get; }

    public ToastManager<TData> Manager { get; }

    public ToastProviderLink<TData> Link { get; }

    public IReadOnlyList<ToastObject<TData>> Toasts => Store.State.Toasts;

    public void Dispose()
    {
        Link.Dispose();
        Store.Dispose();
    }

    public ToastObject<TData>? Find(string id) => ToastSelectors<TData>.Toast(Store.State, id);

    public bool IsOpen(string id) => Find(id) is { TransitionStatus: not TransitionStatus.Ending };

    public void Advance(int milliseconds) => Clock.Advance(TimeSpan.FromMilliseconds(milliseconds));

    private static Task Inline(Action action)
    {
        action();
        return Task.CompletedTask;
    }
}

// A clock whose wall time can jump without its timers firing, as in a throttled background tab (store.test.ts
// "restarts the full delay when the clock jumped past the timeout before pausing", vi.setSystemTime).
internal sealed class JumpingTimeProvider(FakeTimeProvider inner) : TimeProvider
{
    private long jump;

    public override long TimestampFrequency => inner.TimestampFrequency;

    public void Jump(TimeSpan by) => jump += (long)(by.TotalSeconds * inner.TimestampFrequency);

    public override long GetTimestamp() => inner.GetTimestamp() + jump;

    public override DateTimeOffset GetUtcNow() => inner.GetUtcNow() + TimeSpan.FromSeconds((double)jump / inner.TimestampFrequency);

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
        inner.CreateTimer(callback, state, dueTime, period);
}

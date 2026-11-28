using Iodine.Headless.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Iodine.Headless.Tests.Components.Interop;

// The runtime here is a test double, not interop: the analyzer reads its members as JS interop calls.
#pragma warning disable BL0016

/// <summary>
/// Issue 132 (design point 10-132a (b), chosen by the user 2026-11-28): a repeated attach of an element whose first attach
/// is still in flight completes only after that attach, so a part that signals "attached" afterwards never signals before
/// the module ran; and the options the repeated call carries are sent as one update when they differ from those sent,
/// so a change a part recorded during the first attach reaches the module.
/// </summary>
public sealed class TwinReattachTests
{
    // PR 427's red-check (run 46165562153): every await is bounded, so a title whose awaited step never happens on the base
    // fails with a TimeoutException instead of holding the job until its limit.
    private static readonly TimeSpan s_bound = TimeSpan.FromSeconds(5);

    [Fact(DisplayName = "port: a re-attach of the same element while its attach is in flight completes only after that attach, and sends its changed options as one update")]
    [Trait("Requirement", "ADR04-reattach-in-flight")]
    public async Task A_reattach_in_flight_completes_after_the_attach_and_sends_its_changed_options()
    {
        HeldAttachRuntime runtime = new();
        await using TestTwin twin = new(runtime);
        using IDisposable unblock = runtime.UnblockOnExit();

        Task<TwinHandle<TestTwinOptions>> first = twin.AttachAsync("base-ui-1", new TestTwinOptions { Threshold = 1 }).AsTask();
        Task<TwinHandle<TestTwinOptions>> second = twin.AttachAsync("base-ui-1", new TestTwinOptions { Threshold = 2 }).AsTask();

        Assert.False(second.IsCompleted);
        Assert.Equal(["import", "attach"], runtime.Calls);

        runtime.ReleaseAttach();
        TwinHandle<TestTwinOptions> attached = await Bounded(first);

        Assert.Same(attached, await Bounded(second));
        Assert.Equal(["import", "attach", "update"], runtime.Calls);
        Assert.Equal("""{"threshold":2}""", runtime.Updates.Single());
    }

    [Fact(DisplayName = "port: a re-attach in flight with the attach's own options completes after the attach and sends no update")]
    [Trait("Requirement", "ADR04-reattach-in-flight")]
    public async Task A_reattach_in_flight_with_unchanged_options_completes_after_the_attach_and_sends_nothing()
    {
        HeldAttachRuntime runtime = new();
        await using TestTwin twin = new(runtime);
        using IDisposable unblock = runtime.UnblockOnExit();

        Task<TwinHandle<TestTwinOptions>> first = twin.AttachAsync("base-ui-1", new TestTwinOptions { Threshold = 1 }).AsTask();
        Task<TwinHandle<TestTwinOptions>> second = twin.AttachAsync("base-ui-1", new TestTwinOptions { Threshold = 1 }).AsTask();

        Assert.False(second.IsCompleted);

        runtime.ReleaseAttach();
        Assert.Same(await Bounded(first), await Bounded(second));
        Assert.Equal(["import", "attach"], runtime.Calls);
    }

    [Fact(DisplayName = "port: re-attaches in flight forward their options in call order, each only when it differs from the last sent, references included")]
    [Trait("Requirement", "ADR04-reattach-in-flight")]
    public async Task Reattaches_in_flight_forward_in_order_and_only_what_changed()
    {
        HeldAttachRuntime runtime = new();
        await using TestTwin twin = new(runtime);
        using IDisposable unblock = runtime.UnblockOnExit();
        ElementReference target = new("target-1");
        TwinReferences references = new();
        references.Add(target);

        Task<TwinHandle<TestTwinOptions>> first = twin.AttachAsync("base-ui-1", new TestTwinOptions { Threshold = 1 }).AsTask();
        Task<TwinHandle<TestTwinOptions>> second = twin.AttachAsync("base-ui-1", new TestTwinOptions { Threshold = 2 }).AsTask();
        Task<TwinHandle<TestTwinOptions>> third = twin.AttachAsync("base-ui-1", new TestTwinOptions { Threshold = 2 }).AsTask();
        Task<TwinHandle<TestTwinOptions>> fourth = twin.AttachAsync("base-ui-1", new TestTwinOptions { Threshold = 2, TargetRef = 0 }, references).AsTask();

        Assert.False(fourth.IsCompleted);

        runtime.ReleaseAttach();
        await Bounded(Task.WhenAll(first, second, third, fourth));

        Assert.Equal(["import", "attach", "update", "update"], runtime.Calls);
        Assert.Equal(["""{"threshold":2}""", """{"threshold":2,"targetRef":0}"""], runtime.Updates);
        Assert.Equal([target], runtime.UpdateReferences[1]!);
    }

    [Fact(DisplayName = "port: a re-attach in flight of an attach that fails surfaces the same failure")]
    [Trait("Requirement", "ADR04-reattach-in-flight")]
    public async Task A_reattach_in_flight_of_a_failing_attach_surfaces_the_failure()
    {
        HeldAttachRuntime runtime = new();
        await using TestTwin twin = new(runtime);
        using IDisposable unblock = runtime.UnblockOnExit();

        Task<TwinHandle<TestTwinOptions>> first = twin.AttachAsync("base-ui-1", new TestTwinOptions()).AsTask();
        Task<TwinHandle<TestTwinOptions>> second = twin.AttachAsync("base-ui-1", new TestTwinOptions { Threshold = 2 }).AsTask();

        Assert.False(second.IsCompleted);

        runtime.FailAttach(new JSException("Iodine: the attach failed."));
        await Assert.ThrowsAsync<JSException>(() => Bounded(first));
        await Assert.ThrowsAsync<JSException>(() => Bounded(second));
        Assert.Equal(["import", "attach"], runtime.Calls);
    }

    // PR 427's verifier, finding 1: the attach has completed but an earlier forward's update is still on its way; a call in
    // that window joins the chain, so its options are not dropped and it does not return before the update ran.
    [Fact(DisplayName = "port: a re-attach after the attach completed while an earlier forward is still sending waits for that forward and sends its own options after it")]
    [Trait("Requirement", "ADR04-reattach-in-flight")]
    public async Task A_reattach_while_a_forward_is_sending_joins_the_chain()
    {
        HeldAttachRuntime runtime = new() { HoldUpdates = true };
        await using TestTwin twin = new(runtime);
        using IDisposable unblock = runtime.UnblockOnExit();

        Task<TwinHandle<TestTwinOptions>> first = twin.AttachAsync("base-ui-1", new TestTwinOptions { Threshold = 1 }).AsTask();
        Task<TwinHandle<TestTwinOptions>> second = twin.AttachAsync("base-ui-1", new TestTwinOptions { Threshold = 2 }).AsTask();
        runtime.ReleaseAttach();
        await Bounded(first);
        await Bounded(runtime.UpdateStarted);

        Task<TwinHandle<TestTwinOptions>> third = twin.AttachAsync("base-ui-1", new TestTwinOptions { Threshold = 3 }).AsTask();

        Assert.False(second.IsCompleted);
        Assert.False(third.IsCompleted);

        runtime.ReleaseUpdates();
        await Bounded(Task.WhenAll(second, third));
        Assert.Equal(["""{"threshold":2}""", """{"threshold":3}"""], runtime.Updates);
    }

    // PR 427's CI (run 46175183428, menu.filter-submenu.spec.ts:507 [server], 5 of 20 locally): a forward that resumes after
    // the part sent newer options through the handle overwrote them with the older options its re-attach carried.
    [Fact(DisplayName = "port: a forward still queued when the part sends newer options through the handle is dropped, so the module ends on the part's options")]
    [Trait("Requirement", "ADR04-reattach-in-flight")]
    public async Task A_queued_forward_older_than_the_parts_update_is_dropped()
    {
        HeldAttachRuntime runtime = new() { HoldUpdates = true };
        await using TestTwin twin = new(runtime);
        using IDisposable unblock = runtime.UnblockOnExit();

        Task<TwinHandle<TestTwinOptions>> first = twin.AttachAsync("base-ui-1", new TestTwinOptions { Threshold = 1 }).AsTask();
        Task<TwinHandle<TestTwinOptions>> second = twin.AttachAsync("base-ui-1", new TestTwinOptions { Threshold = 2 }).AsTask();
        Task<TwinHandle<TestTwinOptions>> third = twin.AttachAsync("base-ui-1", new TestTwinOptions { Threshold = 3 }).AsTask();

        Assert.False(third.IsCompleted);

        runtime.ReleaseAttach();
        TwinHandle<TestTwinOptions> handle = await Bounded(first);
        await Bounded(runtime.UpdateStarted);
        Task update = handle.UpdateAsync(new TestTwinOptions { Threshold = 5 }).AsTask();

        runtime.ReleaseUpdates();
        await Bounded(Task.WhenAll(second, third, update));
        Assert.Equal(["""{"threshold":2}""", """{"threshold":5}"""], runtime.Updates);
    }

    [Fact(DisplayName = "port: re-attaches in flight that go A, B, A send B and then A, so the module ends on the last render's options")]
    [Trait("Requirement", "ADR04-reattach-in-flight")]
    public async Task Reattaches_A_B_A_end_on_A()
    {
        HeldAttachRuntime runtime = new();
        await using TestTwin twin = new(runtime);
        using IDisposable unblock = runtime.UnblockOnExit();

        Task<TwinHandle<TestTwinOptions>> first = twin.AttachAsync("base-ui-1", new TestTwinOptions { Threshold = 1 }).AsTask();
        Task<TwinHandle<TestTwinOptions>> second = twin.AttachAsync("base-ui-1", new TestTwinOptions { Threshold = 2 }).AsTask();
        Task<TwinHandle<TestTwinOptions>> third = twin.AttachAsync("base-ui-1", new TestTwinOptions { Threshold = 1 }).AsTask();

        Assert.False(third.IsCompleted);

        runtime.ReleaseAttach();
        await Bounded(Task.WhenAll(first, second, third));
        Assert.Equal(["""{"threshold":2}""", """{"threshold":1}"""], runtime.Updates);
    }

    [Fact(DisplayName = "port: a re-attach in flight whose twin is disposed before the attach ends gets the no-op handle once it ends, and sends no update")]
    [Trait("Requirement", "ADR04-reattach-in-flight")]
    public async Task A_reattach_in_flight_of_a_disposed_twin_sends_nothing()
    {
        HeldAttachRuntime runtime = new();
        TestTwin twin = new(runtime);
        using IDisposable unblock = runtime.UnblockOnExit();

        Task<TwinHandle<TestTwinOptions>> first = twin.AttachAsync("base-ui-1", new TestTwinOptions { Threshold = 1 }).AsTask();
        Task<TwinHandle<TestTwinOptions>> second = twin.AttachAsync("base-ui-1", new TestTwinOptions { Threshold = 2 }).AsTask();
        ValueTask disposing = twin.DisposeAsync();

        Assert.False(second.IsCompleted);

        runtime.FailAttach(new JSException("Iodine: no element for the reference"));
        TwinHandle<TestTwinOptions> handle = await Bounded(second);
        Assert.Same(await Bounded(first), handle);
        await Bounded(disposing.AsTask());
        await Bounded(handle.UpdateAsync(new TestTwinOptions { Threshold = 4 }).AsTask());

        Assert.True(handle.IsDisposed);
        Assert.Equal(["import", "attach"], runtime.Calls);
    }

    private static Task<T> Bounded<T>(Task<T> task) => task.WaitAsync(s_bound, TestContext.Current.CancellationToken);

    private static Task Bounded(Task task) => task.WaitAsync(s_bound, TestContext.Current.CancellationToken);

    // The module import completes at once; the first attach waits until the test releases or fails it, later ones
    // complete at once. The module's handle records every update with its options and references.
    private sealed class HeldAttachRuntime : IJSRuntime, IJSObjectReference
    {
        private readonly TaskCompletionSource<IJSObjectReference> attach = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource updates = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource updateStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ModuleHandle handle;

        public HeldAttachRuntime()
        {
            handle = new ModuleHandle(this);
        }

        public List<string> Calls { get; } = [];

        public List<string> Updates { get; } = [];

        public List<object[]?> UpdateReferences { get; } = [];

        public void ReleaseAttach() => attach.SetResult(handle);

        // When set, every update waits until ReleaseUpdates; UpdateStarted completes when the first one arrives.
        public bool HoldUpdates { get; init; }

        public Task UpdateStarted => updateStarted.Task;

        public void ReleaseUpdates() => updates.TrySetResult();

        public void FailAttach(Exception exception) => attach.SetException(exception);

        // Disposed before the twin, so a failed assertion never leaves the twin's disposal waiting on the held attach.
        public IDisposable UnblockOnExit() => new Unblock(attach, updates);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            Calls.Add(identifier);
            return identifier switch
            {
                "import" => ValueTask.FromResult((TValue)(object)this),
                "attach" => new ValueTask<TValue>(attach.Task.ContinueWith(task => (TValue)task.GetAwaiter().GetResult(), TaskScheduler.Default)),
                _ => ValueTask.FromResult(default(TValue)!),
            };
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private sealed class Unblock(TaskCompletionSource<IJSObjectReference> attach, TaskCompletionSource updates) : IDisposable
        {
            public void Dispose()
            {
                attach.TrySetException(new JSDisconnectedException("The test ended with the attach held."));
                updates.TrySetResult();
            }
        }

        private sealed class ModuleHandle(HeldAttachRuntime runtime) : IJSObjectReference
        {
            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

            public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
            {
                if (identifier == "update")
                {
                    runtime.Calls.Add(identifier);
                    runtime.Updates.Add((string)args![0]!);
                    runtime.UpdateReferences.Add(args.Length > 1 ? (object[]?)args[1] : null);
                    runtime.updateStarted.TrySetResult();
                    if (runtime.HoldUpdates)
                    {
                        return new ValueTask<TValue>(runtime.updates.Task.ContinueWith(_ => default(TValue)!, TaskScheduler.Default));
                    }
                }

                return ValueTask.FromResult(default(TValue)!);
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}

#pragma warning restore BL0016

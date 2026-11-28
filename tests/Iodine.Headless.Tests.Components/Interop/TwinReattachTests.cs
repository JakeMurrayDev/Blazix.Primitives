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
        TwinHandle<TestTwinOptions> attached = await first;

        Assert.Same(attached, await second);
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
        Assert.Same(await first, await second);
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
        await Task.WhenAll(first, second, third, fourth);

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
        await Assert.ThrowsAsync<JSException>(() => first);
        await Assert.ThrowsAsync<JSException>(() => second);
        Assert.Equal(["import", "attach"], runtime.Calls);
    }

    // The module import completes at once; the first attach waits until the test releases or fails it, later ones
    // complete at once. The module's handle records every update with its options and references.
    private sealed class HeldAttachRuntime : IJSRuntime, IJSObjectReference
    {
        private readonly TaskCompletionSource<IJSObjectReference> attach = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ModuleHandle handle;

        public HeldAttachRuntime()
        {
            handle = new ModuleHandle(this);
        }

        public List<string> Calls { get; } = [];

        public List<string> Updates { get; } = [];

        public List<object[]?> UpdateReferences { get; } = [];

        public void ReleaseAttach() => attach.SetResult(handle);

        public void FailAttach(Exception exception) => attach.SetException(exception);

        // Disposed before the twin, so a failed assertion never leaves the twin's disposal waiting on the held attach.
        public IDisposable UnblockOnExit() => new Unblock(attach);

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

        private sealed class Unblock(TaskCompletionSource<IJSObjectReference> attach) : IDisposable
        {
            public void Dispose() => attach.TrySetException(new JSDisconnectedException("The test ended with the attach held."));
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
                }

                return ValueTask.FromResult(default(TValue)!);
            }

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}

#pragma warning restore BL0016

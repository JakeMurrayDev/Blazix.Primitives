using Blazix.Primitives.Interop;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components.Interop;

// The runtime here is a test double, not interop: the analyzer reads its members as JS interop calls.
#pragma warning disable BL0016

/// <summary>
/// ADR 04 Amendments 2026-10-29 (gap 17 of M4 track 5): an attach whose twin is disposed while it is in flight names an
/// element that may be gone; the twin yields a no-op handle, as it does when the circuit disconnects, instead of the
/// module's error surfacing in the part's OnAfterRenderAsync.
/// </summary>
public sealed class TwinAttachDisposalTests
{
    [Fact(DisplayName = "port: an attach whose twin is disposed in flight yields a no-op handle instead of the module's error")]
    [Trait("Requirement", "ADR04-attach-after-render")]
    public async Task An_attach_whose_twin_is_disposed_in_flight_yields_a_no_op_handle()
    {
        StallingRuntime runtime = new();
        TestTwin twin = new(runtime);

        Task<TwinHandle<TestTwinOptions>> attaching = twin.AttachAsync("base-ui-1", new TestTwinOptions()).AsTask();
        ValueTask disposing = twin.DisposeAsync();
        runtime.FailAttach(new JSException("Blazix: no element for the reference"));

        TwinHandle<TestTwinOptions> handle = await attaching;
        await disposing;
        await handle.UpdateAsync(new TestTwinOptions { Threshold = 1 });
        Assert.Equal(["import", "attach"], runtime.Calls);
    }

    // The module import completes at once; the attach waits until the test fails it. Every call is recorded.
    private sealed class StallingRuntime : IJSRuntime, IJSObjectReference
    {
        private readonly TaskCompletionSource<IJSObjectReference> attach = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<string> Calls { get; } = [];

        public void FailAttach(Exception exception) => attach.SetException(exception);

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
    }
}

#pragma warning restore BL0016

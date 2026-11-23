using Blazix.Primitives.Interop;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components.Interop;

// The runtime here is a test double, not interop: the analyzer reads its members as JS interop calls.
#pragma warning disable BL0016

/// <summary>
/// The popup families' attachment helper (ADR 09; M4 track 5): a part that leaves while its first attach is in flight
/// takes its element with it, so the module finds no element for the reference.
/// </summary>
public sealed class PopupModuleAttachmentTests
{
    [Fact(DisplayName = "port: an attach that the part's disposal overtakes drops the module's missing-element error")]
    [Trait("Requirement", "ADR04-attach-after-render")]
    public async Task An_attach_that_the_parts_disposal_overtakes_drops_the_missing_element_error()
    {
        StallingRuntime runtime = new();
        PopupModuleAttachment attachment = new(runtime);

        Task sync = attachment.SyncAsync(new ElementReference("popup"), new PopupOptions());
        ValueTask disposing = attachment.DisposeAsync();
        runtime.FailAttach(new JSException("Blazix: no element for the reference"));

        await sync;
        await disposing;
        Assert.Equal(0, attachment.Updates);
    }

    [Fact(DisplayName = "port: an attach that fails while the part is still there reports the error")]
    [Trait("Requirement", "ADR04-attach-after-render")]
    public async Task An_attach_that_fails_while_the_part_is_still_there_reports_the_error()
    {
        StallingRuntime runtime = new();
        await using PopupModuleAttachment attachment = new(runtime);

        Task sync = attachment.SyncAsync(new ElementReference("popup"), new PopupOptions());
        runtime.FailAttach(new JSException("Blazix: no element for the reference"));

        await Assert.ThrowsAsync<JSException>(() => sync);
    }

    // The module import completes at once; the attach waits until the test fails it.
    private sealed class StallingRuntime : IJSRuntime, IJSObjectReference
    {
        private readonly TaskCompletionSource<IJSObjectReference> attach = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void FailAttach(Exception exception) => attach.SetException(exception);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => identifier switch
        {
            "import" => ValueTask.FromResult((TValue)(object)this),
            "attach" => new ValueTask<TValue>(attach.Task.ContinueWith(task => (TValue)task.GetAwaiter().GetResult(), TaskScheduler.Default)),
            _ => ValueTask.FromResult(default(TValue)!),
        };

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

#pragma warning restore BL0016

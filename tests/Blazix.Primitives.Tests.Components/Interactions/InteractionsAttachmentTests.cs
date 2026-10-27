using System.Text.Json;
using Blazix.Primitives.Rendering;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

// The runtime here is a test double, not interop: the analyzer reads its members as JS interop calls.
#pragma warning disable BL0016

/// <summary>
/// L2: the attach-then-flush cycle of the interactions behaviours under a stalled module load, against a runtime whose
/// <c>attach</c> completes when the test says (the `CompositeRootBehavior` flush-after-attach precedent).
/// </summary>
public sealed class InteractionsAttachmentTests
{
    [Fact]
    public async Task A_change_made_while_the_attach_is_in_flight_reaches_the_handle_once_the_attach_completes()
    {
        StallingRuntime runtime = new();
        await using InteractionsSessionBehavior session = new(runtime, new PartIdGenerator(), _ => Task.CompletedTask, _ => Task.CompletedTask);

        Task first = session.AfterRenderAsync();
        session.Open = true;
        await session.AfterRenderAsync();
        runtime.CompleteAttach();
        await first;

        string update = Assert.Single(runtime.Updates);
        Assert.True(JsonDocument.Parse(update).RootElement.GetProperty("session").GetProperty("open").GetBoolean());
    }

    private sealed class StallingRuntime : IJSRuntime, IJSObjectReference
    {
        private readonly TaskCompletionSource<IJSObjectReference> attach = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<string> Updates { get; } = [];

        public void CompleteAttach() => attach.SetResult(this);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            switch (identifier)
            {
                case "import":
                    return ValueTask.FromResult((TValue)(object)this);
                case "attach":
                    return new ValueTask<TValue>(attach.Task.ContinueWith(task => (TValue)task.Result, TaskScheduler.Default));
                case "update":
                    Updates.Add((string)args![0]!);
                    return ValueTask.FromResult(default(TValue)!);
                default:
                    return ValueTask.FromResult(default(TValue)!);
            }
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

#pragma warning restore BL0016

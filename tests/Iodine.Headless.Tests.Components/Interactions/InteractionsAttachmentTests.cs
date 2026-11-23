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

    // Design point 3 of M4 track 1 (settled by the orchestrator 2026-10-27): until the portal module has moved the node it
    // is `display: none` in place, so a portaled part's handle awaits PopupPortalContext.WhenMoved before its first attach.
    [Fact]
    public async Task The_floating_handle_inside_a_portal_attaches_only_once_the_portal_has_moved_its_node()
    {
        StallingRuntime runtime = new();
        runtime.CompleteAttach();
        PopupPortalContext portal = new();
        await using InteractionsFloatingBehavior floating = new(runtime, _ => Task.CompletedTask, _ => Task.CompletedTask)
        {
            SessionToken = "base-ui-1",
            PortalContext = portal,
        };

        Task first = floating.AfterRenderAsync(true, null, "base-ui-2");
        await floating.AfterRenderAsync(true, null, "base-ui-2");
        Assert.Equal(0, runtime.Attaches);
        Assert.False(first.IsCompleted);

        portal.CompleteMove();
        await first;

        Assert.Equal(1, runtime.Attaches);
        Assert.Empty(runtime.Updates);
    }

    [Fact]
    public async Task The_floating_handle_outside_any_portal_attaches_at_its_first_render()
    {
        StallingRuntime runtime = new();
        runtime.CompleteAttach();
        await using InteractionsFloatingBehavior floating = new(runtime, _ => Task.CompletedTask, _ => Task.CompletedTask) { SessionToken = "base-ui-1" };

        await floating.AfterRenderAsync(true, null, "base-ui-2");

        Assert.Equal(1, runtime.Attaches);
    }

    private sealed class StallingRuntime : IJSRuntime, IJSObjectReference
    {
        private readonly TaskCompletionSource<IJSObjectReference> attach = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<string> Updates { get; } = [];

        public int Attaches { get; private set; }

        public void CompleteAttach() => attach.SetResult(this);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            switch (identifier)
            {
                case "import":
                    return ValueTask.FromResult((TValue)(object)this);
                case "attach":
                    Attaches++;
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

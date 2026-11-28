using Iodine.Headless.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Iodine.Headless.Tests.Rendering;

/// <summary>
/// ADR 16 rule 7 at L1 for the pipeline alone, around a steady-state pass of <c>RenderPipeline.Render</c> with its lists,
/// handler slots and class memo warm. Issue 57: recognising the part's created handler cost a box of the slot's
/// <see cref="EventCallback"/> per handler per render, because Blazor's callback has only <c>Equals(object)</c>. The one
/// box a pass keeps is Blazor's own, the frame's <c>object</c> slot that <c>RenderTreeBuilder.AddAttribute</c> stores the
/// emitted callback in, so the pass is measured against the size of that one box.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class PipelineAllocationTests
{
    // Pinned at 0 beyond the frame's box (issue 304's zero pin: no tolerance), 2026-11-28, arm64, Release: on the base the
    // pass allocated 64 bytes, the frame's 32-byte box and the comparison's; with issue 57 it allocates the frame's box alone.
    private const long PipelineBudgetBytes = 0;

    [Fact(DisplayName = "a steady-state pipeline pass that adds the part's created handler allocates only the frame's box of it")]
    [Trait("Requirement", "ADR16-7")]
    [Trait("Requirement", "RND-EVT-02")]
    public void A_pass_that_adds_the_created_handler_allocates_only_the_frames_box_of_it()
    {
        long pass = LeastOfFive();
        long frameBox = BoxSize();

        Assert.True(frameBox > 0);
        AllocationSampler.AssertWithinBudget(pass - frameBox, PipelineBudgetBytes, "a steady-state pipeline pass, beyond the frame's box,");
    }

    // The bytes of one boxed EventCallback on this runtime: what RenderTreeBuilder.AddAttribute stores in the frame.
    private static long BoxSize()
    {
        EventCallback callback = new(null, (Func<MouseEventArgs, Task>)(_ => Task.CompletedTask));
        long least = long.MaxValue;
        for (int run = 0; run < 5; run++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            object boxed = callback;
            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
            GC.KeepAlive(boxed);
        }

        return least;
    }

    private static long LeastOfFive()
    {
        TestHost host = new();
        using PartRenderState state = new(host);
        EventCallback handler = state.CreateHandler<MouseEventArgs>("onclick", _ => Task.CompletedTask);
        AttributeList partAttributes = new();
        using RenderTreeBuilder builder = new();
        Func<EmptyState, string?> classValue = static _ => "part";

        void Pass()
        {
            builder.Clear();
            partAttributes.Clear();
            partAttributes.Set("role", "group");
            partAttributes.Set("onclick", handler);
            RenderPipeline.Render(builder, state, host, new RenderInputs<EmptyState>
            {
                State = EmptyState.Instance,
                PartAttributes = partAttributes,
                ClassValue = classValue,
                DefaultTagName = "div",
                Enabled = true,
            });
        }

        for (int i = 0; i < 20; i++)
        {
            Pass();
        }

        long least = long.MaxValue;
        for (int run = 0; run < 5; run++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            Pass();
            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        Assert.True(state.Merged.TryGetValue("onclick", out AttributeValue emitted) && emitted.IsHandler);
        return least;
    }

    private sealed class TestHost : IPartHost
    {
        /// <inheritdoc />
        public string? HostWrapperTagName => null;

        /// <inheritdoc />
        public Task HandleEventAsync(EventCallbackWorkItem item, object? arg) => item.InvokeAsync(arg);

        /// <inheritdoc />
        public void SetElement(ElementReference? element)
        {
        }

        /// <inheritdoc />
        public bool ForwardsConsumerHandler(string eventName) => true;

        /// <inheritdoc />
        public void FinishAttributes(AttributeList attributes)
        {
        }

        /// <inheritdoc />
        public void BuildAfterElement(RenderTreeBuilder builder)
        {
        }

        /// <inheritdoc />
        public void BuildBeforeElement(RenderTreeBuilder builder)
        {
        }

        /// <inheritdoc />
        public void AddHostWrapperAttributes(AttributeList attributes)
        {
        }
    }
}

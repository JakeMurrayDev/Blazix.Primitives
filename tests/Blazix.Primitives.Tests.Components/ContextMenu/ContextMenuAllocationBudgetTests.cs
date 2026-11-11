using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 for ContextMenu (the brief's Tests first): a re-render with unchanged parameters of a closed root with its
/// trigger, portal, positioner, popup and items, and a re-render of the trigger alone, each measured around bUnit's
/// synchronous render on the test thread (the least of five). The root's includes Blazor's own per-render cost of the root
/// and its fixed cascades; the trigger's, its state, its attributes and the options comparison that sends nothing.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class ContextMenuAllocationBudgetTests : IDisposable
{
    // First measurement 2026-11-11 (arm64, .NET 11, Release; M5 track 7): 1528 bytes in 3 of 3 runs, the least of five
    // (AllocationSampler.LeastOfFive), for the parent passing the same child content again to a closed root with a trigger, a
    // portal and two items: Menu's 1360 plus the root's own fixed cascade of its parent context. The trigger and the portal,
    // whose parameters are the same fragments, are skipped by Blazor's change detection. The budget is the envelope plus
    // 112 B (the brief's), and only tightens afterwards.
    private const long RootRerenderBudgetBytes = 1640;

    // First measurement 2026-11-11: 352 bytes in 3 of 3 runs for the trigger rendered again with no parameter changed: its
    // render, its attributes and the options comparison, which sends no update. The envelope plus 112 B.
    private const long TriggerRerenderBudgetBytes = 464;

    private readonly BunitContext context = new();

    public ContextMenuAllocationBudgetTests()
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "port: a re-render of a closed context menu root with its parts and unchanged parameters stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_root_rerender_with_unchanged_parameters_stays_within_its_budget()
    {
        RenderFragment content = Content;
        Action<ComponentParameterCollectionBuilder<ContextMenuRoot>> parameters = p => p.AddChildContent(content);
        IRenderedComponent<ContextMenuRoot> cut = context.Render(parameters);

        for (int i = 0; i < 20; i++)
        {
            cut.Render(parameters);
        }

        long allocated = AllocationSampler.LeastOfFive(() => cut.Render(parameters));

        Assert.True(allocated <= RootRerenderBudgetBytes, $"a re-render allocated {allocated} bytes; the budget is {RootRerenderBudgetBytes}");
    }

    [Fact(DisplayName = "port: a re-render of a context menu trigger with unchanged parameters sends nothing and stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_trigger_rerender_with_unchanged_parameters_stays_within_its_budget()
    {
        IRenderedComponent<ContextMenuRoot> root = context.Render<ContextMenuRoot>(p => p.AddChildContent(Content));
        IRenderedComponent<ContextMenuTrigger> trigger = root.FindComponent<ContextMenuTrigger>();
        Action<ComponentParameterCollectionBuilder<ContextMenuTrigger>> unchanged = _ => { };

        for (int i = 0; i < 20; i++)
        {
            trigger.Render(unchanged);
        }

        int updates = context.JSInterop.Invocations.Count(invocation => invocation.Identifier == "update");
        long allocated = AllocationSampler.LeastOfFive(() => trigger.Render(unchanged));

        Assert.Equal(updates, context.JSInterop.Invocations.Count(invocation => invocation.Identifier == "update"));
        Assert.True(allocated <= TriggerRerenderBudgetBytes, $"a re-render allocated {allocated} bytes; the budget is {TriggerRerenderBudgetBytes}");
    }

    private static void Content(RenderTreeBuilder builder)
    {
        builder.OpenComponent<ContextMenuTrigger>(0);
        builder.CloseComponent();
        builder.OpenComponent<ContextMenuPortal>(1);
        builder.AddComponentParameter(2, nameof(ContextMenuPortal.ChildContent), (RenderFragment)Portal);
        builder.CloseComponent();
    }

    private static void Portal(RenderTreeBuilder builder)
    {
        builder.OpenComponent<ContextMenuPositioner>(0);
        builder.AddComponentParameter(1, nameof(ContextMenuPositioner.ChildContent), (RenderFragment)Positioner);
        builder.CloseComponent();
    }

    private static void Positioner(RenderTreeBuilder builder)
    {
        builder.OpenComponent<ContextMenuPopup>(0);
        builder.AddComponentParameter(1, nameof(ContextMenuPopup.ChildContent), (RenderFragment)Popup);
        builder.CloseComponent();
    }

    private static void Popup(RenderTreeBuilder builder)
    {
        builder.OpenComponent<ContextMenuItem>(0);
        builder.CloseComponent();
        builder.OpenComponent<ContextMenuItem>(1);
        builder.CloseComponent();
    }
}

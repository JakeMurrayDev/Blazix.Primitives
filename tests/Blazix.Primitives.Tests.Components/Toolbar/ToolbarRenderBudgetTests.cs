using Bunit;
using Microsoft.AspNetCore.Components;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 for Toolbar at L2: a re-render of a toolbar with a button, a link, an input, a group of one button and a
/// separator, with unchanged parameters (the parent passing the same values again), measured around bUnit's synchronous
/// work on the test thread. The re-render includes Blazor's own per-render cost and the composite twin's comparison of its
/// options; it reaches no item and sends nothing. The budget was taken from the first measurement and only tightens
/// afterwards.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class ToolbarRenderBudgetTests : IDisposable
{
    // First measurement 2026-10-27 (arm64, .NET 11 RC2, Release): 736 bytes, which reaches no item (bUnit's parameterless
    // Render() passes no parameter, so the cascade keeps its content).
    private const long RootRerenderBudgetBytes = 736;

    private readonly ToolbarTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: a re-render of a toolbar with unchanged items sends nothing and stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_rerender_of_a_toolbar_with_unchanged_items_sends_nothing_and_stays_within_its_budget()
    {
        RenderFragment items = builder =>
        {
            builder.OpenComponent<ToolbarButton>(0);
            builder.CloseComponent();
            builder.OpenComponent<ToolbarLink>(1);
            builder.AddAttribute(2, "href", "#");
            builder.CloseComponent();
            builder.OpenComponent<ToolbarInput>(3);
            builder.CloseComponent();
            builder.OpenComponent<ToolbarGroup>(4);
            builder.AddComponentParameter(5, nameof(ToolbarGroup.ChildContent), (RenderFragment)(group =>
            {
                group.OpenComponent<ToolbarButton>(0);
                group.CloseComponent();
            }));
            builder.CloseComponent();
            builder.OpenComponent<ToolbarSeparator>(6);
            builder.CloseComponent();
        };
        IRenderedComponent<ToolbarRoot> cut = test.Context.Render<ToolbarRoot>(p => p
            .AddUnmatched("class", "consumer")
            .AddUnmatched("aria-label", "Formatting")
            .AddChildContent(items));
        for (int i = 0; i < 20; i++)
        {
            cut.Render();
        }

        int compositeSends = test.CompositeAttaches.Count + test.CompositeUpdates.Count;
        int buttonSends = test.ButtonAttaches.Count + test.ButtonUpdates.Count;
        long before = GC.GetAllocatedBytesForCurrentThread();
        cut.Render();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(compositeSends, test.CompositeAttaches.Count + test.CompositeUpdates.Count);
        Assert.Equal(buttonSends, test.ButtonAttaches.Count + test.ButtonUpdates.Count);
        Assert.True(allocated <= RootRerenderBudgetBytes, $"a re-render allocated {allocated} bytes; the budget is {RootRerenderBudgetBytes}");
    }
}

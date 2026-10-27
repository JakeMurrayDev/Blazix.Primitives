using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 for the composite root: a re-render of <see cref="TestCompositeRoot"/> with unchanged parameters and
/// items, measured around bUnit's synchronous render on the test thread, as Button's budget is. It includes Blazor's own
/// per-render cost and the behaviour's comparison of its options, which sends nothing. The budget was taken from the
/// first measurement and only tightens afterwards.
/// </summary>
public sealed class CompositeAllocationBudgetTests : IDisposable
{
    // First measurement 2026-10-22 (arm64, .NET 11 RC2, Release): 3936 bytes for the parent passing the same values again
    // to a root with three items. The root, the fixed cascade and the three items all render: an item's child content is a
    // new fragment on every render of the content, as a consumer's markup is. The behaviours' own share is the boxed
    // enumerator of the root's pass over the unmatched bag (as Button's); the comparison of the options allocates nothing.
    // 4032 the same day with option (i) of ADR 04's note of 2026-10-22 (chosen by the user 2026-10-23): each item creates its own
    // onkeydown handler, and the pipeline renders a part's created handler on every render, 32 bytes per item.
    // 4040 on 2026-10-26 (M3 track 1, E3): the library's share is unchanged; the harness's grew. The parameters lambda
    // rebuilds the expression `x => x.ModifierKeys` on every render, and `Expression.Property` copies the host's array of
    // public properties to find the getter's property, 8 bytes per property: TestCompositeRoot gained
    // `DeriveDisabledIndices`. A probe property that is not a parameter costs the same 8 bytes, and the options'
    // comparison of the new field allocates nothing.
    // 4048 the same day (M3 track 1, E2): TestCompositeRoot gained `ReportItems`, the same 8 bytes of the harness.
    // 4056 the same day (M3 track 1, E5): TestCompositeRoot gained `FollowActiveItem`, the same 8 bytes again.
    // 4064 on 2026-10-27 (M4 track 1, the host wrapper of ADR 02 Amendments 2026-10-27): the library's share is unchanged
    // (a part that does not opt in reads one virtual property and allocates nothing); the harness's grew. `PartBase` gained
    // the protected `HostWrapperTagName`, and `Expression.Property` copies the public and non-public properties, 8 bytes
    // each: a second protected probe property moved the figure to 4072, measured the same day.
    private const long RerenderBudgetBytes = 4064;

    private readonly CompositeTestContext host = new();

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "a re-render of a composite root with unchanged items sends nothing and stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    [Trait("Requirement", "CMP-STA-10")]
    public void A_rerender_with_unchanged_items_sends_nothing_and_stays_within_its_budget()
    {
        EventCallback<KeyboardEventArgs> onKeyDown = EventCallback.Factory.Create<KeyboardEventArgs>(this, () => { });
        IReadOnlyList<string> modifierKeys = ["Shift"];
        RenderFragment content = CompositeItems.Of("a", "b", "c");
        Action<ComponentParameterCollectionBuilder<TestCompositeRoot>> parameters = p => p
            .Add(x => x.ModifierKeys, modifierKeys)
            .AddUnmatched("class", "group")
            .AddUnmatched("onkeydown", onKeyDown)
            .AddChildContent(content);
        IRenderedComponent<TestCompositeRoot> cut = host.Context.Render(parameters);

        for (int i = 0; i < 20; i++)
        {
            cut.Render(parameters);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        cut.Render(parameters);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Empty(host.UpdateOptions);
        Assert.True(allocated <= RerenderBudgetBytes, $"a re-render allocated {allocated} bytes; the budget is {RerenderBudgetBytes}");
    }
}

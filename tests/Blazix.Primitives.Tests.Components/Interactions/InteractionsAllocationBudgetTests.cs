using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 for the interactions behaviours: a re-render of <see cref="TestInteractionsRoot"/> with unchanged inputs,
/// measured around bUnit's synchronous render on the test thread, as the composite root's budget is. It includes Blazor's
/// own per-render cost of the root, its fixed cascade and its trigger; the behaviours' comparison of their options
/// allocates nothing and sends nothing. The budget was taken from the first measurement and only tightens afterwards.
/// </summary>
public sealed class InteractionsAllocationBudgetTests : IDisposable
{
    // First measurement 2026-10-27 (arm64, .NET 11, Release): 2072 bytes for the parent passing the same values again to a
    // root with one trigger. The root and its fixed cascade render; the trigger, whose only parameter is the same
    // EventCallback, is skipped by Blazor's change detection. The session behaviour compares its inputs with the options
    // last sent before it builds a record, so the comparison allocates nothing and nothing is sent.
    private const long RerenderBudgetBytes = 2072;

    private readonly InteractionsTestContext host = new();

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "a re-render of a popup root and its trigger with unchanged inputs sends nothing and stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_rerender_with_unchanged_inputs_sends_nothing_and_stays_within_its_budget()
    {
        EventCallback<MouseEventArgs> onClick = EventCallback.Factory.Create<MouseEventArgs>(this, () => { });
        InteractionsDismissOptions dismiss = new() { OwnBackdropIds = ["backdrop"] };
        RenderFragment content = builder =>
        {
            builder.OpenComponent<TestInteractionsTrigger>(0);
            builder.AddAttribute(1, "onclick", onClick);
            builder.CloseComponent();
        };
        Action<ComponentParameterCollectionBuilder<TestInteractionsRoot>> parameters = p => p
            .Add(x => x.Dismiss, dismiss)
            .AddChildContent(content);
        IRenderedComponent<TestInteractionsRoot> cut = host.Context.Render(parameters);

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

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 for Button: a re-render with unchanged parameters, the parent passing the same values again, measured
/// around bUnit's synchronous render on the test thread. It includes Blazor's own per-render cost (the parameter view,
/// the unmatched-attribute dictionary, the diff) and the twin's comparison of the options, which sends nothing. A part
/// render needs a renderer, so the budget sits at L2 beside <c>Rendering/AllocationBudgetTests</c>. The budget was taken
/// from the first measurement and only tightens afterwards.
/// </summary>
public sealed class ButtonAllocationBudgetTests : IDisposable
{
    // First measurement 2026-10-21 (arm64, .NET 11 RC2, Release): 616 bytes enabled and 584 disabled, against 504 for
    // SampleRoot given the same bag. The difference is the part's two passes over the unmatched bag through its
    // interface (the boxed enumerators) when it withholds the key handler; the withheld dictionary is reused.
    private const long EnabledRerenderBudgetBytes = 616;
    private const long DisabledRerenderBudgetBytes = 584;

    private readonly BunitContext context = new();

    public ButtonAllocationBudgetTests()
    {
        context.Services.AddBlazixPrimitives();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "a re-render of an enabled button with unchanged parameters stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_rerender_of_an_enabled_button_with_unchanged_parameters_stays_within_its_budget()
    {
        long allocated = Measure(disabled: false);

        Assert.True(allocated <= EnabledRerenderBudgetBytes, $"a re-render allocated {allocated} bytes; the budget is {EnabledRerenderBudgetBytes}");
    }

    [Fact(DisplayName = "a re-render of a disabled button that withholds the consumer's handlers stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_rerender_of_a_disabled_button_that_withholds_the_consumers_handlers_stays_within_its_budget()
    {
        long allocated = Measure(disabled: true);

        Assert.True(allocated <= DisabledRerenderBudgetBytes, $"a re-render allocated {allocated} bytes; the budget is {DisabledRerenderBudgetBytes}");
    }

    private long Measure(bool disabled)
    {
        EventCallback<MouseEventArgs> onClick = EventCallback.Factory.Create<MouseEventArgs>(this, () => { });
        EventCallback<KeyboardEventArgs> onKeyDown = EventCallback.Factory.Create<KeyboardEventArgs>(this, () => { });
        RenderFragment content = builder => builder.AddContent(0, "Save");
        IRenderedComponent<Button> cut = context.Render<Button>(p => p
            .Add(x => x.Disabled, disabled)
            .AddUnmatched("class", "consumer")
            .AddUnmatched("onclick", onClick)
            .AddUnmatched("onkeydown", onKeyDown)
            .AddChildContent(content));

        for (int i = 0; i < 20; i++)
        {
            cut.Render();
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        cut.Render();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}

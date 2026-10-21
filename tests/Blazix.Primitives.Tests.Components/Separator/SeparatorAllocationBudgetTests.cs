using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using LibraryOrientation = Blazix.Primitives.Orientation;

// The namespace is the project's root, not a `Separator` namespace (see SeparatorConformanceTests).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 at L2 for <see cref="Separator"/>: the steady-state re-render with unchanged parameters (the state record
/// is reused, the class and style merges are memoised), measured the way <c>AllocationBudgetTests</c> measures
/// <c>SampleRoot</c>. The budget is the first measurement and only tightens afterwards.
/// </summary>
public sealed class SeparatorAllocationBudgetTests : IDisposable
{
    // Provisional until the part exists: replaced by the first measurement in the implementing commit.
    private const long RenderBudgetBytes = 256;

    private readonly BunitContext context = new();

    public SeparatorAllocationBudgetTests()
    {
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "a steady-state render of a separator stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_steady_state_render_of_a_separator_stays_within_its_budget()
    {
        IRenderedComponent<Separator> cut = context.Render<Separator>(p => p
            .Add(x => x.Orientation, LibraryOrientation.Vertical)
            .AddUnmatched("class", "consumer")
            .AddUnmatched("style", "margin: 0")
            .AddUnmatched("data-testid", "separator")
            .Add(x => x.ClassValue, (Func<SeparatorState, string?>)(state => state.Orientation == LibraryOrientation.Vertical ? "vertical" : null)));

        IComponent part = cut.Instance;
        // A parent re-render with unchanged parameters reaches the part as SetParametersAsync; an empty view keeps every
        // parameter as it was and still renders.
        Func<Task> refresh = () => part.SetParametersAsync(ParameterView.Empty);
        for (int i = 0; i < 20; i++)
        {
            cut.InvokeAsync(refresh);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        cut.InvokeAsync(refresh);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated <= RenderBudgetBytes, $"a render allocated {allocated} bytes; the budget is {RenderBudgetBytes}");
    }
}

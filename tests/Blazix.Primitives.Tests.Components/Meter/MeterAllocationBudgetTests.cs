using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 at L2 for <see cref="MeterRoot"/>: the steady-state re-render with unchanged <c>Value</c>, <c>Format</c>
/// and <c>Culture</c> (the derived text, the serialized numbers and the state are reused; the style and class merges are
/// memoised), measured the way <c>AllocationBudgetTests</c> measures <c>SampleRoot</c>. The budget is the first measurement
/// and only tightens afterwards.
/// </summary>
public sealed class MeterAllocationBudgetTests : IDisposable
{
    // Set from the first measurement (see the pull request); a re-render that reaches Blazor's own per-render work only.
    private const long RenderBudgetBytes = 1024;

    private readonly BunitContext context = new();

    public MeterAllocationBudgetTests()
    {
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "a steady-state render of a meter root stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_steady_state_render_of_a_meter_root_stays_within_its_budget()
    {
        IRenderedComponent<MeterRoot> cut = context.Render<MeterRoot>(p => p
            .Add(x => x.Value, 33.3)
            .Add(x => x.Format, "N1")
            .Add(x => x.Culture, CultureInfo.GetCultureInfo("de-DE"))
            .AddUnmatched("class", "consumer")
            .AddUnmatched("data-testid", "meter"));

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

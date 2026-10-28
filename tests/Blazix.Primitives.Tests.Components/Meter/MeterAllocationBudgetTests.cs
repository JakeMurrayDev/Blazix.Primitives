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
[Collection(AllocationBudgetsCollection.Name)]
public sealed class MeterAllocationBudgetTests : IDisposable
{
    // First measurement 2026-10-21 (arm64, .NET 11 RC2, Release): 344 bytes, all of it Blazor's own per-render work on this
    // path (SetParametersAsync, the dispatcher, the diff, and the fixed CascadingValue that carries the context re-setting
    // its parameters): Separator measures 264 for a part with no child component. The root's derived text, the serialized
    // numbers, the state and the cascade fragment are reused while the inputs are unchanged, so the root adds nothing.
    private const long RenderBudgetBytes = 344;

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

        long allocated = AllocationSampler.LeastOfFive(() => cut.InvokeAsync(refresh));

        Assert.True(allocated <= RenderBudgetBytes, $"a render allocated {allocated} bytes; the budget is {RenderBudgetBytes}");
    }
}

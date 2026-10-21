using System.Globalization;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 at L2 for <see cref="ProgressRoot"/>: the steady-state re-render with unchanged parameters (the derived
/// text, the serialized numbers and the cascade fragment are reused; the status is one of three shared state records, so
/// none is allocated while the status is unchanged; the class and style merges are memoised), measured the way
/// <c>AllocationBudgetTests</c> measures <c>SampleRoot</c>. The budget is the first measurement and only tightens afterwards.
/// </summary>
public sealed class ProgressAllocationBudgetTests : IDisposable
{
    // First measurement 2026-10-21 (arm64, .NET 11 RC2, Release): 344 bytes, the same as MeterRoot's (MeterAllocationBudgetTests):
    // all of it Blazor's own per-render work on this path (SetParametersAsync, the dispatcher, the diff, and the fixed
    // CascadingValue that carries the context re-setting its parameters). The root allocates no state record and no text.
    private const long RenderBudgetBytes = 344;

    private readonly BunitContext context = new();

    public ProgressAllocationBudgetTests()
    {
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "a steady-state render of a progress root stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_steady_state_render_of_a_progress_root_stays_within_its_budget()
    {
        IRenderedComponent<ProgressRoot> cut = context.Render<ProgressRoot>(p => p
            .Add(x => x.Value, 33.3)
            .Add(x => x.Format, "N1")
            .Add(x => x.Culture, CultureInfo.GetCultureInfo("de-DE"))
            .AddUnmatched("class", "consumer")
            .AddUnmatched("data-testid", "progress"));

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

    [Fact(DisplayName = "passes the same state record to the class function while the status is unchanged")]
    [Trait("Requirement", "ADR16-7")]
    [Trait("Requirement", "PRG-STA-01")]
    public void Passes_the_same_state_record_to_the_class_function_while_the_status_is_unchanged()
    {
        List<ProgressRootState> states = [];
        IRenderedComponent<ProgressRoot> cut = context.Render<ProgressRoot>(p => p
            .Add(x => x.Value, 20)
            .Add(x => x.ClassValue, state => { states.Add(state); return null; }));

        cut.Render(p => p.Add(x => x.Value, 21));
        cut.Render(p => p.Add(x => x.Value, 22));

        Assert.True(states.Count >= 3);
        Assert.All(states, state => Assert.Same(states[0], state));
    }
}

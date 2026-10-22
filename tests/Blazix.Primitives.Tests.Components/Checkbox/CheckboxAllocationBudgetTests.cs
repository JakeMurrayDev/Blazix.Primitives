using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 for Checkbox: a re-render of a root with unchanged parameters (the parent passing the same values again)
/// and an indicator status report that changes nothing, both measured around bUnit's synchronous work on the test thread.
/// The re-render includes Blazor's own per-render cost and the twins' comparison of their options, which sends nothing;
/// the report includes the parse of its JSON and the comparison with the mirrored status. The budgets were taken from the
/// first measurement and only tighten afterwards.
/// </summary>
public sealed class CheckboxAllocationBudgetTests : IDisposable
{
    // First measurement 2026-10-23 (arm64, .NET 11 RC2, Release): 672 bytes for the re-render, the same as Switch's (the
    // indeterminate state adds a field, not an allocation); 328 bytes for the status report, the source-generated parse of
    // its JSON, which the comparison with the mirrored status then discards without a render.
    private const long RerenderBudgetBytes = 672;
    private const long UnchangedStatusBudgetBytes = 328;

    private readonly CheckboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "a re-render of a checkbox with unchanged parameters stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_rerender_of_a_checkbox_with_unchanged_parameters_stays_within_its_budget()
    {
        EventCallback<MouseEventArgs> onClick = EventCallback.Factory.Create<MouseEventArgs>(this, () => { });
        RenderFragment content = builder => builder.AddContent(0, "x");
        IRenderedComponent<CheckboxRoot> cut = test.Context.Render<CheckboxRoot>(p => p
            .Add(x => x.Name, "n")
            .AddUnmatched("class", "consumer")
            .AddUnmatched("onclick", onClick)
            .AddChildContent(content));

        for (int i = 0; i < 20; i++)
        {
            cut.Render();
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        cut.Render();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated <= RerenderBudgetBytes, $"a re-render allocated {allocated} bytes; the budget is {RerenderBudgetBytes}");
    }

    [Fact(DisplayName = "an indicator status report that changes nothing stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public async Task An_indicator_status_report_that_changes_nothing_stays_within_its_budget()
    {
        IRenderedComponent<CheckboxRoot> cut = test.Context.Render<CheckboxRoot>(p => p
            .Add(x => x.DefaultChecked, true)
            .AddChildContent<CheckboxIndicator>());
        TransitionsTwin twin = test.TransitionsTwin();
        const string unchanged = """{"status":null,"mounted":true}""";
        for (int i = 0; i < 20; i++)
        {
            await cut.InvokeAsync(() => twin.OnStatusChange(unchanged));
        }

        // bUnit's dispatcher runs the call inline, so the measurement stays on the test thread.
        long before = GC.GetAllocatedBytesForCurrentThread();
        Task reported = cut.InvokeAsync(() => twin.OnStatusChange(unchanged));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        await reported;

        Assert.True(allocated <= UnchangedStatusBudgetBytes, $"a status report allocated {allocated} bytes; the budget is {UnchangedStatusBudgetBytes}");
    }
}

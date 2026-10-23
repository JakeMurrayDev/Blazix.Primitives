using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 for Switch: a re-render with unchanged parameters (the parent passing the same values again), and a
/// toggle committed with no consumer callback, both measured around bUnit's synchronous work on the test thread. The
/// re-render includes Blazor's own per-render cost and the twins' comparison of their options, which sends nothing; the
/// toggle includes the parse of the module's report, the request, the render it causes and the update the dom twin sends.
/// The budgets were taken from the first measurement and only tighten afterwards.
/// </summary>
public sealed class SwitchAllocationBudgetTests : IDisposable
{
    // First measurement 2026-10-22 (arm64, .NET 11 RC2, Release): 672 bytes for the re-render, against Button's 560 with
    // a click and a key handler (the rest is the fixed cascade's re-set, about 80 bytes, Meter's measurement); 9,120 bytes
    // for the toggle, most of it the source-generated parse of the report's JSON and the request's async state machine.
    // Raised 2026-10-23 with the field integration (issue 80's follow-up): 704 bytes for the re-render, the 32 of the four
    // cascading parameters the field behavior reads (Checkbox measures the same 704), and 9,272 bytes for the toggle, the
    // field behavior's value reaction outside a field (the form's error lookup and the edit-context check).
    private const long RerenderBudgetBytes = 704;
    private const long ToggleBudgetBytes = 9272;

    private readonly SwitchTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "a re-render of a switch with unchanged parameters stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_rerender_of_a_switch_with_unchanged_parameters_stays_within_its_budget()
    {
        EventCallback<MouseEventArgs> onClick = EventCallback.Factory.Create<MouseEventArgs>(this, () => { });
        RenderFragment content = builder => builder.AddContent(0, "x");
        IRenderedComponent<SwitchRoot> cut = test.Context.Render<SwitchRoot>(p => p
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

    [Fact(DisplayName = "a toggle committed without a consumer callback stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public async Task A_toggle_committed_without_a_consumer_callback_stays_within_its_budget()
    {
        IRenderedComponent<SwitchRoot> cut = test.Context.Render<SwitchRoot>();
        ButtonTwin twin = test.ButtonTwin();
        string on = $$"""{"checked":true,"event":{{SwitchTestContext.InputClickJson()}}}""";
        string off = $$"""{"checked":false,"event":{{SwitchTestContext.InputClickJson()}}}""";
        for (int i = 0; i < 20; i++)
        {
            string payload = i % 2 == 0 ? on : off;
            await cut.InvokeAsync(() => twin.OnToggle(payload));
        }

        // bUnit's dispatcher runs the call inline, so the measurement stays on the test thread. The steady state is the
        // least of five toggles: bUnit's own invocation lists grow by doubling, which lands on whichever toggle crosses a
        // capacity.
        long allocated = long.MaxValue;
        for (int i = 0; i < 5; i++)
        {
            string payload = i % 2 == 0 ? on : off;
            long before = GC.GetAllocatedBytesForCurrentThread();
            Task toggled = cut.InvokeAsync(() => twin.OnToggle(payload));
            allocated = Math.Min(allocated, GC.GetAllocatedBytesForCurrentThread() - before);
            await toggled;
        }

        Assert.Equal("true", cut.Find("[role=switch]").GetAttribute("aria-checked"));
        Assert.True(allocated <= ToggleBudgetBytes, $"a toggle allocated {allocated} bytes; the budget is {ToggleBudgetBytes}");
    }
}

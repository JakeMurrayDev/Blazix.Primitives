using System.Globalization;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 for NumberField (issue 277, decision 26 (a), chosen by the user 2026-11-18): a re-render of a root with
/// unchanged parameters, which also sends nothing to its modules; a hold tick's C# path (the parse of the module's report,
/// the request, the format and the render); and a click step, which adds the dirty-text sync and the comparison before the
/// commit. Each figure is the least of five runs after a warm-up (AllocationSampler's rule), measured on the test thread.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class NumberFieldAllocationBudgetTests : IDisposable
{
    // First measurement 2026-11-18 (arm64, .NET 11, Release, dynamic PGO off), the least of five in a process of its own, the
    // same in each of five processes: 848 bytes for the re-render of the root, its input and its stepper (Switch's root alone
    // is 704); 15,504 bytes for a hold tick and 15,488 for a click step, most of it the source-generated parse of the
    // report's JSON, the request's async state machines and the three parts' new state records. Re-pinned 2026-11-19, before
    // the budget reached main, when the input attached the `number-field` module (design point 2 (b)): 912 bytes for the
    // re-render (the input's twin comparison and its two declined handlers, +64), and 2,472 and 2,456 bytes for the tick and
    // the click, the same in five processes. The two steps fell by about 13 KB when the input's text moved to the module's
    // options; the cause of the size of that drop is not yet analysed (issue 277's hand-over).
    private const long RerenderBudgetBytes = 912;
    private const long HoldTickBudgetBytes = 2472;
    private const long ClickStepBudgetBytes = 2456;

    private static readonly CultureInfo s_enUs = CultureInfo.GetCultureInfo("en-US");

    private readonly NumberFieldTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "a re-render of a number field with unchanged parameters sends nothing and stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    [Trait("Requirement", "NUM-I18N-13")]
    public void A_rerender_with_unchanged_parameters_sends_nothing_and_stays_within_its_budget()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render();
        for (int i = 0; i < 20; i++)
        {
            cut.Render();
        }

        int buttonUpdates = test.ButtonHandle.Invocations["update"].Count;
        int formUpdates = test.FormHandle.Invocations["update"].Count;
        long allocated = long.MaxValue;
        for (int i = 0; i < 5; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            cut.Render();
            allocated = Math.Min(allocated, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        Assert.Equal(buttonUpdates, test.ButtonHandle.Invocations["update"].Count);
        Assert.Equal(formUpdates, test.FormHandle.Invocations["update"].Count);
        Assert.True(allocated <= RerenderBudgetBytes, $"a re-render allocated {allocated} bytes; the budget is {RerenderBudgetBytes}");
    }

    [Fact(DisplayName = "a hold tick's step stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    [Trait("Requirement", "NUM-PTR-03")]
    public async Task A_hold_ticks_step_stays_within_its_budget()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render();
        ButtonTwin twin = test.ButtonTwin(cut.FindComponent<NumberFieldIncrement>().Instance);
        string tick = $$"""{"event":{{NumberFieldTestContext.EventJson("pointerdown")}}}""";
        for (int i = 0; i < 20; i++)
        {
            await cut.InvokeAsync(() => twin.OnHoldTick(tick));
        }

        long allocated = long.MaxValue;
        for (int i = 0; i < 5; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            Task ticked = cut.InvokeAsync(() => twin.OnHoldTick(tick));
            allocated = Math.Min(allocated, GC.GetAllocatedBytesForCurrentThread() - before);
            await ticked;
        }

        Assert.Equal("25", test.Text);
        Assert.True(allocated <= HoldTickBudgetBytes, $"a hold tick allocated {allocated} bytes; the budget is {HoldTickBudgetBytes}");
    }

    [Fact(DisplayName = "a click step through the formatter stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    [Trait("Requirement", "NUM-PTR-01")]
    public async Task A_click_step_through_the_formatter_stays_within_its_budget()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render();
        ButtonTwin twin = test.ButtonTwin(cut.FindComponent<NumberFieldIncrement>().Instance);
        string click = $$"""{"event":{{NumberFieldTestContext.EventJson("click", "", 0)}}}""";
        for (int i = 0; i < 20; i++)
        {
            await cut.InvokeAsync(() => twin.OnHoldClick(click));
        }

        long allocated = long.MaxValue;
        for (int i = 0; i < 5; i++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            Task clicked = cut.InvokeAsync(() => twin.OnHoldClick(click));
            allocated = Math.Min(allocated, GC.GetAllocatedBytesForCurrentThread() - before);
            await clicked;
        }

        Assert.Equal("25", test.Text);
        Assert.True(allocated <= ClickStepBudgetBytes, $"a click step allocated {allocated} bytes; the budget is {ClickStepBudgetBytes}");
    }

    private IRenderedComponent<NumberFieldRoot> Render()
    {
        RenderFragment parts = b =>
        {
            b.OpenComponent<NumberFieldInput>(0);
            b.CloseComponent();
            b.OpenComponent<NumberFieldIncrement>(1);
            b.CloseComponent();
        };

        return test.Context.Render<NumberFieldRoot>(p => p
            .Add(x => x.Culture, s_enUs)
            .Add(x => x.DefaultValue, 0)
            .Add(x => x.Name, "quantity")
            .Add(x => x.ChildContent, parts));
    }
}

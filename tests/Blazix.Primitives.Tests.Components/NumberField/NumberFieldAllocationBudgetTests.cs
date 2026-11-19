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
    // the click, the same in five processes. Re-pinned again the same day, still before main, with the steppers' hold-start
    // veto (design point 1 (a)): +32 bytes each, 944, 2,504 and 2,488, the floor in three processes after main's merge.
    // The 13 KB drop of the two steps, analysed 2026-11-19: it is bUnit's markup upkeep, not the library's path. Before the
    // handover the input rendered its `value` from C#, so each step's batch carried an attribute diff and bUnit rebuilt the
    // markup (GC allocation-tick samples over 3,000 ticks: char[], String and StringBuilder, 339 of about 390). With only that
    // attribute frozen after the first render at f4be6621, the tick fell from 15,536 to 2,288 bytes and those types vanished.
    // The step still runs inside the measured region: the tick completes synchronously within InvokeAsync, and the module's
    // `update` with the new text is recorded before the region ends (scratch probe, not committed).
    // The hold tick's first value on main is CI's Linux x64 figure, 32 bytes over macOS arm64's: two of three CI runs of
    // 41411694 measured exactly 2,536 against macOS's 2,504 (issue 304, the budgets' tolerance across hosts). The re-render
    // and the click step passed on CI at their macOS figures. Then main's efb420c1 (SLD-EVT-04) gave EventTargetInfo two
    // reference fields, 16 bytes more per snapshot target, which both steps deserialize: on macOS the click went from 2,488
    // to 2,504 and the tick from 2,504 to 2,520 (the floor in three processes, 2026-11-19); the re-render is unchanged.
    // The tick's 2,552 is CI's 2,536 plus that measured 16, not yet measured on Linux.
    // Raised 2026-11-19 in 6b (ADR 16 Amendments 2026-11-19, chosen by the user 2026-11-19): the click step from 2,504 to
    // 2,512 bytes. The input role's gate travels as the nested `input` part of `NumberFieldOptions`, one reference more on
    // the record (40 to 48 bytes), which every step's `update` allocates; 2,512 in three processes on macOS arm64. The tick
    // (2,528) stays within its pin and the re-render is unchanged. CI's Linux figure is the one that counts (issue 304).
    private const long RerenderBudgetBytes = 944;
    private const long HoldTickBudgetBytes = 2552;
    private const long ClickStepBudgetBytes = 2512;

    // 6b (issue 277, decision 26 (a)): a keystroke's report through the parse and the change request, the input's text
    // answered with the echoed revision. First measurement 2026-11-19 (arm64, .NET 11, Release, dynamic PGO off), the least
    // of five, the same in three processes: 2,824 bytes. CI's Linux figure re-pins it (issue 304).
    private const long KeystrokeBudgetBytes = 2824;

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
        AllocationSampler.AssertWithinBudget(allocated, RerenderBudgetBytes, "a re-render");
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
        AllocationSampler.AssertWithinBudget(allocated, HoldTickBudgetBytes, "a hold tick");
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
        AllocationSampler.AssertWithinBudget(allocated, ClickStepBudgetBytes, "a click step");
    }

    [Fact(DisplayName = "a keystroke through the parser stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    [Trait("Requirement", "NUM-EVT-15")]
    public async Task A_keystroke_through_the_parser_stays_within_its_budget()
    {
        IRenderedComponent<NumberFieldRoot> cut = Render();
        NumberFieldTwin twin = test.NumberFieldTwin();
        string[] texts = [.. Enumerable.Range(0, 25).Select(i => $$$"""{"text":"1,2{{{i % 10}}}{{{i / 10}}}","revision":{{{i + 1}}},"event":{"kind":"InputEvent","type":"input"}}""")];
        for (int i = 0; i < 20; i++)
        {
            await cut.InvokeAsync(() => twin.OnInputChange(texts[i]));
        }

        long allocated = long.MaxValue;
        for (int i = 20; i < 25; i++)
        {
            string report = texts[i];
            long before = GC.GetAllocatedBytesForCurrentThread();
            Task changed = cut.InvokeAsync(() => twin.OnInputChange(report));
            allocated = Math.Min(allocated, GC.GetAllocatedBytesForCurrentThread() - before);
            await changed;
        }

        Assert.Equal(1242, cut.Instance.CurrentValue);
        AllocationSampler.AssertWithinBudget(allocated, KeystrokeBudgetBytes, "a keystroke");
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

using Blazix.Primitives.Interop;
using Bunit;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// ADR 16 rule 7 for Combobox (M5 track 10; the brief's L2 plan and Deliverable 14), each the least of five runs after a
// warm-up, measured around bUnit's synchronous render on the test thread: a re-render of an open root with unchanged
// parameters over 1,000 items, which sends nothing to any module; and a keystroke over 1,000 virtualized items with a window
// of 20 (issue 29's budget on CoreCLR; its WebAssembly half is an L4 measurement).
/// <summary>Combobox's allocation budget at L2.</summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class ComboboxAllocationBudgetTests : IDisposable
{
    private const int Count = 1000;

    // First measurement 2026-11-11 (arm64, .NET 11, Release; M5 track 10): the envelope of five runs of this class, each the
    // least of five, plus the CI runner's +112 bytes floor shift (InteractionsAllocationBudgetTests, 2026-10-29). The
    // re-render is the root and its 1,000 items with their text (878432 bytes in each of the five); the keystroke filters
    // 1,000 items and renders the window of 20 (294864 bytes in each of the five). They only tighten.
    private const long RerenderBudget = 878544;

    private const long KeystrokeBudget = 294976;

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: at 1,000 items, an unchanged root re-render sends nothing and stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void An_unchanged_root_rerender_sends_nothing_and_stays_within_its_budget()
    {
        IReadOnlyList<string> items = [.. Enumerable.Range(0, Count).Select(i => "v" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))];
        IRenderedComponent<ComboboxHarness> cut = test.Context.Render<ComboboxHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Items"] = items, ["DefaultOpen"] = true }));
        Assert.Equal(Count, cut.FindAll("[role=option]").Count);

        for (int i = 0; i < 20; i++)
        {
            cut.Render();
        }

        int updatesBefore = Updates();
        long rerender = AllocationSampler.LeastOfFive(() => cut.Render());

        Assert.Equal(updatesBefore, Updates());
        Assert.True(rerender <= RerenderBudget, $"a re-render allocated {rerender} bytes; the budget is {RerenderBudget}");
    }

    [Fact(DisplayName = "port: a keystroke over 1,000 virtualized items with a window of 20 stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    [Trait("Requirement", "CMB-API-22")]
    public async Task A_keystroke_over_1000_virtualized_items_stays_within_its_budget()
    {
        IReadOnlyList<string> items = [.. Enumerable.Range(0, Count).Select(i => "v" + i.ToString(System.Globalization.CultureInfo.InvariantCulture))];
        IRenderedComponent<ComboboxWindowHost> cut = test.Context.Render<ComboboxWindowHost>(p => p.Add(x => x.Items, items));
        Assert.Equal(20, cut.FindAll("[role=option]").Count);

        // Two queries of the same size (111 matches each), alternated so every run changes the filter.
        string[] queries = ["v1", "v2"];
        ComboboxTwin twin = test.TwinOf(ComboboxTwin.InputPart);
        string[] reports = [.. queries.Select(query => $$"""{"text":"{{query}}","typed":true,"composing":false,"event":{{ComboboxTestContext.InputEvent}}}""")];
        for (int i = 0; i < 20; i++)
        {
            await cut.InvokeAsync(() => twin.OnInputChange(reports[i % 2]));
        }

        long keystroke = AllocationSampler.LeastOfFive(run => cut.InvokeAsync(() => twin.OnInputChange(reports[run % 2])).GetAwaiter().GetResult());

        Assert.Equal(20, cut.FindAll("[role=option]").Count);
        Assert.True(keystroke <= KeystrokeBudget, $"a keystroke allocated {keystroke} bytes; the budget is {KeystrokeBudget}");
    }

    private int Updates() =>
        test.ComboboxHandle.Invocations["update"].Count + test.CompositeHandle.Invocations["update"].Count
        + test.InteractionsHandle.Invocations["update"].Count + test.FormHandle.Invocations["update"].Count;
}

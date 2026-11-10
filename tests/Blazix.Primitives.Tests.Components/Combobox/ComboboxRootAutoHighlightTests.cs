using Bunit;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// CMB-API-17, STA-24, STA-26 and STA-27 at L2 (ComboboxRoot.test.tsx, describe "prop: autoHighlight"): typing highlights the
// first match, a cleared query keeps the highlight, the highlight callback follows the filtered list, and Enter's selection
// clears the highlight once. The highlight is the store's active index (ComboboxRootBatchCSteps); the input's
// aria-activedescendant is the module's, written from it.
/// <summary>ComboboxRoot's autoHighlight at L2.</summary>
public sealed class ComboboxRootAutoHighlightTests : IDisposable
{
    private static readonly string[] s_fruits = ["apple", "banana", "cherry"];

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "does not auto-highlight on initial open when no selection")]
    [Trait("Requirement", "CMB-API-17")]
    public void Does_not_auto_highlight_on_initial_open_when_no_selection()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Items"] = (IReadOnlyList<string>)s_fruits, ["AutoHighlight"] = true, ["DefaultOpen"] = true });

        Assert.NotEmpty(cut.FindAll("[role=listbox]"));
        Assert.Null(Store(cut).State.ActiveIndex);
        Assert.All(test.HighlightRequests(), request => Assert.Null(request));
    }

    [Fact(DisplayName = "highlights the first matching item after typing (single mode)")]
    [Trait("Requirement", "CMB-API-17")]
    public async Task Highlights_the_first_matching_item_after_typing_single_mode()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Items"] = (IReadOnlyList<string>)s_fruits, ["AutoHighlight"] = true });

        await test.TypeEachAsync(cut, "ch");

        Assert.Equal("cherry", cut.HighlightedText(Store(cut)));
        Assert.Equal(0, test.HighlightRequests()[^1]);
    }

    [Fact(DisplayName = "highlights the first matching item for a static list without the items prop")]
    [Trait("Requirement", "CMB-API-17")]
    public async Task Highlights_the_first_matching_item_for_a_static_list_without_the_items_prop()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["AutoHighlight"] = true }, staticItems: ["alpha", "alphabet", "beta"]);

        await test.InputAsync(cut, "a");
        await test.ReportItemsAsync(cut);
        await test.InputAsync(cut, "al");
        Assert.Equal("alpha", cut.HighlightedText(Store(cut)));

        await test.InputAsync(cut, "al ");

        Assert.Equal("alpha", cut.HighlightedText(Store(cut)));
    }

    [Fact(DisplayName = "does not run follow-up state changes after canceled input value changes")]
    [Trait("Requirement", "CMB-API-11")]
    [Trait("Requirement", "CMB-EVT-07")]
    public async Task Does_not_run_follow_up_state_changes_after_canceled_input_value_changes()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["DefaultValue"] = "apple", ["OpenOnInputClick"] = false }, staticItems: ["apple", "banana"]);
        cut.Instance.OnInput = args => args.Details.Cancel();

        await test.InputAsync(cut, string.Empty);

        Assert.NotEmpty(cut.Instance.InputChanges);
        Assert.Empty(cut.Instance.ValueChanges);
        Assert.Empty(cut.Instance.OpenChanges);
        Assert.Empty(cut.FindAll("[role=listbox]"));

        cut.Instance.InputChanges.Clear();
        await test.InputAsync(cut, "b");

        Assert.NotEmpty(cut.Instance.InputChanges);
        Assert.Empty(cut.Instance.OpenChanges);
        Assert.Empty(cut.FindAll("[role=listbox]"));
    }

    [Fact(DisplayName = "retains highlight when query is cleared back to empty")]
    [Trait("Requirement", "CMB-API-17")]
    [Trait("Requirement", "CMB-STA-26")]
    public async Task Retains_highlight_when_query_is_cleared_back_to_empty()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Items"] = (IReadOnlyList<string>)s_fruits, ["AutoHighlight"] = true });
        await test.TypeEachAsync(cut, "a");
        Assert.NotEmpty(cut.FindAll("[role=listbox]"));
        Assert.NotNull(Store(cut).State.ActiveIndex);

        await test.InputAsync(cut, string.Empty);

        Assert.NotEmpty(cut.FindAll("[role=listbox]"));
        Assert.NotNull(Store(cut).State.ActiveIndex);
    }

    [Fact(DisplayName = "retains highlight when clearing the query with input-change behavior")]
    [Trait("Requirement", "CMB-STA-26")]
    public async Task Retains_highlight_when_clearing_the_query_with_input_change_behavior()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Items"] = (IReadOnlyList<string>)s_fruits, ["AutoHighlight"] = true });
        await test.PressAsync(cut, "[data-testid=input]", "input-press");
        Assert.NotEmpty(cut.FindAll("[role=listbox]"));

        await test.TypeEachAsync(cut, "ban");
        Assert.NotNull(cut.OptionNamed("banana"));
        Assert.NotNull(Store(cut).State.ActiveIndex);

        await test.InputAsync(cut, string.Empty);

        Assert.NotEmpty(cut.FindAll("[role=listbox]"));
        Assert.NotNull(Store(cut).State.ActiveIndex);
    }

    [Fact(DisplayName = "highlights the first matching item after typing (multiple mode)")]
    [Trait("Requirement", "CMB-API-17")]
    public async Task Highlights_the_first_matching_item_after_typing_multiple_mode()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Items"] = (IReadOnlyList<string>)s_fruits, ["Multiple"] = true, ["AutoHighlight"] = true });

        await test.TypeEachAsync(cut, "ba");

        Assert.NotEmpty(cut.FindAll("[role=listbox]"));
        Assert.Equal("banana", cut.HighlightedText(Store(cut)));
    }

    [Fact(DisplayName = "updates highlighted callback with newly filtered first item")]
    [Trait("Requirement", "CMB-STA-24")]
    [Trait("Requirement", "CMB-STA-27")]
    public async Task Updates_highlighted_callback_with_newly_filtered_first_item()
    {
        List<(string? Value, int Index, EventReason Reason)> highlights = [];
        IRenderedComponent<ComboboxHarness> cut = Render(new()
        {
            ["Items"] = (IReadOnlyList<string>)["banana", "apple", "apricot"],
            ["AutoHighlight"] = true,
            ["DefaultOpen"] = true,
            ["OnItemHighlighted"] = Recorder(highlights),
        });
        Assert.NotEmpty(cut.FindAll("[role=listbox]"));
        await test.PressAsync(cut, "[data-testid=input]", "input-press");
        await test.HighlightAsync(cut, 0, ComboboxRootBatchCSteps.ArrowDown);
        Assert.NotEmpty(highlights);
        Assert.Equal("banana", highlights[^1].Value);
        highlights.Clear();

        await test.TypeEachAsync(cut, "ap");

        Assert.NotEmpty(highlights);
        Assert.Equal(("apple", 0, EventReason.None), highlights[^1]);
    }

    [Fact(DisplayName = "fires a single clearing highlight on Enter selection")]
    [Trait("Requirement", "CMB-STA-24")]
    public async Task Fires_a_single_clearing_highlight_on_Enter_selection()
    {
        List<(string? Value, int Index, EventReason Reason)> highlights = [];
        IRenderedComponent<ComboboxHarness> cut = Render(new()
        {
            ["Items"] = (IReadOnlyList<string>)["Apple", "Apricot", "Banana"],
            ["AutoHighlight"] = true,
            ["OnItemHighlighted"] = Recorder(highlights),
        });
        await test.PressAsync(cut, "[data-testid=input]", "input-press");
        Assert.NotEmpty(cut.FindAll("[role=listbox]"));
        await test.TypeEachAsync(cut, "app");
        Assert.Equal("Apple", cut.HighlightedText(Store(cut)));
        highlights.Clear();

        await test.EnterAsync(cut, Store(cut));
        await test.CompleteCloseAsync(cut, Store(cut));

        Assert.Equal(["Apple"], cut.Instance.ValueChanges.Select(change => change.Value));
        int clearing = Assert.Single(Enumerable.Range(0, highlights.Count), i => highlights[i].Value is null);
        Assert.All(highlights.Skip(clearing + 1), call => Assert.Null(call.Value));
    }

    private static ComboboxStore Store(IRenderedComponent<ComboboxHarness> cut) => cut.Instance.Root!.Store;

    private EventCallback<ComboboxItemHighlightedEventArgs<string>> Recorder(List<(string? Value, int Index, EventReason Reason)> highlights) =>
        EventCallback.Factory.Create<ComboboxItemHighlightedEventArgs<string>>(this, args => highlights.Add((args.Value, args.Details.Index, args.Details.Reason)));

    private IRenderedComponent<ComboboxHarness> Render(Dictionary<string, object?> root, IReadOnlyList<string>? staticItems = null) =>
        test.Context.Render<ComboboxHarness>(p => p.Add(x => x.RootParameters, root).Add(x => x.StaticItems, staticItems));
}

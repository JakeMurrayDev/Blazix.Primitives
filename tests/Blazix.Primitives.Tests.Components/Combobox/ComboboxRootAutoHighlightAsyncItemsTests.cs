using Bunit;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// CMB-STA-26 at L2 (ComboboxRoot.test.tsx, describe "prop: autoHighlight > asynchronous items"): a typed query's pending
// highlight waits for candidates and for a navigable list, and is discarded by the events Base UI names. The highlight is the
// store's active index (ComboboxRootBatchCSteps): the module renders data-highlighted and aria-activedescendant from it.
// The title of line 9399 (a controlled close interrupted during its exit animation) is skipIf(isJSDOM), so L4.
/// <summary>ComboboxRoot's autoHighlight over items that arrive after typing, at L2.</summary>
public sealed class ComboboxRootAutoHighlightAsyncItemsTests : IDisposable
{
    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Theory(DisplayName = "highlights a candidate arriving after typing with %s")]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Requirement", "CMB-STA-26")]
    public async Task Highlights_a_candidate_arriving_after_typing(bool filteredItems)
    {
        IRenderedComponent<ComboboxAsyncHost> cut = Render([], filteredItems);
        await test.TypeEachAsync(cut, "32");
        cut.Instance.Highlights.Clear();

        cut.Render(p => p.Add(x => x.Items, ["32", "320"]).Add(x => x.UseFilteredItems, filteredItems));
        await test.EchoAsync(cut);

        Assert.Equal("32", cut.HighlightedText(Store(cut)));
        Assert.Equal(0, test.HighlightRequestIndexes()[^1]);
        (string? value, int index, EventReason reason) = Assert.Single(cut.Instance.Highlights);
        Assert.Equal(("32", 0, EventReason.None), (value, index, reason));

        await test.EnterAsync(cut, Store(cut));
        Assert.Equal(["32"], cut.Instance.ValueChanges);
    }

    [Fact(DisplayName = "waits for a controlled popup to open")]
    [Trait("Requirement", "CMB-STA-26")]
    public async Task Waits_for_a_controlled_popup_to_open()
    {
        IRenderedComponent<ComboboxAsyncHost> cut = Render(["32"], root: new() { ["Open"] = false });
        await test.TypeEachAsync(cut, "32");

        cut.Render(p => p.Add(x => x.RootParameters, new Dictionary<string, object?> { ["Open"] = true }));
        await test.EchoAsync(cut);

        Assert.Equal("32", cut.HighlightedText(Store(cut)));
    }

    [Fact(DisplayName = "discards the request when a rejected typed open is followed by a controlled open")]
    [Trait("Requirement", "CMB-STA-26")]
    [Trait("Requirement", "CMB-EVT-07")]
    public async Task Discards_the_request_when_a_rejected_typed_open_is_followed_by_a_controlled_open()
    {
        IRenderedComponent<ComboboxAsyncHost> cut = Render(["32"], root: new() { ["Open"] = false });
        cut.Instance.OnOpen = CancelTypedOpen;
        await test.TypeEachAsync(cut, "32");

        cut.Render(p => p.Add(x => x.RootParameters, new Dictionary<string, object?> { ["Open"] = true }));
        await test.EchoAsync(cut);

        Assert.Single(cut.FindAll("[role=option]"));
        Assert.Null(cut.HighlightedText(Store(cut)));
    }

    [Fact(DisplayName = "keeps a hovered item highlighted when individually rendered items arrive")]
    [Trait("Requirement", "CMB-STA-26")]
    public async Task Keeps_a_hovered_item_highlighted_when_individually_rendered_items_arrive()
    {
        IRenderedComponent<ComboboxChildrenHost> cut = test.Context.Render<ComboboxChildrenHost>(p => p.Add(x => x.Results, []));
        await test.TypeEachAsync(cut, "a");
        cut.Render(p => p.Add(x => x.Results, ["a1", "a2", "a3"]));
        await test.EchoAsync(cut);
        await test.ReportItemsAsync(cut);

        await test.HighlightAsync(cut, 2, ComboboxRootBatchCSteps.MouseMove);

        Assert.Equal("a3", cut.HighlightedText(cut.Instance.Root!.Store));
    }

    [Fact(DisplayName = "highlights the first result when typing filters out individually rendered items")]
    [Trait("Requirement", "CMB-STA-26")]
    public async Task Highlights_the_first_result_when_typing_filters_out_individually_rendered_items()
    {
        IRenderedComponent<ComboboxChildrenHost> cut = test.Context.Render<ComboboxChildrenHost>(p => p
            .Add(x => x.Results, ["apple", "banana", "blackberry", "blueberry"])
            .Add(x => x.FilterByQuery, true));
        ComboboxStore store = cut.Instance.Root!.Store;
        await test.TypeEachAsync(cut, "b");
        await test.ReportItemsAsync(cut);
        await test.HighlightAsync(cut, 1, ComboboxRootBatchCSteps.ArrowDown);

        await test.TypeEachAsync(cut, "l", before: "b");
        await test.ReportItemsAsync(cut);

        Assert.Equal(["blackberry", "blueberry"], cut.OptionTexts());
        Assert.Equal("blackberry", cut.HighlightedText(store));
        Assert.Equal(0, test.HighlightRequestIndexes()[^1]);
    }

    [Fact(DisplayName = "discards the request when an ignored typed open is followed by another open")]
    [Trait("Requirement", "CMB-STA-26")]
    public async Task Discards_the_request_when_an_ignored_typed_open_is_followed_by_another_open()
    {
        IRenderedComponent<ComboboxAsyncHost> cut = Render(["32"], root: new() { ["Open"] = false });
        await test.TypeEachAsync(cut, "32");
        await test.PressAsync(cut, "[data-testid=trigger]", "trigger-press");
        Assert.Equal((true, EventReason.TriggerPress), cut.Instance.OpenChanges[^1]);

        cut.Render(p => p.Add(x => x.RootParameters, new Dictionary<string, object?> { ["Open"] = true }));
        await test.EchoAsync(cut);

        Assert.Single(cut.FindAll("[role=option]"));
        Assert.Null(cut.HighlightedText(Store(cut)));
    }

    // With ArrowDown the module highlights on open only when the list's focusItemOnOpen allows it (AriaCombobox.tsx:1425-1426):
    // the typed query sets queryChangedAfterOpen, so the options must say false.
    [Theory(DisplayName = "discards the request when a rejected typed open is followed by opening with %s")]
    [InlineData("Trigger")]
    [InlineData("ArrowDown")]
    [Trait("Requirement", "CMB-STA-26")]
    public async Task Discards_the_request_when_a_rejected_typed_open_is_followed_by_opening_with(string method)
    {
        IRenderedComponent<ComboboxAsyncHost> cut = Render(["32", "320"]);
        cut.Instance.OnOpen = CancelTypedOpen;
        await test.TypeEachAsync(cut, "32");
        Assert.Empty(cut.FindAll("[role=listbox]"));

        if (method == "Trigger")
        {
            await test.PressAsync(cut, "[data-testid=trigger]", "trigger-press");
        }
        else
        {
            await test.ListOpenAsync(cut);
            Assert.Contains("\"focusItemOnOpen\":false", test.LastListOptions(), StringComparison.Ordinal);
        }

        Assert.NotNull(cut.OptionNamed("32"));
        Assert.Null(cut.HighlightedText(Store(cut)));
        Assert.DoesNotContain(0, test.HighlightRequestIndexes());
    }

    [Fact(DisplayName = "discards the request when the controlled input is cleared")]
    [Trait("Requirement", "CMB-API-09")]
    [Trait("Requirement", "CMB-STA-26")]
    public async Task Discards_the_request_when_the_controlled_input_is_cleared()
    {
        IRenderedComponent<ComboboxAsyncHost> cut = test.Context.Render<ComboboxAsyncHost>(p => p.Add(x => x.ControlledInput, true));
        await test.TypeEachAsync(cut, "32");

        cut.Render(p => p.Add(x => x.ControlledInput, true).Add(x => x.ClearInput, true));
        await test.EchoAsync(cut);
        cut.Render(p => p.Add(x => x.ControlledInput, true).Add(x => x.ClearInput, true).Add(x => x.Items, ["32"]));
        await test.EchoAsync(cut);

        Assert.Equal(string.Empty, Store(cut).State.InputValue);
        Assert.Single(cut.FindAll("[role=option]"));
        Assert.Null(cut.HighlightedText(Store(cut)));
        await test.EnterAsync(cut, Store(cut));
        Assert.Empty(cut.Instance.ValueChanges);
    }

    [Fact(DisplayName = "does not reset keyboard navigation when candidates change")]
    [Trait("Requirement", "CMB-STA-26")]
    public async Task Does_not_reset_keyboard_navigation_when_candidates_change()
    {
        IRenderedComponent<ComboboxAsyncHost> cut = Render([]);
        await test.TypeEachAsync(cut, "32");
        cut.Render(p => p.Add(x => x.Items, ["32", "320"]));
        await test.EchoAsync(cut);
        await test.HighlightAsync(cut, 1, ComboboxRootBatchCSteps.ArrowDown);
        Assert.Equal("320", cut.HighlightedText(Store(cut)));

        cut.Render(p => p.Add(x => x.Items, ["321", "320"]));
        await test.EchoAsync(cut);

        Assert.Equal("320", cut.HighlightedText(Store(cut)));
    }

    [Fact(DisplayName = "discards the request when autoHighlight is disabled")]
    [Trait("Requirement", "CMB-STA-26")]
    public async Task Discards_the_request_when_autoHighlight_is_disabled()
    {
        IRenderedComponent<ComboboxAsyncHost> cut = Render([]);
        await test.TypeEachAsync(cut, "32");

        cut.Render(p => p.Add(x => x.RootParameters, new Dictionary<string, object?> { ["AutoHighlight"] = false }));
        await test.EchoAsync(cut);
        cut.Render(p => p.Add(x => x.RootParameters, new Dictionary<string, object?> { ["AutoHighlight"] = true }).Add(x => x.Items, ["32"]));
        await test.EchoAsync(cut);

        Assert.Single(cut.FindAll("[role=option]"));
        Assert.Null(cut.HighlightedText(Store(cut)));
    }

    private static void CancelTypedOpen(ValueChangeEventArgs<bool> args)
    {
        if (args.Value && args.Details.Reason == EventReason.InputChange)
        {
            args.Details.Cancel();
        }
    }

    private static ComboboxStore Store(IRenderedComponent<ComboboxAsyncHost> cut) => cut.Instance.Root!.Store;

    private IRenderedComponent<ComboboxAsyncHost> Render(IReadOnlyList<string> items, bool filteredItems = false, Dictionary<string, object?>? root = null) =>
        test.Context.Render<ComboboxAsyncHost>(p => p
            .Add(x => x.Items, items)
            .Add(x => x.UseFilteredItems, filteredItems)
            .Add(x => x.RootParameters, root ?? []));
}

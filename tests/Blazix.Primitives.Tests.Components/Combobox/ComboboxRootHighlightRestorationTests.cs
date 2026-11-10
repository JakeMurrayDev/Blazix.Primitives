using Bunit;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// CMB-STA-26 at L2 (ComboboxRoot.test.tsx, describe "highlight restoration when clearing the query"): clearing a typed query
// returns the highlight to the selected item, by the items' registry with or without `items`, and leaves none without a
// selection. The highlight is the store's active index (ComboboxRootBatchCSteps); the highlight the module shows on open is
// the store's selected index it sends.
/// <summary>ComboboxRoot's highlight restoration when the query is cleared, at L2.</summary>
public sealed class ComboboxRootHighlightRestorationTests : IDisposable
{
    private static readonly string[] s_fruits = ["apple", "banana", "cherry"];

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "returns the highlight to the selected item when the input is cleared")]
    [Trait("Requirement", "CMB-STA-26")]
    public async Task Returns_the_highlight_to_the_selected_item_when_the_input_is_cleared()
    {
        IRenderedComponent<ComboboxHarness> cut = RenderInside(new() { ["Items"] = (IReadOnlyList<string>)s_fruits, ["DefaultValue"] = "cherry" });
        await test.PressAsync(cut, "[data-testid=trigger]", "trigger-press");
        Assert.NotEmpty(cut.FindAll("[role=listbox]"));

        await test.TypeEachAsync(cut, "ap");
        Assert.NotNull(cut.OptionNamed("apple"));
        await test.InputAsync(cut, string.Empty);

        Assert.NotNull(cut.OptionNamed("cherry"));
        Assert.Equal("cherry", cut.HighlightedText(cut.Instance.Root!.Store));
    }

    [Fact(DisplayName = "returns the highlight to the selected item without the items prop")]
    [Trait("Requirement", "CMB-STA-26")]
    public async Task Returns_the_highlight_to_the_selected_item_without_the_items_prop()
    {
        IRenderedComponent<ComboboxHarness> cut = RenderInside(new() { ["DefaultValue"] = "cherry" }, staticItems: s_fruits);
        await test.PressAsync(cut, "[data-testid=trigger]", "trigger-press");
        Assert.NotEmpty(cut.FindAll("[role=listbox]"));
        await test.ReportItemsAsync(cut);

        await test.TypeEachAsync(cut, "ap");
        await test.ReportItemsAsync(cut);
        Assert.NotNull(cut.OptionNamed("apple"));
        await test.InputAsync(cut, string.Empty);
        await test.ReportItemsAsync(cut);

        Assert.NotNull(cut.OptionNamed("cherry"));
        Assert.Equal("cherry", cut.HighlightedText(cut.Instance.Root!.Store));
    }

    // The array is a single selection, compared by reference as Object.is does; the module highlights the selected index the
    // store sends when the popup opens.
    [Fact(DisplayName = "restores an array-valued single selection when the query is cleared")]
    [Trait("Requirement", "CMB-STA-26")]
    public async Task Restores_an_array_valued_single_selection_when_the_query_is_cleared()
    {
        int[][] items = [[1, 2], [3, 4], [5, 6]];
        Func<int[], string> join = item => string.Join("-", item);
        IRenderedComponent<ComboboxPopupInputHost<int[]>> cut = test.Context.Render<ComboboxPopupInputHost<int[]>>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?>
            {
                ["Items"] = (IReadOnlyList<int[]>)items,
                ["DefaultValue"] = items[1],
                ["ItemToStringLabel"] = join,
            })
            .Add(x => x.ItemText, join));
        ComboboxStore store = cut.Instance.Root!.Store;
        await test.PressAsync(cut, "[data-testid=trigger]", "trigger-press");
        Assert.NotEmpty(cut.FindAll("[role=listbox]"));
        Assert.Equal(1, store.State.SelectedIndex);
        Assert.Equal("3-4", cut.OptionTexts()[1]);

        await test.TypeEachAsync(cut, "1");
        Assert.NotNull(cut.OptionNamed("1-2"));
        await test.InputAsync(cut, string.Empty);

        Assert.NotNull(cut.OptionNamed("3-4"));
        Assert.Equal("3-4", cut.HighlightedText(store));
    }

    [Fact(DisplayName = "does not highlight anything when there is no selected item")]
    [Trait("Requirement", "CMB-STA-26")]
    public async Task Does_not_highlight_anything_when_there_is_no_selected_item()
    {
        IRenderedComponent<ComboboxHarness> cut = test.Context.Render<ComboboxHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Items"] = (IReadOnlyList<string>)s_fruits }));
        await test.PressAsync(cut, "[data-testid=input]", "input-press");
        Assert.NotEmpty(cut.FindAll("[role=listbox]"));

        await test.TypeEachAsync(cut, "ap");
        Assert.NotNull(cut.OptionNamed("apple"));
        await test.InputAsync(cut, string.Empty);

        Assert.NotNull(cut.OptionNamed("cherry"));
        Assert.Null(cut.Instance.Root!.Store.State.ActiveIndex);
    }

    private static RenderFragment Trigger => builder =>
    {
        builder.OpenComponent<ComboboxTrigger>(0);
        builder.AddComponentParameter(1, "data-testid", "trigger");
        builder.CloseComponent();
    };

    private IRenderedComponent<ComboboxHarness> RenderInside(Dictionary<string, object?> root, IReadOnlyList<string>? staticItems = null) =>
        test.Context.Render<ComboboxHarness>(p => p
            .Add(x => x.RootParameters, root)
            .Add(x => x.StaticItems, staticItems)
            .Add(x => x.InputInsidePopup, true)
            .Add(x => x.Extra, Trigger));
}

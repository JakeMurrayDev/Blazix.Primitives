using Bunit;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// ComboboxRoot.test.tsx's `prop: filteredItems` titles cited at L2. The automatic highlight is C#'s request, which reports
// onItemHighlighted from C#; the highlight of the selected item on open is the list navigation's, from the selected index
// C# sends it (CMP-STA-31), which it then reports back (CompositeTwin.OnListHighlight).
/// <summary>Combobox's root at L2: ComboboxRoot.test.tsx's <c>prop: filteredItems</c> block.</summary>
public sealed class ComboboxRootPropFilteredItemsTests : IDisposable
{
    private static readonly string[] s_fruits = ["Apple", "Banana", "Zucchini"];

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    // The component's setInput is the harness's own render after OnInputValueChange (the callback's receiver), with the
    // new input value and the filtered items its memo derives.
    [Fact(DisplayName = "highlights the externally filtered item order when filtering reorders items")]
    [Trait("Requirement", "CMB-API-13")]
    public async Task Highlights_the_externally_filtered_item_order_when_filtering_reorders_items()
    {
        Dictionary<string, object?> parameters = new() { ["AutoHighlight"] = true };
        void SetInput(string input)
        {
            parameters["InputValue"] = input;
            parameters["FilteredItems"] = input.Length > 0 ? (IReadOnlyList<string>)[.. Enumerable.Reverse(s_fruits)] : (IReadOnlyList<string>)s_fruits;
        }

        SetInput(string.Empty);
        IRenderedComponent<ComboboxRootBatchBHarness> cut = test.Context.Render<ComboboxRootBatchBHarness>(p => p.Add(x => x.RootParameters, parameters));
        cut.Instance.OnInput = args => SetInput(args.Value);

        await cut.PressInputAsync(test);
        Assert.True(cut.HasList());

        cut.Instance.Highlighted.Clear();

        await cut.TypeAsync(test, "a");

        Assert.NotEmpty(cut.Instance.Highlighted);
        Assert.Equal("Zucchini", cut.Instance.Highlighted[^1]);
    }

    [Fact(DisplayName = "opens a reordered external list at the selected value in rendered-list coordinates")]
    [Trait("Requirement", "CMB-STA-25")]
    public async Task Opens_a_reordered_external_list_at_the_selected_value_in_rendered_list_coordinates()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = test.Context.Render<ComboboxRootBatchBHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?>
            {
                ["Items"] = (IReadOnlyList<string>)["Apple", "Banana", "Cherry"],
                ["FilteredItems"] = (IReadOnlyList<string>)["Cherry", "Apple"],
                ["Multiple"] = true,
                ["DefaultValues"] = (IReadOnlyList<string>)["Apple"],
            }));

        await cut.PressInputAsync(test);

        // Apple is at 1 in the rendered list, Cherry first; the list navigation opens at the selected index it is sent.
        Assert.Equal(1, cut.Instance.Root!.Store.State.SelectedIndex);
        Assert.Contains("\"selectedIndex\":1", test.LastListNavigationOptions(), StringComparison.Ordinal);

        await cut.HighlightAsync(test, 1, "Apple", "null");

        Assert.Equal(1, cut.Instance.Root.Store.State.ActiveIndex);
        Assert.Equal("Apple", cut.Instance.Highlighted[^1]);
    }
}

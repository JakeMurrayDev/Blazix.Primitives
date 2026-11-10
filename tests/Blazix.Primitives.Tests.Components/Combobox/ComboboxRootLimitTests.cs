using Bunit;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// CMB-API-16 at L2 (ComboboxRoot.test.tsx, describe "prop: limit"): the limit caps the items rendered from `Items` and their
// groups, stops the filter once reached, leaves a selection outside it without a selected index, and does nothing without
// `Items`. The test's SelectedIndexProbe reads the store's selected index, as the store state here.
/// <summary>ComboboxRoot's Limit at L2.</summary>
public sealed class ComboboxRootLimitTests : IDisposable
{
    private static readonly string[] s_five = ["apple", "banana", "cherry", "date", "elderberry"];

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "keeps the selected index unset when the selected item is outside the rendered limit")]
    [Trait("Requirement", "CMB-API-16")]
    public async Task Keeps_the_selected_index_unset_when_the_selected_item_is_outside_the_rendered_limit()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Items"] = (IReadOnlyList<string>)s_five[..4], ["Limit"] = 2, ["DefaultValue"] = "date" });
        ComboboxStore store = cut.Instance.Root!.Store;
        Assert.Null(store.State.SelectedIndex);

        await test.PressAsync(cut, "[data-testid=input]", "input-press");

        Assert.Equal(2, cut.FindAll("[role=option]").Count);
        Assert.Null(store.State.SelectedIndex);
        Assert.Null(store.State.ActiveIndex);
    }

    [Fact(DisplayName = "limits the number of items displayed when no groups are used")]
    [Trait("Requirement", "CMB-API-16")]
    public void Limits_the_number_of_items_displayed_when_no_groups_are_used()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Items"] = (IReadOnlyList<string>)s_five, ["Limit"] = 3, ["DefaultOpen"] = true });

        Assert.Equal(["apple", "banana", "cherry"], cut.OptionTexts());
    }

    [Fact(DisplayName = "limits the number of items displayed when groups are used")]
    [Trait("Requirement", "CMB-API-16")]
    public void Limits_the_number_of_items_displayed_when_groups_are_used()
    {
        IRenderedComponent<ComboboxGroupsHost> cut = RenderGroups(
            4,
            new("citrus", ["orange", "lemon", "lime"]),
            new("berries", ["strawberry", "blueberry", "raspberry"]));

        Assert.Equal(["orange", "lemon", "lime", "strawberry"], cut.OptionTexts());
        Assert.Equal(["citrus", "berries"], cut.FindAll("[data-testid=group-label]").Select(label => label.TextContent));
    }

    [Fact(DisplayName = "preserves order of items when applying limit across groups")]
    [Trait("Requirement", "CMB-API-16")]
    public void Preserves_order_of_items_when_applying_limit_across_groups()
    {
        IRenderedComponent<ComboboxGroupsHost> cut = RenderGroups(3, new("groupA", ["A1", "A2"]), new("groupB", ["B1", "B2", "B3"]));

        Assert.Equal(["A1", "A2", "B1"], cut.OptionTexts());
    }

    [Fact(DisplayName = "respects limit when filtering items")]
    [Trait("Requirement", "CMB-API-16")]
    public async Task Respects_limit_when_filtering_items()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new()
        {
            ["Items"] = (IReadOnlyList<string>)["apple", "apricot", "avocado", "banana", "blueberry"],
            ["Limit"] = 2,
            ["DefaultOpen"] = true,
        });

        await test.TypeEachAsync(cut, "a");

        Assert.Equal(["apple", "apricot"], cut.OptionTexts());
    }

    [Fact(DisplayName = "stops filtering grouped items once the limit is reached")]
    [Trait("Requirement", "CMB-API-16")]
    public async Task Stops_filtering_grouped_items_once_the_limit_is_reached()
    {
        List<string> filtered = [];
        IRenderedComponent<ComboboxGroupsHost> cut = test.Context.Render<ComboboxGroupsHost>(p => p.Add(x => x.RootParameters, new Dictionary<string, object?>
        {
            ["ItemGroups"] = (IReadOnlyList<ComboboxItemGroup<string>>)
            [
                new("groupA", ["banana", "apple", "apricot", "avocado"]),
                new("groupB", ["artichoke", "banana", "blueberry"]),
            ],
            ["Filter"] = (Func<string, string, Func<string, string>?, bool>)((item, query, _) =>
            {
                filtered.Add(item);
                return item.StartsWith(query, StringComparison.Ordinal);
            }),
            ["Limit"] = 2,
            ["DefaultOpen"] = true,
        }));
        filtered.Clear();

        await test.TypeEachAsync(cut, "a");

        Assert.DoesNotContain("avocado", filtered);
        Assert.DoesNotContain("artichoke", filtered);
        Assert.Equal(["apple", "apricot"], cut.OptionTexts());
    }

    [Fact(DisplayName = "shows all items when limit is -1 (default)")]
    [Trait("Requirement", "CMB-API-16")]
    public void Shows_all_items_when_limit_is_minus_1()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Items"] = (IReadOnlyList<string>)s_five, ["Limit"] = -1, ["DefaultOpen"] = true });

        Assert.Equal(s_five, cut.OptionTexts());
    }

    [Fact(DisplayName = "handles limit of 0 gracefully")]
    [Trait("Requirement", "CMB-API-16")]
    public void Handles_limit_of_0_gracefully()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Items"] = (IReadOnlyList<string>)s_five[..3], ["Limit"] = 0, ["DefaultOpen"] = true });

        Assert.Empty(cut.FindAll("[role=option]"));
    }

    [Fact(DisplayName = "does not limit items when not using items prop")]
    [Trait("Requirement", "CMB-API-16")]
    public void Does_not_limit_items_when_not_using_items_prop()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Limit"] = 2, ["DefaultOpen"] = true }, staticItems: s_five);

        Assert.Equal(s_five, cut.OptionTexts());
    }

    [Fact(DisplayName = "updates displayed items when limit changes")]
    [Trait("Requirement", "CMB-API-16")]
    public void Updates_displayed_items_when_limit_changes()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Items"] = (IReadOnlyList<string>)s_five[..4], ["Limit"] = 2, ["DefaultOpen"] = true });
        Assert.Equal(["apple", "banana"], cut.OptionTexts());

        cut.Render(p => p.Add(x => x.RootParameters, new Dictionary<string, object?> { ["Items"] = (IReadOnlyList<string>)s_five[..4], ["Limit"] = 3, ["DefaultOpen"] = true }));

        Assert.Equal(["apple", "banana", "cherry"], cut.OptionTexts());
    }

    private IRenderedComponent<ComboboxGroupsHost> RenderGroups(int limit, params ComboboxItemGroup<string>[] groups) =>
        test.Context.Render<ComboboxGroupsHost>(p => p.Add(x => x.RootParameters, new Dictionary<string, object?>
        {
            ["ItemGroups"] = (IReadOnlyList<ComboboxItemGroup<string>>)groups,
            ["Limit"] = limit,
            ["DefaultOpen"] = true,
        }));

    private IRenderedComponent<ComboboxHarness> Render(Dictionary<string, object?> root, IReadOnlyList<string>? staticItems = null) =>
        test.Context.Render<ComboboxHarness>(p => p.Add(x => x.RootParameters, root).Add(x => x.StaticItems, staticItems));
}

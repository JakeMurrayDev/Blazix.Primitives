using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// CMB-API-32, EDGE-12 and CMB-API-46/47 at L2 (createItems.test.tsx and ComboboxValue.test.tsx's titles whose items label
// their values): ComboboxItems.Create's collections pass as the root's items with their derived values, label the selection
// from the collection first, then from a collection given as the filtered items, then ItemToStringLabel, then the value's
// text; the filter reads the collection labels; a null item's label wins over the Value part's placeholder.
/// <summary>Combobox's item collections at L2.</summary>
public sealed class ComboboxItemsTests : IDisposable
{
    private static readonly Fruit[] s_fruits = [new("a", "Apple"), new("b", "Banana"), new("c", "Cherry")];

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "uses the derived value for selection and resolves the label while closed")]
    [Trait("Requirement", "CMB-API-32")]
    public void Uses_the_derived_value_and_resolves_the_label_while_closed()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Items"] = Collection(), ["DefaultValue"] = "b" });

        Assert.Equal("Banana", cut.Find("[data-testid=input]").GetAttribute("value"));
        Assert.Equal("b", cut.Instance.Root!.Store.SelectedValue);
    }

    [Fact(DisplayName = "renders the selected label via Combobox.Value")]
    [Trait("Requirement", "CMB-API-46")]
    public void Renders_the_selected_label_via_Combobox_Value()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Items"] = Collection(), ["DefaultValue"] = "c" }, withValue: true);

        Assert.Equal("Cherry", cut.Find("[data-testid=value]").TextContent);
    }

    [Fact(DisplayName = "filters items by their derived labels")]
    [Trait("Requirement", "CMB-API-32")]
    public async Task Filters_items_by_their_derived_labels()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Items"] = Collection(), ["DefaultOpen"] = true });

        await test.TypeAsync(cut, "ban");

        Assert.Equal(["b"], cut.Instance.Root!.Store.FilteredValues);
    }

    [Fact(DisplayName = "keeps filtering on the collection label when itemToStringLabel is provided")]
    [Trait("Requirement", "CMB-API-32")]
    public async Task Keeps_filtering_on_the_collection_label()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new()
        {
            ["Items"] = Collection(),
            ["DefaultOpen"] = true,
            ["ItemToStringLabel"] = (Func<string, string>)(value => "id " + value),
        });

        await test.TypeAsync(cut, "cher");

        Assert.Equal(["c"], cut.Instance.Root!.Store.FilteredValues);
    }

    [Fact(DisplayName = "falls back to itemToStringLabel only for values the collection cannot resolve")]
    [Trait("Requirement", "CMB-API-32")]
    public void Falls_back_to_itemToStringLabel_only_outside_the_collection()
    {
        Func<string, string> toLabel = value => "label " + value;

        Assert.Equal("Apple", Render(new() { ["Items"] = Collection(), ["DefaultValue"] = "a", ["ItemToStringLabel"] = toLabel }).Find("[data-testid=input]").GetAttribute("value"));
        Assert.Equal("label zzz", Render(new() { ["Items"] = Collection(), ["DefaultValue"] = "zzz", ["ItemToStringLabel"] = toLabel }).Find("[data-testid=input]").GetAttribute("value"));
    }

    [Fact(DisplayName = "labels a selected value that is not in the collection as itself")]
    [Trait("Requirement", "CMB-EDGE-12")]
    public void Labels_a_value_outside_the_collection_as_itself()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Items"] = Collection(), ["DefaultValue"] = "zzz" });

        Assert.Equal("zzz", cut.Find("[data-testid=input]").GetAttribute("value"));
    }

    [Fact(DisplayName = "resolves a value that only a custom comparer matches from the collection")]
    [Trait("Requirement", "CMB-API-32")]
    public void Resolves_a_value_through_the_custom_comparer()
    {
        IRenderedComponent<ComboboxHarness> cut = Render(new()
        {
            ["Items"] = Collection(),
            ["DefaultValue"] = "A",
            ["IsItemEqualToValue"] = (Func<string, string, bool>)((item, value) => string.Equals(item, value, StringComparison.OrdinalIgnoreCase)),
        });

        Assert.Equal("Apple", cut.Find("[data-testid=input]").GetAttribute("value"));
    }

    [Fact(DisplayName = "labels a defaultValue whose item only exists in the externally filtered window")]
    [Trait("Requirement", "CMB-EDGE-12")]
    public void Labels_a_value_from_the_external_window()
    {
        IReadOnlyList<string> window = ComboboxItems.Create<Fruit, string>([new("x", "Xigua")], fruit => fruit.Id!, fruit => fruit.Name);

        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Items"] = Collection(), ["FilteredItems"] = window, ["DefaultValue"] = "x" });

        Assert.Equal("Xigua", cut.Find("[data-testid=input]").GetAttribute("value"));
    }

    [Fact(DisplayName = "renders null item label from items array when no value selected")]
    [Trait("Requirement", "CMB-API-46")]
    public void Renders_the_null_items_label_when_no_value_is_selected()
    {
        IReadOnlyList<string> items = ComboboxItems.Create<Fruit, string>([new(null, "None"), .. s_fruits], fruit => fruit.Id!, fruit => fruit.Name);

        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Items"] = items }, withValue: true);

        Assert.Equal("None", cut.Find("[data-testid=value]").TextContent);
    }

    [Fact(DisplayName = "null item label in items takes precedence over placeholder")]
    [Trait("Requirement", "CMB-API-47")]
    public void The_null_items_label_takes_precedence_over_the_placeholder()
    {
        IReadOnlyList<string> withNull = ComboboxItems.Create<Fruit, string>([new(null, "None"), .. s_fruits], fruit => fruit.Id!, fruit => fruit.Name);

        Assert.Equal("Pick one", Render(new() { ["Items"] = Collection() }, withValue: true, placeholder: "Pick one").Find("[data-testid=value]").TextContent);
        Assert.Equal("None", Render(new() { ["Items"] = withNull }, withValue: true, placeholder: "Pick one").Find("[data-testid=value]").TextContent);
    }

    // ComboboxValue.test.tsx: two items share a value; the first one's label wins (the collection reports the duplicate).
    [Fact(DisplayName = "handles duplicate values in items array (uses first match)")]
    [Trait("Requirement", "CMB-EDGE-08")]
    public void Handles_duplicate_values_in_items_array()
    {
        IReadOnlyList<string> items = ComboboxItems.Create<Fruit, string>([new("test", "First Label"), new("test", "Second Label"), new("other", "Other")], fruit => fruit.Id!, fruit => fruit.Name);

        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["Items"] = items, ["DefaultValue"] = "test" }, withValue: true);

        Assert.Equal("First Label", cut.Find("[data-testid=value]").TextContent);
    }

    [Fact(DisplayName = "handles null items in grouped structure")]
    [Trait("Requirement", "CMB-EDGE-05")]
    public void Handles_null_items_in_grouped_structure()
    {
        IReadOnlyList<ComboboxItemGroup<string>> groups = ComboboxItems.CreateGrouped<Fruit, string>([new("options", [new(null, "None selected"), new("option1", "Option 1")])], fruit => fruit.Id!, fruit => fruit.Name);

        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["ItemGroups"] = groups }, withValue: true);

        Assert.Equal("None selected", cut.Find("[data-testid=value]").TextContent);
    }

    [Fact(DisplayName = "handles grouped items correctly")]
    [Trait("Requirement", "CMB-API-46")]
    public void Handles_grouped_items()
    {
        IReadOnlyList<ComboboxItemGroup<string>> groups = ComboboxItems.CreateGrouped<Fruit, string>(
            [new("sweet", s_fruits[..2]) { Label = "Sweet" }, new("sour", s_fruits[2..])],
            fruit => fruit.Id!,
            fruit => fruit.Name);

        IRenderedComponent<ComboboxHarness> cut = Render(new() { ["ItemGroups"] = groups, ["DefaultValue"] = "c" }, withValue: true);

        Assert.Equal("Cherry", cut.Find("[data-testid=value]").TextContent);
        Assert.Equal(["a", "b"], groups[0].Items);
        Assert.Equal("Sweet", groups[0].Label);
    }

    [Fact(DisplayName = "does not call the accessors when creating a flat collection")]
    [Trait("Requirement", "CMB-API-32")]
    public void Does_not_call_the_accessors_when_creating()
    {
        int calls = 0;
        ComboboxItemCollection<Fruit, string> flat = ComboboxItems.Create<Fruit, string>(s_fruits, fruit => { calls++; return fruit.Id!; }, fruit => { calls++; return fruit.Name; });
        ComboboxItemGroupCollection<Fruit, string> grouped = ComboboxItems.CreateGrouped<Fruit, string>([new("all", s_fruits)], fruit => { calls++; return fruit.Id!; });

        Assert.Equal(0, calls);
        Assert.Equal(3, flat.Count);
        Assert.Single(grouped);
    }

    [Fact(DisplayName = "port: Get returns a value's source item and throws for an unknown value")]
    [Trait("Requirement", "CMB-API-32")]
    public void Get_returns_the_source_item()
    {
        ComboboxItemCollection<Fruit, string> collection = Collection();

        Assert.Equal("Banana", collection.Get("b").Name);
        Assert.False(collection.TryGet("zzz", out _));
        KeyNotFoundException error = Assert.Throws<KeyNotFoundException>(() => collection.Get("zzz"));
        Assert.StartsWith("Blazix: ", error.Message, StringComparison.Ordinal);
    }

    private static ComboboxItemCollection<Fruit, string> Collection() => ComboboxItems.Create<Fruit, string>(s_fruits, fruit => fruit.Id!, fruit => fruit.Name);

    private IRenderedComponent<ComboboxHarness> Render(Dictionary<string, object?> root, bool withValue = false, string? placeholder = null)
    {
        void Extra(RenderTreeBuilder builder)
        {
            if (!withValue)
            {
                return;
            }

            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "data-testid", "value");
            builder.OpenComponent<ComboboxValue<string>>(2);
            if (placeholder is not null)
            {
                builder.AddComponentParameter(3, "Placeholder", (RenderFragment)(inner => inner.AddContent(0, placeholder)));
            }

            builder.CloseComponent();
            builder.CloseElement();
        }

        return test.Context.Render<ComboboxHarness>(p => p.Add(x => x.RootParameters, root).Add(x => x.Extra, (RenderFragment)Extra));
    }

    private sealed record Fruit(string? Id, string Name);
}

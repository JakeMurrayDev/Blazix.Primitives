using System.Globalization;
using System.Text.RegularExpressions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// Base UI's ComboboxItem.test.tsx titles for an item's index (CMB-API-22, CMB-API-57, CMB-DOM-09, CMB-STA-33), at L2: the
// item's index is the one its registration sends to the `composite` module, whose registry drops a negative index (no
// element, so neither navigation nor a hover reaches the item).
/// <summary>Combobox's item index at L2.</summary>
public sealed partial class ComboboxItemIndexTests : IDisposable
{
    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "does not assign an id to an explicitly unregistered item")]
    [Trait("Requirement", "CMB-API-57")]
    [Trait("Requirement", "CMB-DOM-09")]
    public void Does_not_assign_an_id_to_an_explicitly_unregistered_item()
    {
        IRenderedComponent<IComponent> cut = test.Context.Render(Root(root => root.AddComponentParameter(1, "DefaultOpen", true), list =>
        {
            list.OpenComponent<ComboboxItem<string>>(0);
            list.AddComponentParameter(1, nameof(ComboboxItem<string>.Value), "orphan");
            list.AddComponentParameter(2, nameof(ComboboxItem<string>.Index), -1);
            list.AddComponentParameter(3, "ChildContent", Text("orphan"));
            list.CloseComponent();
        }));

        Assert.False(cut.Find("[role=option]").HasAttribute("id"));
    }

    [Fact(DisplayName = "does not register an item missing from the filtered values")]
    [Trait("Requirement", "CMB-API-22")]
    [Trait("Requirement", "CMB-STA-33")]
    public void Does_not_register_an_item_missing_from_the_filtered_values()
    {
        IRenderedComponent<IComponent> cut = test.Context.Render(Root(
            root =>
            {
                root.AddComponentParameter(1, "DefaultOpen", true);
                root.AddComponentParameter(2, "Virtualized", true);
                root.AddComponentParameter(3, "Items", (IReadOnlyList<string>)["one"]);
            },
            list =>
            {
                list.OpenComponent<ComboboxItem<string>>(0);
                list.AddComponentParameter(1, "ChildContent", Text("missing value"));
                list.CloseComponent();
            }));

        Assert.False(cut.Find("[role=option]").HasAttribute("id"));
        Assert.Equal([-1], RegisteredIndexes());
    }

    [Fact(DisplayName = "registers a rendered window at its filtered indexes")]
    [Trait("Requirement", "CMB-API-22")]
    [Trait("Requirement", "CMB-STA-33")]
    public void Registers_a_rendered_window_at_its_filtered_indexes()
    {
        IRenderedComponent<IComponent> cut = test.Context.Render(Root(
            root =>
            {
                root.AddComponentParameter(1, "DefaultOpen", true);
                root.AddComponentParameter(2, "Virtualized", true);
                root.AddComponentParameter(3, "Items", (IReadOnlyList<string>)["one", "two", "three", "four", "five"]);
            },
            list =>
            {
                // The window of the filtered items a virtualizer renders: items.slice(2, 4), without an index of their own.
                foreach (string item in new[] { "three", "four" })
                {
                    list.OpenComponent<ComboboxItem<string>>(0);
                    list.SetKey(item);
                    list.AddComponentParameter(1, nameof(ComboboxItem<string>.Value), item);
                    list.AddComponentParameter(2, "ChildContent", Text(item));
                    list.CloseComponent();
                }
            }));

        Assert.Equal(2, cut.FindAll("[role=option]").Count);
        Assert.Equal([2, 3], RegisteredIndexes());
    }

    private static RenderFragment Text(string text) => builder => builder.AddContent(0, text);

    private static RenderFragment Root(Action<RenderTreeBuilder> rootParameters, Action<RenderTreeBuilder> listContent) => builder =>
    {
        builder.OpenComponent<ComboboxRoot<string>>(0);
        rootParameters(builder);
        builder.AddComponentParameter(10, "ChildContent", (RenderFragment)(inner =>
        {
            inner.OpenComponent<ComboboxList<string>>(0);
            inner.AddComponentParameter(1, "ChildContent", (RenderFragment)(list => listContent(list)));
            inner.CloseComponent();
        }));
        builder.CloseComponent();
    };

    // The explicit indexes of the item registrations the list's last options carry, in order; null for an automatic index.
    private List<int?> RegisteredIndexes()
    {
        string options = test.ListHandleOptions().Last(json => json.Contains("\"items\":[{", StringComparison.Ordinal));
        string items = ItemsArray().Match(options).Groups[1].Value;
        List<int?> indexes = [];
        foreach (Match registration in Registration().Matches(items))
        {
            Match index = IndexField().Match(registration.Value);
            indexes.Add(index.Success ? int.Parse(index.Groups[1].Value, CultureInfo.InvariantCulture) : null);
        }

        return indexes;
    }

    [GeneratedRegex("\"items\":\\[(.*?)\\](,|\\})")]
    private static partial Regex ItemsArray();

    [GeneratedRegex("\\{[^{}]*\\}")]
    private static partial Regex Registration();

    [GeneratedRegex("\"index\":(-?\\d+)")]
    private static partial Regex IndexField();
}

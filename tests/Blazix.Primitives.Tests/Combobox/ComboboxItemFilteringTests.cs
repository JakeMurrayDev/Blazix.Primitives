using System.Globalization;
using Blazix.Primitives.Utilities;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

// CMB-STA-22 (the bypass, AriaCombobox.tsx:342-356) and CMB-STA-23 (the filtered-items derivation, :358-452) as pure C#
// derivation tests ([L1], the brief's Tests first): each of Base UI's ComboboxRoot.test.tsx titles is restated as the
// derivation slice its rendered list exercises, with the same items and query. The createItems.test.tsx titles of these
// rows land with createItems. CI runs under the invariant culture, so the collator names en-US (Base UI's runtime locale).
/// <summary>Combobox's filtered items at L1: the bypass and the derivation.</summary>
public sealed class ComboboxItemFilteringTests
{
    private static readonly CollatorFilter s_collator = CollatorFilter.Create(new CultureInfo("en-US"));

    [Fact(DisplayName = "should show all items when query matches current selection")]
    [Trait("Requirement", "CMB-STA-22")]
    public void Should_show_all_items_when_query_matches_current_selection()
    {
        // After selecting "apple" the input shows "apple"; reopening without typing keeps queryChangedAfterOpen false.
        string[] items = ["apple", "banana", "cherry"];
        bool bypass = ComboboxItemFiltering.ShouldBypassFiltering(single: true, queryChangedAfterOpen: false, query: "apple", selectedLabel: "apple", s_collator);

        ComboboxFilteredItems<string> result = ComboboxItemFiltering.Derive(Inputs(items, bypass ? "" : "apple"));

        Assert.True(bypass);
        Assert.Equal(items, result.Flat);
    }

    [Fact(DisplayName = "should reset input value to selected value when popup closes without selection")]
    [Trait("Requirement", "CMB-STA-22")]
    public void Should_reset_input_value_to_selected_value_when_popup_closes_without_selection()
    {
        // Typing "xyz" after "apple" filters (the query changed after open); once the close resets the input to the
        // selected label and the flag clears, the next open shows every item again.
        string[] items = ["apple", "banana", "cherry"];
        bool typed = ComboboxItemFiltering.ShouldBypassFiltering(single: true, queryChangedAfterOpen: true, query: "applexyz", selectedLabel: "apple", s_collator);
        bool reset = ComboboxItemFiltering.ShouldBypassFiltering(single: true, queryChangedAfterOpen: false, query: "apple", selectedLabel: "apple", s_collator);

        Assert.False(typed);
        Assert.Empty(ComboboxItemFiltering.Derive(Inputs(items, "applexyz")).Flat);
        Assert.True(reset);
        Assert.Equal(items, ComboboxItemFiltering.Derive(Inputs(items, "")).Flat);
    }

    [Fact(DisplayName = "resets external filteredItems when reopening after a selection")]
    [Trait("Requirement", "CMB-STA-22")]
    public void Resets_external_filteredItems_when_reopening_after_a_selection()
    {
        // With items and filteredItems both given, a bypass ignores the external result (the selected value is in items).
        string[] items = ["apple", "orange", "banana"];
        string[] external = ["apple"];

        ComboboxFilteredItems<string> ignored = ComboboxItemFiltering.Derive(Inputs(items, "", external: external, ignoreExternal: true));
        ComboboxFilteredItems<string> used = ComboboxItemFiltering.Derive(Inputs(items, "apple", external: external, ignoreExternal: false));

        Assert.Equal(items, ignored.Flat);
        Assert.Equal(external, used.Flat);
    }

    [Fact(DisplayName = "\"single\" selects and closes, then reopens with selection focused")]
    [Trait("Requirement", "CMB-STA-22")]
    public void Single_selects_and_closes_then_reopens_with_selection_focused()
    {
        string[] items = ["a", "b", "c"];
        bool bypass = ComboboxItemFiltering.ShouldBypassFiltering(single: true, queryChangedAfterOpen: false, query: "b", selectedLabel: "b", s_collator);

        ComboboxFilteredItems<string> result = ComboboxItemFiltering.Derive(Inputs(items, bypass ? "" : "b"));

        Assert.True(bypass);
        Assert.Equal(items, result.Flat);
        Assert.Equal(1, ItemEquality.FindSelectionIndex<string>([.. result.Flat.Select(item => new ItemSlot<string>(item, true))], "b", null));
    }

    [Fact(DisplayName = "port: no bypass in multiple mode, for an empty query, or for a label of another length")]
    [Trait("Requirement", "CMB-STA-22")]
    public void No_bypass_in_multiple_mode_for_an_empty_query_or_for_a_label_of_another_length()
    {
        Assert.True(ComboboxItemFiltering.ShouldBypassFiltering(single: true, queryChangedAfterOpen: false, query: "APPLE", selectedLabel: "apple", s_collator));
        Assert.False(ComboboxItemFiltering.ShouldBypassFiltering(single: false, queryChangedAfterOpen: false, query: "apple", selectedLabel: "apple", s_collator));
        Assert.False(ComboboxItemFiltering.ShouldBypassFiltering(single: true, queryChangedAfterOpen: false, query: "", selectedLabel: "", s_collator));
        Assert.False(ComboboxItemFiltering.ShouldBypassFiltering(single: true, queryChangedAfterOpen: false, query: "app", selectedLabel: "apple", s_collator));
    }

    [Fact(DisplayName = "omits groups with no matching items")]
    [Trait("Requirement", "CMB-STA-23")]
    public void Omits_groups_with_no_matching_items()
    {
        ComboboxItemGroup<string>[] groups = [new("fruit", ["apple"]), new("vegetables", ["broccoli"])];

        ComboboxFilteredItems<string> result = ComboboxItemFiltering.Derive(Inputs(groups, "app"));

        ComboboxItemGroup<string> only = Assert.Single(result.Groups!);
        Assert.Equal("fruit", only.Value);
        Assert.Equal(["apple"], only.Items);
        Assert.Equal(["apple"], result.Flat);
    }

    [Fact(DisplayName = "limits the number of items displayed when groups are used")]
    [Trait("Requirement", "CMB-STA-23")]
    public void Limits_the_number_of_items_displayed_when_groups_are_used()
    {
        ComboboxItemGroup<string>[] groups = [new("citrus", ["orange", "lemon", "lime"]), new("berries", ["strawberry", "blueberry", "raspberry"])];

        ComboboxFilteredItems<string> result = ComboboxItemFiltering.Derive(Inputs(groups, "", limit: 4));

        Assert.Equal(["orange", "lemon", "lime", "strawberry"], result.Flat);
        Assert.Equal(2, result.Groups!.Count);
        Assert.Equal(["strawberry"], result.Groups[1].Items);
    }

    [Fact(DisplayName = "preserves order of items when applying limit across groups")]
    [Trait("Requirement", "CMB-STA-23")]
    public void Preserves_order_of_items_when_applying_limit_across_groups()
    {
        ComboboxItemGroup<string>[] groups = [new("groupA", ["A1", "A2"]), new("groupB", ["B1", "B2", "B3"])];

        ComboboxFilteredItems<string> result = ComboboxItemFiltering.Derive(Inputs(groups, "", limit: 3));

        Assert.Equal(["A1", "A2", "B1"], result.Flat);
    }

    [Fact(DisplayName = "uses custom filter to narrow results")]
    [Trait("Requirement", "CMB-STA-23")]
    public void Uses_custom_filter_to_narrow_results()
    {
        string[] items = ["alpha", "beta", "alphabet", "alpine"];
        ComboboxItemPredicate<string> startsWith = ComboboxItemPredicate<string>.Custom((item, query, _) => item.StartsWith(query, StringComparison.OrdinalIgnoreCase), null);

        ComboboxFilteredItems<string> result = ComboboxItemFiltering.Derive(Inputs(items, "alp", filter: startsWith));

        Assert.Equal(["alpha", "alphabet", "alpine"], result.Flat);
    }

    [Fact(DisplayName = "uses filteredItems when items prop is omitted")]
    [Trait("Requirement", "CMB-STA-23")]
    public void Uses_filteredItems_when_items_prop_is_omitted()
    {
        string[] fruits = ["Apple", "Banana", "Cherry"];

        ComboboxFilteredItems<string> result = ComboboxItemFiltering.Derive(Inputs((string[]?)null, "a", external: fruits));

        Assert.Same(fruits, result.Flat);
    }

    [Fact(DisplayName = "port: without items or filteredItems the derivation is empty, and an unlimited empty query keeps the items' own list")]
    [Trait("Requirement", "CMB-STA-23")]
    public void Without_items_the_derivation_is_empty_and_an_unlimited_empty_query_keeps_the_list()
    {
        string[] items = ["a", "b"];

        Assert.Empty(ComboboxItemFiltering.Derive(Inputs((string[]?)null, "")).Flat);
        Assert.Same(items, ComboboxItemFiltering.Derive(Inputs(items, "")).Flat);
        Assert.Equal(["a"], ComboboxItemFiltering.Derive(Inputs(items, "", limit: 1)).Flat);
    }

    [Fact(DisplayName = "port: the default filter rejects null items, the off filter keeps every item, and a limit stops matching")]
    [Trait("Requirement", "CMB-STA-23")]
    [Trait("Requirement", "CMB-API-14")]
    public void The_default_filter_rejects_null_items_and_the_off_filter_keeps_every_item()
    {
        string?[] items = [null, "apple", "apricot", "banana"];

        Assert.Equal(["apple", "apricot"], ComboboxItemFiltering.Derive(Inputs(items, "ap")).Flat);
        Assert.Equal(items, ComboboxItemFiltering.Derive(Inputs(items, "ap", filter: ComboboxItemPredicate<string?>.Off)).Flat);
        Assert.Equal(["apple"], ComboboxItemFiltering.Derive(Inputs(items, "ap", limit: 1)).Flat);
    }

    private static ComboboxFilterInputs<T> Inputs<T>(
        IReadOnlyList<T>? items,
        string query,
        int limit = -1,
        IReadOnlyList<T>? external = null,
        bool ignoreExternal = false,
        ComboboxItemPredicate<T>? filter = null) =>
        new(items, null, external, null, ignoreExternal, query, limit, filter ?? ComboboxItemPredicate<T>.Collator(s_collator, null));

    private static ComboboxFilterInputs<T> Inputs<T>(IReadOnlyList<ComboboxItemGroup<T>> groups, string query, int limit = -1) =>
        new(null, groups, null, null, false, query, limit, ComboboxItemPredicate<T>.Collator(s_collator, null));
}

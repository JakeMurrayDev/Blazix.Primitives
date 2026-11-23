using System.Collections;
using Blazix.Primitives.Utilities;

namespace Blazix.Primitives.Tests.ListValues;

// UTL-ALG-06 (internals/itemEquality.ts, ADR 08 Amendments 2026-11-03 (2)): itemEquality.test.ts's 15 titles verbatim on the
// port's view, a span of ItemSlot<TValue> in rendered order whose absent slot stands for Base UI's `undefined` item and an
// unmounted hole, and port titles for the members Base UI tests only through the families. A null comparer is Base UI's
// defaultItemEquality (Object.is, Equality.Is).
/// <summary>The item equality the value-typed list families share.</summary>
public sealed class ItemEqualityTests
{
    private static readonly ItemSlot<string>[] s_items = Slots("a", "b", "c");

    // Design point 1 (a) of M5 track 10 (an Item's index among the filtered values in constant time): the map shares
    // SelectionSet's hash and Object.is equality, and serves the default comparer only.
    [Fact(DisplayName = "port: an index map finds each value's first index, and -1 for an absent value")]
    [Trait("Requirement", "UTL-ALG-06")]
    public void An_index_map_finds_each_values_first_index()
    {
        ItemEquality.IndexMap<string> map = new();
        ItemSlot<string>[] items = [new("a", true), new("b", true), new("a", true), new("x", false), new("c", true)];

        map.Build(items);

        Assert.Equal(0, map.IndexOf("a"));
        Assert.Equal(1, map.IndexOf("b"));
        Assert.Equal(4, map.IndexOf("c"));
        Assert.Equal(-1, map.IndexOf("x"));
        Assert.Equal(-1, map.IndexOf("d"));
    }

    [Fact(DisplayName = "port: an index map compares by Object.is")]
    [Trait("Requirement", "UTL-ALG-06")]
    public void An_index_map_compares_by_Object_is()
    {
        ItemEquality.IndexMap<double> map = new();

        map.Build(Slots(double.NaN, -0.0, 1.5));

        Assert.Equal(0, map.IndexOf(double.NaN));
        Assert.Equal(1, map.IndexOf(-0.0));
        Assert.Equal(-1, map.IndexOf(0.0));
        Assert.Equal(2, map.IndexOf(1.5));
    }

    [Fact(DisplayName = "port: an index map rebuilt over another list holds only that list")]
    [Trait("Requirement", "UTL-ALG-06")]
    public void An_index_map_rebuilt_over_another_list_holds_only_that_list()
    {
        ItemEquality.IndexMap<int> map = new();
        map.Build(Slots([.. Enumerable.Range(0, 100)]));
        Assert.Equal(99, map.IndexOf(99));

        map.Build(Slots(7, 3));

        Assert.Equal(-1, map.IndexOf(99));
        Assert.Equal(0, map.IndexOf(7));
        Assert.Equal(1, map.IndexOf(3));
    }

    [Fact(DisplayName = "anchors to the first selected item in rendered order, not value order")]
    [Trait("Requirement", "UTL-ALG-06")]
    public void Anchors_to_the_first_selected_item_in_rendered_order_not_value_order()
    {
        Assert.Equal(1, ItemEquality.FindSelectionIndex<string>(s_items, ["c", "b"], null));
        Assert.Equal(1, ItemEquality.FindSelectionIndex<string>(s_items, ["b", "c"], null));
    }

    [Fact(DisplayName = "returns null when nothing in the value array is rendered")]
    [Trait("Requirement", "UTL-ALG-06")]
    public void Returns_null_when_nothing_in_the_value_array_is_rendered()
    {
        Assert.Null(ItemEquality.FindSelectionIndex<string>(s_items, [], null));
        Assert.Null(ItemEquality.FindSelectionIndex<string>(s_items, ["d"], null));
    }

    // Single mode takes the value itself, so a list-typed TValue is one value by construction: the single overload.
    [Fact(DisplayName = "treats an array as a single value outside multiple mode")]
    [Trait("Requirement", "UTL-ALG-06")]
    public void Treats_an_array_as_a_single_value_outside_multiple_mode()
    {
        string[] arrayValue = ["x", "y"];
        Func<string[], string[], bool> comparer = (itemValue, selectedValue) => itemValue.AsSpan().SequenceEqual(selectedValue);

        Assert.Equal(1, ItemEquality.FindSelectionIndex<string[]>(Slots<string[]>(["a"], ["x", "y"]), arrayValue, comparer));
    }

    [Fact(DisplayName = "anchors to the first selected item with a custom comparer")]
    [Trait("Requirement", "UTL-ALG-06")]
    public void Anchors_to_the_first_selected_item_with_a_custom_comparer()
    {
        Func<string, string, bool> comparer = (itemValue, selectedValue) => string.Equals(itemValue, selectedValue, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(1, ItemEquality.FindSelectionIndex<string>(s_items, ["C", "B"], comparer));
        Assert.Null(ItemEquality.FindSelectionIndex<string>(s_items, ["D"], comparer));
    }

    [Fact(DisplayName = "reads the selected values once instead of rescanning them for every item")]
    [Trait("Requirement", "UTL-ALG-06")]
    public void Reads_the_selected_values_once_instead_of_rescanning_them_for_every_item()
    {
        // Nothing rendered is selected, so a nested scan would visit every pair.
        ItemSlot<string>[] itemValues = Slots([.. Enumerable.Range(0, 200).Select(i => $"item-{i}")]);
        CountingList<string> selectedValues = new([.. Enumerable.Range(0, 100).Select(i => $"filtered-out-{i}")]);

        Assert.Null(ItemEquality.FindSelectionIndex<string>(itemValues, selectedValues, null));
        Assert.True(selectedValues.Reads <= itemValues.Length + selectedValues.Count, $"{selectedValues.Reads} reads");
    }

    [Fact(DisplayName = "keeps `+0` and `-0` distinct, like `Object.is`")]
    [Trait("Requirement", "UTL-ALG-06")]
    public void Keeps_positive_and_negative_zero_distinct_like_Object_is()
    {
        Assert.Null(ItemEquality.FindSelectionIndex<double>(Slots(-0.0), [0.0], null));
        Assert.Null(ItemEquality.FindSelectionIndex<double>(Slots(0.0), [-0.0], null));
        Assert.Equal(0, ItemEquality.FindSelectionIndex<double>(Slots(-0.0), [-0.0], null));
        Assert.Equal(1, ItemEquality.FindSelectionIndex<double>(Slots(1.0, 0.0), [2.0, 0.0], null));
        Assert.Null(ItemEquality.FindSelectionIndex<double>(Slots(1.0, -0.0), [2.0, 0.0], null));
        Assert.Equal(1, ItemEquality.FindSelectionIndex<double>(Slots(1.0, -0.0), [2.0, -0.0], null));
    }

    [Fact(DisplayName = "matches `NaN` and `null` against themselves")]
    [Trait("Requirement", "UTL-ALG-06")]
    public void Matches_NaN_and_null_against_themselves()
    {
        Assert.Equal(1, ItemEquality.FindSelectionIndex<double>(Slots(1.0, double.NaN), [double.NaN], null));
        Assert.Equal(1, ItemEquality.FindSelectionIndex<string?>(Slots<string?>("a", null), [null], null));
    }

    // Port note (UTL-ALG-06, 2026-11-04): C# has no `undefined`. An item that never registered its value, Base UI's `undefined`
    // item, is an absent slot, and the selected values have no `undefined` member (a supplied null is the value null).
    [Fact(DisplayName = "never matches an `undefined` item or an `undefined` selected value")]
    [Trait("Requirement", "UTL-ALG-06")]
    public void Never_matches_an_undefined_item_or_an_undefined_selected_value()
    {
        ItemSlot<string?>[] items = [default, new(null, true), new("b", true)];

        Assert.Equal(2, ItemEquality.FindSelectionIndex<string?>(items, ["b"], null));
        Assert.Null(ItemEquality.FindSelectionIndex<string?>([default], [null], null));
        Assert.Null(ItemEquality.FindSelectionIndex<string?>([default], (string?)null, null));
    }

    // Port note (UTL-ALG-06, 2026-11-04): an unmounted item's hole is an absent slot; a C# list of selected values has no hole.
    [Fact(DisplayName = "never matches a hole left by an unmounted item")]
    [Trait("Requirement", "UTL-ALG-06")]
    public void Never_matches_a_hole_left_by_an_unmounted_item()
    {
        ItemSlot<string>[] sparseItems = [default, default, new("c", true)];

        Assert.Equal(2, ItemEquality.FindSelectionIndex<string>(sparseItems, ["c"], null));
        Assert.Null(ItemEquality.FindSelectionIndex<string>(sparseItems, [], null));
    }

    [Fact(DisplayName = "does not claim an unselected item")]
    [Trait("Requirement", "UTL-ALG-06")]
    public void Does_not_claim_an_unselected_item() => Assert.Null(Resolve(1, ["a", "c"], null));

    [Fact(DisplayName = "claims when no item holds the index yet")]
    [Trait("Requirement", "UTL-ALG-06")]
    public void Claims_when_no_item_holds_the_index_yet() => Assert.Equal(2, Resolve(2, ["c"], null));

    [Fact(DisplayName = "claims from a later holder")]
    [Trait("Requirement", "UTL-ALG-06")]
    public void Claims_from_a_later_holder() => Assert.Equal(0, Resolve(0, ["a", "c"], 2));

    [Fact(DisplayName = "leaves the index with an earlier selected holder")]
    [Trait("Requirement", "UTL-ALG-06")]
    public void Leaves_the_index_with_an_earlier_selected_holder() => Assert.Equal(0, Resolve(2, ["a", "c"], 0));

    [Fact(DisplayName = "takes over from an earlier holder that is no longer selected")]
    [Trait("Requirement", "UTL-ALG-06")]
    public void Takes_over_from_an_earlier_holder_that_is_no_longer_selected()
    {
        // `a` held the index but has been deselected, so `b` takes it and `c` then defers.
        Assert.Equal(1, Resolve(0, ["b", "c"], 0));
        Assert.Equal(1, Resolve(2, ["b", "c"], 1));
    }

    [Fact(DisplayName = "takes over when the earlier holder has left the registry")]
    [Trait("Requirement", "UTL-ALG-06")]
    public void Takes_over_when_the_earlier_holder_has_left_the_registry()
    {
        ItemSlot<string>[] sparseRegistry = [default, new("b", true), new("c", true)];

        Assert.Equal(2, ItemEquality.ResolveSelectedIndex<string>(2, "c", sparseRegistry, ["c"], null, 0));
    }

    [Fact(DisplayName = "port: a comparer is consulted only when both operands are non-null, item first, and a null operand compares by Object.is")]
    [Trait("Requirement", "UTL-ALG-06")]
    [Trait("Requirement", "SEL-API-22")]
    public void A_comparer_is_consulted_only_for_non_null_operands_item_first()
    {
        List<(string Item, string Selected)> calls = [];
        Func<string?, string?, bool> comparer = (item, selected) =>
        {
            calls.Add((item!, selected!));
            return string.Equals(item, selected, StringComparison.OrdinalIgnoreCase);
        };

        Assert.True(ItemEquality.Compare("a", "A", comparer));
        Assert.False(ItemEquality.Compare(null, "A", comparer));
        Assert.False(ItemEquality.Compare("a", null, comparer));
        Assert.True(ItemEquality.Compare<string?>(null, null, comparer));
        Assert.Equal(("a", "A"), Assert.Single(calls));

        // The default compares as Object.is: a record class by reference, a string by its text.
        Assert.False(ItemEquality.Compare(new Item("a"), new Item("a"), null));
        Assert.True(ItemEquality.Compare(string.Concat("a", "b"), "ab", null));
    }

    [Fact(DisplayName = "port: the dirty test compares lists element-wise in order through the comparer, and other values with ===")]
    [Trait("Requirement", "UTL-ALG-06")]
    public void The_dirty_test_compares_lists_element_wise_and_scalars_strictly()
    {
        Func<string, string, bool> comparer = (item, selected) => string.Equals(item, selected, StringComparison.OrdinalIgnoreCase);

        Assert.False(ItemEquality.IsDirty<string>(["a", "b"], ["A", "B"], comparer));
        Assert.True(ItemEquality.IsDirty<string>(["a", "b"], ["b", "a"], comparer));
        Assert.True(ItemEquality.IsDirty<string>(["a"], ["a", "b"], null));
        Assert.True(ItemEquality.IsDirty<string>(["a"], null, null));
        Assert.False(ItemEquality.IsDirty<string>(null, null, null));

        // A scalar is dirty unless ===: NaN differs from itself, and +0 equals -0.
        Assert.True(ItemEquality.IsDirty(double.NaN, double.NaN));
        Assert.False(ItemEquality.IsDirty(0.0, -0.0));
        Assert.False(ItemEquality.IsDirty("a", "a"));
    }

    [Fact(DisplayName = "port: selected values include an item value through the comparer, and removing it keeps the rest in order")]
    [Trait("Requirement", "UTL-ALG-06")]
    public void Selected_values_include_an_item_value_and_removing_it_keeps_the_rest_in_order()
    {
        Func<string, string, bool> comparer = (item, selected) => string.Equals(item, selected, StringComparison.OrdinalIgnoreCase);

        Assert.True(ItemEquality.Includes<string>(["a", "B"], "b", comparer));
        Assert.False(ItemEquality.Includes<string>(["a", "B"], "b", null));
        Assert.False(ItemEquality.Includes<string>(null, "b", null));
        Assert.Equal(1, ItemEquality.FindItemIndex<string>(s_items, "b", null));
        Assert.Equal(-1, ItemEquality.FindItemIndex<string>(s_items, "d", null));
        Assert.Equal(["a", "c"], ItemEquality.RemoveItem<string>(["a", "B", "c", "b"], "b", comparer));
    }

    private static int? Resolve(int index, IReadOnlyList<string> selectedValues, int? currentIndex) =>
        ItemEquality.ResolveSelectedIndex<string>(index, s_items[index].Value, s_items, selectedValues, null, currentIndex);

    private static ItemSlot<T>[] Slots<T>(params T[] values) => [.. values.Select(value => new ItemSlot<T>(value, true))];

    private sealed record Item(string Name);

    // Base UI's Proxy: counts the reads of an element by index or by enumeration.
    private sealed class CountingList<T>(T[] values) : IReadOnlyList<T>
    {
        public int Reads { get; private set; }

        public int Count => values.Length;

        public T this[int index]
        {
            get
            {
                Reads++;
                return values[index];
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            foreach (T value in values)
            {
                Reads++;
                yield return value;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

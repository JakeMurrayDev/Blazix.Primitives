using System.Globalization;
using Blazix.Primitives.Utilities;

namespace Blazix.Primitives.Tests.ListValues;

// UTL-ALG-07, UTL-I18N-13 (internals/resolveValueLabel.tsx; design point 2 of M5 track 4, settled by the orchestrator
// 2026-11-04: the internal LabeledItems<TValue> view, default labels as invariant text). resolveValueLabel.test.ts's titles
// verbatim where the view expresses them: the record form is the keyed view (a dictionary has no prototype, so the prototype
// titles hold as written). Its object-shape titles are restated as port titles, since a typed view has no `items` member to
// misread. CI runs under the invariant culture, so the titles set de-DE, whose decimal separator differs.
/// <summary>The value-label resolution the value-typed list families share.</summary>
public sealed class ValueLabelsTests : IDisposable
{
    private readonly CultureInfo previous = CultureInfo.CurrentCulture;

    public ValueLabelsTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
    }

    /// <inheritdoc />
    public void Dispose() => CultureInfo.CurrentCulture = previous;

    [Fact(DisplayName = "returns true when grouped items contain a null-valued item with a label")]
    [Trait("Requirement", "UTL-ALG-07")]
    public void Returns_true_when_grouped_items_contain_a_null_valued_item_with_a_label() =>
        Assert.True(ValueLabels.HasNullItemLabel(LabeledItems<string?>.Grouped([[new("a", "A"), new(null, "Select")]])));

    [Fact(DisplayName = "returns false when grouped items contain a null-valued item without a label")]
    [Trait("Requirement", "UTL-ALG-07")]
    public void Returns_false_when_grouped_items_contain_a_null_valued_item_without_a_label() =>
        Assert.False(ValueLabels.HasNullItemLabel(LabeledItems<string?>.Grouped([[new(null, null), new("a", "A")]])));

    [Fact(DisplayName = "returns false when grouped items do not contain a null-valued item")]
    [Trait("Requirement", "UTL-ALG-07")]
    public void Returns_false_when_grouped_items_do_not_contain_a_null_valued_item() =>
        Assert.False(ValueLabels.HasNullItemLabel(LabeledItems<string?>.Grouped([[new("a", "A")]])));

    // A group's heading is the family's (any member); the view holds only its leaves.
    [Fact(DisplayName = "supports grouped items with custom heading keys")]
    [Trait("Requirement", "UTL-ALG-07")]
    public void Supports_grouped_items_with_custom_heading_keys() =>
        Assert.True(ValueLabels.HasNullItemLabel(LabeledItems<string?>.Grouped([[new("a", "A"), new(null, "Select")]])));

    [Fact(DisplayName = "returns true when flat items contain a null-valued item with a label")]
    [Trait("Requirement", "UTL-ALG-07")]
    public void Returns_true_when_flat_items_contain_a_null_valued_item_with_a_label() =>
        Assert.True(ValueLabels.HasNullItemLabel(LabeledItems<string?>.Flat([new("a", "A"), new(null, "None")])));

    [Fact(DisplayName = "returns false when flat items do not contain a null-valued item")]
    [Trait("Requirement", "UTL-ALG-07")]
    public void Returns_false_when_flat_items_do_not_contain_a_null_valued_item() =>
        Assert.False(ValueLabels.HasNullItemLabel(LabeledItems<string?>.Flat([new("a", "A"), new("b", "B")])));

    [Fact(DisplayName = "returns false when items is a Record without a \"null\" key")]
    [Trait("Requirement", "UTL-ALG-07")]
    public void Returns_false_when_items_is_a_Record_without_a_null_key() =>
        Assert.False(ValueLabels.HasNullItemLabel(LabeledItems<string?>.Keyed(Fonts())));

    [Fact(DisplayName = "returns true when items is a Record with a \"null\" key")]
    [Trait("Requirement", "UTL-ALG-07")]
    public void Returns_true_when_items_is_a_Record_with_a_null_key() =>
        Assert.True(ValueLabels.HasNullItemLabel(LabeledItems<string?>.Keyed(new Dictionary<string, string?> { ["null"] = "None", ["sans"] = "Sans-serif" })));

    [Fact(DisplayName = "returns false when items is undefined")]
    [Trait("Requirement", "UTL-ALG-07")]
    public void Returns_false_when_items_is_undefined() => Assert.False(ValueLabels.HasNullItemLabel(default(LabeledItems<string?>)));

    [Fact(DisplayName = "falls back to the stringified label when the value matches an Object.prototype member")]
    [Trait("Requirement", "UTL-ALG-07")]
    public void Falls_back_to_the_stringified_label_when_the_value_matches_an_Object_prototype_member()
    {
        LabeledItems<string> items = LabeledItems<string>.Keyed(Fonts());

        Assert.Equal("constructor", ValueLabels.ResolveSelectedLabel("constructor", items, null));
        Assert.Equal("toString", ValueLabels.ResolveSelectedLabel("toString", items, null));
        Assert.Equal("hasOwnProperty", ValueLabels.ResolveSelectedLabel("hasOwnProperty", items, null));
        Assert.Equal("__proto__", ValueLabels.ResolveSelectedLabel("__proto__", items, null));
    }

    [Fact(DisplayName = "resolves an own key that matches an Object.prototype member")]
    [Trait("Requirement", "UTL-ALG-07")]
    public void Resolves_an_own_key_that_matches_an_Object_prototype_member() =>
        Assert.Equal("Custom constructor", ValueLabels.ResolveSelectedLabel("constructor", LabeledItems<string>.Keyed(new Dictionary<string, string?> { ["constructor"] = "Custom constructor", ["sans"] = "Sans-serif" }), null));

    [Fact(DisplayName = "keeps resolving the null placeholder key in a record")]
    [Trait("Requirement", "UTL-ALG-07")]
    public void Keeps_resolving_the_null_placeholder_key_in_a_record() =>
        Assert.Equal("None", ValueLabels.ResolveSelectedLabel<string?>(null, LabeledItems<string?>.Keyed(new Dictionary<string, string?> { ["null"] = "None", ["sans"] = "Sans-serif" }), null));

    [Fact(DisplayName = "falls back to the stringified label when an own key has a nullish label")]
    [Trait("Requirement", "UTL-ALG-07")]
    public void Falls_back_to_the_stringified_label_when_an_own_key_has_a_nullish_label() =>
        Assert.Equal("sans", ValueLabels.ResolveSelectedLabel("sans", LabeledItems<string>.Keyed(new Dictionary<string, string?> { ["sans"] = null }), null));

    // Port note (UTL-ALG-07, 2026-11-04): Base UI classifies an array by its first element's `items` member ("classifies %s",
    // "resolves a flat item label when the item has an unrelated items field"). The view is grouped only when built from
    // groups, so a leaf whose value has an `Items` member stays a leaf.
    [Fact(DisplayName = "port: the view is grouped only when built from groups, and a leaf whose value has an Items member stays a flat leaf")]
    [Trait("Requirement", "UTL-ALG-07")]
    public void The_view_is_grouped_only_when_built_from_groups()
    {
        Option withItems = new("a", ["metadata"]);
        LabeledItems<Option> flat = LabeledItems<Option>.Flat([new(withItems, "A")]);

        Assert.False(flat.IsGrouped);
        Assert.True(LabeledItems<Option>.Grouped([[]]).IsGrouped);
        Assert.False(default(LabeledItems<Option>).IsGrouped);
        Assert.Equal("A", ValueLabels.ResolveSelectedLabel(withItems, flat, null));
    }

    [Fact(DisplayName = "port: a selected label is the leaf's label for an === match, and the stringified value otherwise")]
    [Trait("Requirement", "UTL-ALG-07")]
    [Trait("Requirement", "UTL-I18N-13")]
    public void A_selected_label_is_the_leafs_label_for_a_strict_match_and_the_stringified_value_otherwise()
    {
        LabeledItems<double> items = LabeledItems<double>.Grouped([[new(1.5, "One and a half")], [new(double.NaN, "Not a number"), new(2, null)]]);

        Assert.Equal("One and a half", ValueLabels.ResolveSelectedLabel(1.5, items, null));
        Assert.Equal("NaN", ValueLabels.ResolveSelectedLabel(double.NaN, items, null));
        Assert.Equal("2", ValueLabels.ResolveSelectedLabel(2.0, items, null));
        Assert.Equal("2.5", ValueLabels.ResolveSelectedLabel(2.5, items, null));
        Assert.Equal("1.5 (custom)", ValueLabels.ResolveSelectedLabel(1.5, items, value => value.ToString(CultureInfo.InvariantCulture) + " (custom)"));
    }

    [Fact(DisplayName = "port: multiple labels are joined with a comma and a space")]
    [Trait("Requirement", "UTL-ALG-07")]
    public void Multiple_labels_are_joined_with_a_comma_and_a_space()
    {
        LabeledItems<string> items = LabeledItems<string>.Flat([new("a", "Apple"), new("b", "Banana")]);

        Assert.Equal("Apple, Banana, c", ValueLabels.ResolveMultipleLabels(["a", "b", "c"], items, null));
        Assert.Equal(string.Empty, ValueLabels.ResolveMultipleLabels([], items, null));
    }

    [Fact(DisplayName = "port: an item's label and value text default to its invariant text, use the converters for a non-null item, and read no member of an object")]
    [Trait("Requirement", "UTL-I18N-13")]
    public void An_items_label_and_value_text_default_to_its_invariant_text()
    {
        int calls = 0;
        Func<double?, string> label = value =>
        {
            calls++;
            return value is null ? "never" : $"label {value.Value.ToString(CultureInfo.InvariantCulture)}";
        };

        Assert.Equal("1.5", ValueLabels.StringifyAsLabel<double?>(1.5, null));
        Assert.Equal(string.Empty, ValueLabels.StringifyAsLabel<double?>(null, label));
        Assert.Equal("label 1.5", ValueLabels.StringifyAsLabel<double?>(1.5, label));
        Assert.Equal(1, calls);
        Assert.Equal(string.Empty, ValueLabels.StringifyAsLabel("a", _ => null!));
        Assert.Equal("Option { Value = b, Items = System.String[] }", ValueLabels.StringifyAsLabel(new Option("b", []), null));
        Assert.Equal("1.5", ValueLabels.StringifyAsValue<double?>(1.5, null));
        Assert.Equal("v", ValueLabels.StringifyAsValue("x", _ => "v"));
    }

    [Fact(DisplayName = "port: the collator filter matches an item through its stringified label")]
    [Trait("Requirement", "UTL-I18N-13")]
    public void The_collator_filter_matches_an_item_through_its_stringified_label()
    {
        CollatorFilter filter = CollatorFilter.Create(new CultureInfo("en-US"));

        Assert.True(filter.Contains(1.5, "1.5", null));
        Assert.True(filter.StartsWith(new Option("Résumé", []), "resume", option => option.Value));
        Assert.True(filter.EndsWith("Apple pie", "PIE", null));
    }

    private static Dictionary<string, string?> Fonts() => new() { ["sans"] = "Sans-serif", ["serif"] = "Serif", ["mono"] = "Monospace" };

    private sealed record Option(string Value, string[] Items);
}

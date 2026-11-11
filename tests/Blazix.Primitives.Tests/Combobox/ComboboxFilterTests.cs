using System.Globalization;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests;

// CMB-API-65 (combobox/root/utils/useFilter.ts:14-46, root/utils/index.ts:23-68): Combobox.useFilter as the public
// ComboboxFilter (named by the track in its dated ADR 08 line), over the shared collator filter (UTL-I18N-08..13). Base UI's
// hook renders a component to read the result; the port's filter is a value, so each title calls it directly. CI runs
// under the invariant culture, so each title names its culture (en-US for Base UI's `locale: 'en'`).
/// <summary>Combobox's filter at L1: <c>useFilter.test.tsx</c> and <c>root/utils/index.test.ts</c>.</summary>
public sealed class ComboboxFilterTests
{
    private static readonly CultureInfo s_english = new("en-US");

    [Fact(DisplayName = "uses default options when called without arguments")]
    [Trait("Requirement", "CMB-API-65")]
    public void Uses_default_options_when_called_without_arguments()
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = s_english;
        try
        {
            ComboboxFilter<string> filter = ComboboxFilter.Create<string>();

            Assert.True(filter.Contains("Apple", "app"));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact(DisplayName = "matches the start and end of an item label")]
    [Trait("Requirement", "CMB-API-65")]
    public void Matches_the_start_and_end_of_an_item_label()
    {
        ComboboxFilter<string> filter = ComboboxFilter.Create<string>(culture: s_english);
        ComboboxFilter<Labeled> objects = ComboboxFilter.Create<Labeled>(culture: s_english);

        Assert.True(filter.StartsWith("Éclair", "ec"));
        Assert.False(filter.StartsWith("Éclair", "clair"));
        Assert.True(filter.StartsWith("Éclair", ""));
        Assert.False(filter.StartsWith("Tea", "teapot"));
        Assert.True(filter.EndsWith("Crème brûlée", "BRULEE"));
        Assert.False(filter.EndsWith("Crème brûlée", "crème"));
        Assert.True(filter.EndsWith("Crème brûlée", ""));
        Assert.False(filter.EndsWith("Tea", "iced tea"));
        Assert.True(objects.StartsWith(new Labeled("Banana"), "ban", item => item.Label));
    }

    [Fact(DisplayName = "filters selected and unselected items in single and multiple modes")]
    [Trait("Requirement", "CMB-API-65")]
    public void Filters_selected_and_unselected_items_in_single_and_multiple_modes()
    {
        ComboboxFilter<string> single = ComboboxFilter.Create("Apple", multiple: false, culture: s_english);
        ComboboxFilter<string> multiple = ComboboxFilter.Create("Apple", multiple: true, culture: s_english);

        Assert.True(single.Contains("Banana", "apple"));
        Assert.True(single.Contains("Banana", "nan"));
        Assert.False(multiple.Contains("Banana", "apple"));
        Assert.True(multiple.Contains("Banana", "nan"));
    }

    [Fact(DisplayName = "port: equal options give equal filters, as Base UI's hook returns the same object while its inputs are unchanged")]
    [Trait("Requirement", "CMB-API-65")]
    public void Equal_options_give_equal_filters()
    {
        Assert.Equal(ComboboxFilter.Create("Apple", culture: s_english), ComboboxFilter.Create("Apple", culture: s_english));
        Assert.NotEqual(ComboboxFilter.Create("Apple", culture: s_english), ComboboxFilter.Create("Banana", culture: s_english));
        Assert.NotEqual(ComboboxFilter.Create("Apple", multiple: true, culture: s_english), ComboboxFilter.Create("Apple", culture: s_english));
    }

    [Fact(DisplayName = "rejects nullish items and filters primitives and projected objects")]
    [Trait("Requirement", "CMB-API-65")]
    public void Rejects_nullish_items_and_filters_primitives_and_projected_objects()
    {
        ComboboxFilter<string?> primitives = ComboboxFilter.Create<string?>(multiple: true, culture: s_english);
        ComboboxFilter<Named?> objects = ComboboxFilter.Create<Named?>(multiple: true, culture: s_english);

        Assert.False(primitives.Contains(null, "app"));
        Assert.True(primitives.Contains("Apple", "app"));
        Assert.True(objects.Contains(new Named("Banana"), "nan", item => item!.Name));
        Assert.False(objects.Contains(null, "nan", item => item!.Name));
    }

    [Fact(DisplayName = "shows all items for an empty query and rejects nullish items")]
    [Trait("Requirement", "CMB-API-65")]
    public void Shows_all_items_for_an_empty_query_and_rejects_nullish_items()
    {
        ComboboxFilter<string?> filter = ComboboxFilter.Create<string?>(culture: s_english);

        Assert.False(filter.Contains(null, "app"));
        Assert.True(filter.Contains("Apple", ""));
    }

    [Fact(DisplayName = "shows all items when the query exactly matches the selected label")]
    [Trait("Requirement", "CMB-API-65")]
    public void Shows_all_items_when_the_query_exactly_matches_the_selected_label()
    {
        ComboboxFilter<string> filter = ComboboxFilter.Create("Apple", culture: s_english);

        Assert.True(filter.Contains("Banana", "apple"));
    }

    [Fact(DisplayName = "otherwise filters the current item")]
    [Trait("Requirement", "CMB-API-65")]
    public void Otherwise_filters_the_current_item()
    {
        ComboboxFilter<string> filter = ComboboxFilter.Create("Apple", culture: s_english);

        Assert.False(filter.Contains("Banana", "app"));
        Assert.True(filter.Contains("Banana", "nan"));
    }

    [Fact(DisplayName = "port: a filter's Contains binds as the root's Filter delegate")]
    [Trait("Requirement", "CMB-API-65")]
    [Trait("Requirement", "CMB-API-14")]
    public void A_filters_Contains_binds_as_the_roots_Filter_delegate()
    {
        ComboboxFilter<string> filter = ComboboxFilter.Create("Apple", culture: s_english);
        Func<string, string, Func<string, string>?, bool> bound = filter.Contains;

        Assert.True(bound("Banana", "apple", null));
        Assert.False(bound("Banana", "app", null));
    }

    private sealed record Labeled(string Label);

    private sealed record Named(string Name);
}

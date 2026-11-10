using System.Globalization;
using System.Numerics;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// createItems.test.tsx's `integration` block at L2, the titles combobox.md cites (CMB-API-12..15, API-22, API-66, API-68,
// STA-19, STA-20, STA-28, PTR-05, EVT-11, EDGE-02, EDGE-04..06, EDGE-08, EDGE-12). The port's shapes (design point 2, ADR 08
// "Combobox as built"): `ComboboxItems.Create` returns the derived values, so an external window of source items is a
// collection created over them and passed as FilteredItems; Base UI's `createItems(undefined)` is a null Items; the List's
// template receives the value, and its text is the consumer's lookup of the source item; a custom Filter receives the
// derived value, and reads the source item through the collection's Get. useFilteredItems() has no public port: the List's
// ItemContent maps the same filtered items with their indexes, so the harness's Window stands for the slice.
/// <summary>Combobox's item collections with a root, at L2.</summary>
public sealed class ComboboxCreateItemsIntegrationTests : IDisposable
{
    private static readonly User[] s_users = [new(1, "Alice"), new(2, "Bob"), new(3, "Carol")];

    private static readonly ApiUser[] s_apiUsers =
    [
        new("user-1", "Alice", "Engineer", "alice@example.com"),
        new("user-2", "Bob", "Product manager", "bob@example.com"),
        new("user-3", "Carol", "Designer", "carol@example.com"),
    ];

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    // Covers the paginated async-search use case of Base UI's issue 3818.
    [Fact(DisplayName = "keeps separately fetched selected metadata outside paginated search results")]
    [Trait("Requirement", "CMB-EDGE-12")]
    public async Task Keeps_separately_fetched_selected_metadata_outside_paginated_search_results()
    {
        Employee selected = new("employee-100000", "Grace Hopper", "Platform");
        Employee[] firstPage = [new("employee-1", "Ada Lovelace", "Research"), new("employee-2", "Alan Turing", "Security")];
        Employee[] pageContainingSelection = [new("employee-100000", "Grace Hopper", "Platform"), new("employee-3", "Margaret Hamilton", "Flight software")];
        ComboboxItemCollection<Employee, string> items = null!;
        ComboboxItemCollection<Employee, string> window = null!;
        Dictionary<string, object?> Props(Employee[] searchResults)
        {
            Employee[] known = [selected, .. searchResults.Where(employee => employee.Id != selected.Id)];
            items = ComboboxItems.Create<Employee, string>(known, employee => employee.Id, employee => employee.Name);
            window = ComboboxItems.Create<Employee, string>(searchResults, employee => employee.Id, employee => employee.Name);
            return new() { ["Items"] = items, ["FilteredItems"] = window, ["Value"] = selected.Id, ["DefaultOpen"] = true };
        }

        // The template renders the item the list holds: the window's, or a known one while the input mirrors the selection.
        IRenderedComponent<ComboboxTypedHarness<string>> cut = Render<string>(Props(firstPage), p => p
            .Add(x => x.WithValue, true)
            .Add(x => x.ItemText, id => window.TryGet(id, out Employee? found) ? found!.Name : items.Get(id).Name));
        Assert.Equal("Grace Hopper", cut.InputText());

        await cut.TypeAsync(test, "a");

        Assert.False(cut.HasOption("Grace Hopper"));
        Assert.Equal(2, cut.OptionTexts().Count);
        cut.Instance.ValueChanges.Clear();

        cut.Render(p => p.Add(x => x.RootParameters, Props(pageContainingSelection)));

        Assert.Equal("true", cut.Option("Grace Hopper").GetAttribute("aria-selected"));

        cut.Render(p => p.Add(x => x.RootParameters, Props(firstPage)));

        Assert.False(cut.HasOption("Grace Hopper"));
        Assert.Empty(cut.Instance.ValueChanges);
        Assert.Equal("Grace Hopper", cut.Find("[data-testid=value]").TextContent);
    }

    // The consumer's onValueChange stores the selection and clears the results in one update, as Base UI's App does.
    [Fact(DisplayName = "falls back to the raw selected ID when API results are cleared")]
    [Trait("Requirement", "CMB-EDGE-04")]
    public async Task Falls_back_to_the_raw_selected_ID_when_API_results_are_cleared()
    {
        Dictionary<string, object?> root = new()
        {
            ["Items"] = ApiUserItems(s_apiUsers),
            ["Value"] = null,
            ["DefaultOpen"] = true,
        };
        IRenderedComponent<ComboboxTypedHarness<string>> cut = Render<string>(root, p => p.Add(x => x.ItemText, ApiUserName));
        cut.Instance.OnValue = args =>
        {
            root["Value"] = args.Value;
            root["Items"] = null;
        };

        await cut.ClickOptionAsync("Carol");

        Assert.Equal("user-3", cut.InputText());
        Assert.False(cut.Instance.Root!.Store.IsOpen);
    }

    [Fact(DisplayName = "keeps nullish entries out of the accessors and out of filtered results")]
    [Trait("Requirement", "CMB-EDGE-05")]
    public async Task Keeps_nullish_entries_out_of_the_accessors_and_out_of_filtered_results()
    {
        // A hole in otherwise well-typed data, which is how it reaches a collection in practice.
        User?[] source = [null, s_users[0]];
        List<User?> calls = [];
        IRenderedComponent<ComboboxTypedHarness<int>> cut = Render<int>(new()
        {
            ["Items"] = ComboboxItems.Create<User?, int>(source, user => { calls.Add(user); return user!.Id; }, user => user!.Name),
            ["FilteredItems"] = UserItems([s_users[0]]),
            ["DefaultValue"] = 1,
            ["DefaultOpen"] = true,
        }, p => p.Add(x => x.ItemText, id => s_users.FirstOrDefault(user => user.Id == id)?.Name ?? string.Empty));
        Assert.Equal("Alice", cut.InputText());

        // A hole reaches the template while there is no query (as default(int), where Base UI's callback receives null), but
        // it is dropped as soon as filtering runs.
        await cut.TypeAsync(test, "a");

        Assert.Single(cut.OptionTexts());
        Assert.True(cut.HasOption("Alice"));
        Assert.All(calls, Assert.NotNull);
    }

    [Fact(DisplayName = "treats a non-array items field as item data rather than as a group")]
    [Trait("Requirement", "CMB-API-12")]
    [Trait("Requirement", "CMB-API-68")]
    public async Task Treats_a_non_array_items_field_as_item_data_rather_than_as_a_group()
    {
        // Only an actual group marks a group; unrelated or optional fields stay item data.
        Record[] records = [new(1, "Alice", null), new(2, "Bob", 3)];
        ComboboxItemCollection<Record, int> items = ComboboxItems.Create<Record, int>(records, record => record.Id, record => record.Name);
        IRenderedComponent<ComboboxTypedHarness<int>> cut = Render<int>(new() { ["Items"] = items, ["DefaultOpen"] = true }, p => p.Add(x => x.ItemText, id => items.Get(id).Name));

        Assert.Equal(2, cut.OptionTexts().Count);

        await cut.ClickOptionAsync("Bob");

        Assert.Equal(2, cut.Instance.ValueChanges[^1].Value);
        Assert.Equal("Bob", cut.InputText());
    }

    // The port has no runtime shape detection: a group is a ComboboxItemGroup given to CreateGrouped and passed as ItemGroups
    // (design point 2), the form Base UI's runtime takes for a record whose `items` field holds an array.
    [Fact(DisplayName = "treats an array-valued items field as a group when its declared type is unknown")]
    [Trait("Requirement", "CMB-API-12")]
    [Trait("Requirement", "CMB-API-68")]
    public void Treats_an_array_valued_items_field_as_a_group_when_its_declared_type_is_unknown()
    {
        ComboboxItemGroupCollection<User, int> items = ComboboxItems.CreateGrouped<User, int>([new("people", [s_users[0]])], user => user.Id, user => user.Name);

        IRenderedComponent<ComboboxTypedHarness<int>> cut = Render<int>(new() { ["ItemGroups"] = items, ["DefaultValue"] = 1 }, p => p.Add(x => x.WithList, false));

        Assert.Equal("Alice", cut.InputText());
    }

    [Fact(DisplayName = "renders a sentinel item for the empty selection like any other item")]
    [Trait("Requirement", "CMB-EDGE-05")]
    public void Renders_a_sentinel_item_for_the_empty_selection_like_any_other_item()
    {
        (string Id, string Name)[] withSentinel = [("none", "None"), .. s_apiUsers.Select(user => (user.Id, user.Name))];
        ComboboxItemCollection<(string Id, string Name), string> items = ComboboxItems.Create<(string Id, string Name), string>(withSentinel, item => item.Id, item => item.Name);

        IRenderedComponent<ComboboxTypedHarness<string>> cut = Render<string>(new() { ["Items"] = items, ["DefaultValue"] = "none" }, p => p.Add(x => x.WithList, false));

        Assert.Equal("None", cut.InputText());
    }

    [Fact(DisplayName = "renders an empty list while the data is undefined and fills in once it loads")]
    [Trait("Requirement", "CMB-EDGE-04")]
    public void Renders_an_empty_list_while_the_data_is_undefined_and_fills_in_once_it_loads()
    {
        IRenderedComponent<ComboboxTypedHarness<int>> cut = Render<int>(new() { ["Items"] = null, ["DefaultOpen"] = true }, p => p.Add(x => x.ItemText, UserName));

        Assert.Empty(cut.OptionTexts());

        cut.Render(p => p.Add(x => x.RootParameters, new Dictionary<string, object?> { ["Items"] = UserItems(s_users), ["DefaultOpen"] = true }));

        Assert.Equal(3, cut.OptionTexts().Count);
    }

    [Fact(DisplayName = "resolves and degrades the selected label as the data loads and shrinks")]
    [Trait("Requirement", "CMB-EDGE-04")]
    public void Resolves_and_degrades_the_selected_label_as_the_data_loads_and_shrinks()
    {
        static Dictionary<string, object?> Props(User[]? data) => new() { ["Items"] = data is null ? null : UserItems(data), ["DefaultValue"] = 3 };
        IRenderedComponent<ComboboxTypedHarness<int>> cut = Render<int>(Props(null), p => p.Add(x => x.WithList, false));

        Assert.Equal("3", cut.InputText());

        cut.Render(p => p.Add(x => x.RootParameters, Props(s_users)));

        Assert.Equal("Carol", cut.InputText());

        cut.Render(p => p.Add(x => x.RootParameters, Props([s_users[0]])));

        Assert.Equal("3", cut.InputText());
    }

    // The highlight on mount is the list navigation's, which navigates to the selected index it holds while open and mounted
    // (useListNavigation.ts:389-403; the module's L3 titles), and the item shows the module's report, so the visible highlight
    // is read at L4. The port's half, read here, is the root's seed of that index for an inline list open on mount
    // (AriaCombobox.tsx:454-470), and the report of that index resolving to the selected derived value.
    [Fact(DisplayName = "highlights an initially selected derived value on mount when inline")]
    [Trait("Requirement", "CMB-STA-28")]
    public async Task Highlights_an_initially_selected_derived_value_on_mount_when_inline()
    {
        IRenderedComponent<ComboboxTypedHarness<int>> cut = Render<int>(new()
        {
            ["Items"] = UserItems(s_users),
            ["Inline"] = true,
            ["Open"] = true,
            ["DefaultValue"] = 3,
        }, p => p.Add(x => x.InlineList, true).Add(x => x.ItemText, UserName));

        Assert.Contains("\"selectedIndex\":2", LastListNavigationOptions(), StringComparison.Ordinal);

        await cut.HighlightAsync(test, 2, "Carol");

        Assert.Equal(3, cut.Instance.Highlighted[^1]);
    }

    [Fact(DisplayName = "uses the root locale for filtering")]
    [Trait("Requirement", "CMB-API-15")]
    public async Task Uses_the_root_locale_for_filtering()
    {
        string[] cities = ["Isparta", "İzmir"];
        IRenderedComponent<ComboboxTypedHarness<string>> cut = Render<string>(new()
        {
            ["Items"] = ComboboxItems.Create<string, string>(cities, city => city, city => city),
            ["Culture"] = new CultureInfo("tr"),
            ["DefaultOpen"] = true,
        });

        await cut.TypeAsync(test, "i");

        Assert.False(cut.HasOption("Isparta"));
        Assert.True(cut.HasOption("İzmir"));
    }

    // A re-render with another culture is the consumer's change of language; the collection and its accessor are the same.
    [Fact(DisplayName = "updates a selected label when a stable accessor returns a new result")]
    [Trait("Requirement", "CMB-STA-20")]
    public async Task Updates_a_selected_label_when_a_stable_accessor_returns_a_new_result()
    {
        string language = "en";
        ComboboxItemCollection<User, int> items = ComboboxItems.Create<User, int>(s_users, user => user.Id, user => language == "es" && user.Id == 1 ? "Alicia" : user.Name);
        Dictionary<string, object?> Props() => new() { ["Items"] = items, ["DefaultValue"] = 1, ["Culture"] = new CultureInfo(language) };
        IRenderedComponent<ComboboxTypedHarness<int>> cut = Render<int>(Props(), p => p
            .Add(x => x.WithValue, true)
            .Add(x => x.WithTrigger, true)
            .Add(x => x.ItemText, UserName));
        Assert.Equal("Alice", cut.InputText());
        Assert.Equal("Alice", cut.Find("[data-testid=value]").TextContent);

        language = "es";
        cut.Render(p => p.Add(x => x.RootParameters, Props()));

        Assert.Equal("Alicia", cut.InputText());
        Assert.Equal("Alicia", cut.Find("[data-testid=value]").TextContent);

        await cut.PressTriggerAsync(test);

        Assert.Equal(s_users.Length, cut.OptionTexts().Count);
    }

    // The port's Filter receives the derived value (ADR 08 "Combobox as built"); the consumer reads the source item through
    // the collection, which is what Base UI's filter receives.
    [Fact(DisplayName = "passes source items to a custom root filter")]
    [Trait("Requirement", "CMB-API-14")]
    public async Task Passes_source_items_to_a_custom_root_filter()
    {
        ComboboxItemCollection<User, int> items = UserItems(s_users);
        List<User> calls = [];
        IRenderedComponent<ComboboxTypedHarness<int>> cut = Render<int>(new()
        {
            ["Items"] = items,
            ["Filter"] = (Func<int, string, Func<int, string>?, bool>)((value, _, _) =>
            {
                User user = items.Get(value);
                calls.Add(user);
                return user.Id == 2;
            }),
            ["DefaultOpen"] = true,
        }, p => p.Add(x => x.ItemText, UserName));

        await cut.TypeAsync(test, "x");

        Assert.False(cut.HasOption("Alice"));
        Assert.True(cut.HasOption("Bob"));
        Assert.All(calls, user => Assert.Contains(user, s_users));
        Assert.Equal(new HashSet<int> { 1, 2, 3 }, calls.Select(user => user.Id).ToHashSet());
    }

    [Fact(DisplayName = "labels a derived selection when useFilter receives its value")]
    [Trait("Requirement", "CMB-EDGE-12")]
    public async Task Labels_a_derived_selection_when_useFilter_receives_its_value()
    {
        ComboboxFilter<int> filter = ComboboxFilter.Create(2);
        IRenderedComponent<ComboboxTypedHarness<int>> cut = Render<int>(new()
        {
            ["Items"] = ComboboxItems.Create<User, int>(s_users, user => user.Id, user => user.Name.Trim()),
            ["Filter"] = (Func<int, string, Func<int, string>?, bool>)filter.Contains,
            ["DefaultValue"] = 2,
            ["DefaultOpen"] = true,
        }, p => p.Add(x => x.ItemText, UserName));

        await cut.TypeAsync(test, string.Empty);
        await cut.TypeAsync(test, "ali");

        Assert.True(cut.HasOption("Alice"));
        Assert.False(cut.HasOption("Bob"));
    }

    // The port's Filter receives the derived value; the consumer reads the source item's other fields through the collection.
    [Fact(DisplayName = "lets a custom root filter match source fields that are neither the value nor the label")]
    [Trait("Requirement", "CMB-API-14")]
    public async Task Lets_a_custom_root_filter_match_source_fields_that_are_neither_the_value_nor_the_label()
    {
        Contact[] contacts = [new(1, "Alice", "alice@example.com"), new(2, "Bob", "bob@other.com")];
        ComboboxItemCollection<Contact, int> items = ComboboxItems.Create<Contact, int>(contacts, contact => contact.Id, contact => contact.Name);
        IRenderedComponent<ComboboxTypedHarness<int>> cut = Render<int>(new()
        {
            ["Items"] = items,
            ["Filter"] = (Func<int, string, Func<int, string>?, bool>)((value, query, _) => items.Get(value).Email.Contains(query, StringComparison.Ordinal)),
            ["DefaultOpen"] = true,
        }, p => p.Add(x => x.ItemText, id => items.Get(id).Name));

        await cut.TypeAsync(test, "other.com");

        Assert.False(cut.HasOption("Alice"));
        Assert.True(cut.HasOption("Bob"));
    }

    // Base UI's createItems<User, number | string>: a value type both an id and a typed tag fit, object in C#.
    [Fact(DisplayName = "uses the default label fallback outside the collection")]
    [Trait("Requirement", "CMB-EDGE-12")]
    public void Uses_the_default_label_fallback_outside_the_collection()
    {
        ComboboxItemCollection<User, object> items = ComboboxItems.Create<User, object>(s_users, user => user.Id, user => user.Name);

        IRenderedComponent<ComboboxTypedHarness<object>> cut = Render<object>(new() { ["Items"] = items, ["DefaultValue"] = "New tag" }, p => p.Add(x => x.WithList, false));

        Assert.Equal("New tag", cut.InputText());
    }

    [Fact(DisplayName = "does not stringify null when no value is selected")]
    [Trait("Requirement", "CMB-API-68")]
    public void Does_not_stringify_null_when_no_value_is_selected()
    {
        IRenderedComponent<ComboboxTypedHarness<int>> cut = Render<int>(
            new() { ["Items"] = UserItems(s_users), ["ItemToStringLabel"] = (Func<int, string>)(id => "User " + id.ToString(CultureInfo.InvariantCulture)) },
            p => p.Add(x => x.WithInput, false).Add(x => x.WithList, false).Add(x => x.WithValue, true));

        Assert.Equal(string.Empty, cut.Find("[data-testid=value]").InnerHtml);
    }

    [Fact(DisplayName = "does not treat source value fields as derived selections")]
    [Trait("Requirement", "CMB-EDGE-08")]
    public void Does_not_treat_source_value_fields_as_derived_selections()
    {
        Discount[] source = [new(1, null, "No discount")];
        void Extra(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "span");
            builder.AddAttribute(1, "data-testid", "placeholder");
            builder.OpenComponent<ComboboxValue<int?>>(2);
            builder.AddComponentParameter(3, "Placeholder", (RenderFragment)(inner => inner.AddContent(0, "Choose an option")));
            builder.CloseComponent();
            builder.CloseElement();
            builder.OpenElement(4, "span");
            builder.AddAttribute(5, "data-testid", "value");
            builder.OpenComponent<ComboboxValue<int?>>(6);
            builder.CloseComponent();
            builder.CloseElement();
        }

        IRenderedComponent<ComboboxTypedHarness<int?>> cut = Render<int?>(
            new() { ["Items"] = ComboboxItems.Create<Discount, int?>(source, item => item.Id, item => item.Label) },
            p => p.Add(x => x.WithInput, false).Add(x => x.WithList, false).Add(x => x.Extra, (RenderFragment)Extra));

        Assert.Equal("Choose an option", cut.Find("[data-testid=placeholder]").TextContent);
        Assert.Equal(string.Empty, cut.Find("[data-testid=value]").InnerHtml);
    }

    [Fact(DisplayName = "toggles derived values in multiple mode")]
    [Trait("Requirement", "CMB-PTR-05")]
    public async Task Toggles_derived_values_in_multiple_mode()
    {
        IRenderedComponent<ComboboxTypedHarness<int>> cut = Render<int>(new() { ["Items"] = UserItems(s_users), ["Multiple"] = true, ["DefaultOpen"] = true }, p => p.Add(x => x.ItemText, UserName));

        await cut.ClickOptionAsync("Bob");

        Assert.Equal([2], cut.Instance.ValuesChanges[^1]);
        Assert.Equal("true", cut.Option("Bob").GetAttribute("aria-selected"));

        await cut.ClickOptionAsync("Carol");

        Assert.Equal([2, 3], cut.Instance.ValuesChanges[^1]);

        await cut.ClickOptionAsync("Bob");

        Assert.Equal([3], cut.Instance.ValuesChanges[^1]);
        Assert.Equal("false", cut.Option("Bob").GetAttribute("aria-selected"));
    }

    // Base UI's cases: the serialized derived value ("2") and a derived label ("Bob").
    [Theory(DisplayName = "matches browser autofill against %s")]
    [Trait("Requirement", "CMB-EVT-11")]
    [InlineData("2")]
    [InlineData("Bob")]
    public async Task Matches_browser_autofill(string autofilledValue)
    {
        IRenderedComponent<ComboboxTypedHarness<int>> cut = Render<int>(new() { ["Items"] = UserItems(s_users), ["Name"] = "user" }, p => p.Add(x => x.WithList, false));

        await cut.AutofillAsync(test, autofilledValue);

        Assert.Equal(2, cut.Instance.ValueChanges[^1].Value);
        Assert.Equal("Bob", cut.InputText());
    }

    // One response carries both the result window and the persisted selection, so the label is resolved in the same
    // parameter set that first projects the item it comes from.
    [Fact(DisplayName = "labels a selection that arrives together with its externally filtered item")]
    [Trait("Requirement", "CMB-EDGE-12")]
    public void Labels_a_selection_that_arrives_together_with_its_externally_filtered_item()
    {
        ComboboxItemCollection<ApiUser, string> items = ApiUserItems([]);
        Dictionary<string, object?> Props(ApiUser[] results, string? value) => new() { ["Items"] = items, ["FilteredItems"] = ApiUserItems(results), ["Value"] = value };
        IRenderedComponent<ComboboxTypedHarness<string>> cut = Render<string>(Props([], null), p => p.Add(x => x.WithValue, true).Add(x => x.WithList, false));

        cut.Render(p => p.Add(x => x.RootParameters, Props([s_apiUsers[0]], "user-1")));

        Assert.Equal("Alice", cut.InputText());
        Assert.Equal("Alice", cut.Find("[data-testid=value]").TextContent);
    }

    [Fact(DisplayName = "re-labels an unchanged selection when its item arrives in a later external window")]
    [Trait("Requirement", "CMB-EDGE-12")]
    public void Relabels_an_unchanged_selection_when_its_item_arrives_in_a_later_external_window()
    {
        ComboboxItemCollection<ApiUser, string> items = ApiUserItems([]);
        Dictionary<string, object?> Props(ApiUser[] results) => new() { ["Items"] = items, ["FilteredItems"] = ApiUserItems(results), ["Value"] = "user-1" };
        IRenderedComponent<ComboboxTypedHarness<string>> cut = Render<string>(Props([]), p => p.Add(x => x.WithList, false));

        // Nothing can resolve the label yet, so it degrades to the raw value.
        Assert.Equal("user-1", cut.InputText());

        cut.Render(p => p.Add(x => x.RootParameters, Props([s_apiUsers[0]])));

        Assert.Equal("Alice", cut.InputText());
    }

    [Fact(DisplayName = "uses the custom comparer to label a value from the externally filtered window")]
    [Trait("Requirement", "CMB-EDGE-12")]
    public void Uses_the_custom_comparer_to_label_a_value_from_the_externally_filtered_window()
    {
        static string Lower(User user) => user.Name.ToLowerInvariant();
        IRenderedComponent<ComboboxTypedHarness<string>> cut = Render<string>(new()
        {
            ["Items"] = ComboboxItems.Create<User, string>([s_users[0]], Lower, user => user.Name),
            ["FilteredItems"] = ComboboxItems.Create<User, string>([s_users[1]], Lower, user => user.Name),
            ["DefaultValue"] = "BOB",
            ["IsItemEqualToValue"] = (Func<string, string, bool>)((item, value) => string.Equals(item, value, StringComparison.OrdinalIgnoreCase)),
            ["DefaultOpen"] = true,
        }, p => p.Add(x => x.ItemText, value => s_users.First(user => Lower(user) == value).Name));

        Assert.Equal("true", cut.Option("Bob").GetAttribute("aria-selected"));
        Assert.Equal("Bob", cut.InputText());
    }

    [Fact(DisplayName = "keeps an external result visible when the selected item is absent from known data")]
    [Trait("Requirement", "CMB-EDGE-12")]
    public void Keeps_an_external_result_visible_when_the_selected_item_is_absent_from_known_data()
    {
        IRenderedComponent<ComboboxTypedHarness<string>> cut = Render<string>(new()
        {
            ["Items"] = ApiUserItems([s_apiUsers[1]]),
            ["FilteredItems"] = ApiUserItems([s_apiUsers[0]]),
            ["DefaultValue"] = "user-1",
            ["DefaultOpen"] = true,
        }, p => p.Add(x => x.ItemText, ApiUserName));

        Assert.Equal("Alice", cut.InputText());
        Assert.True(cut.HasOption("Alice"));
        Assert.False(cut.HasOption("Bob"));
    }

    [Fact(DisplayName = "projects externally filtered source items into the derived value domain")]
    [Trait("Requirement", "CMB-API-13")]
    public async Task Projects_externally_filtered_source_items_into_the_derived_value_domain()
    {
        IRenderedComponent<ComboboxTypedHarness<int>> cut = Render<int>(new()
        {
            ["Items"] = UserItems(s_users),
            ["FilteredItems"] = UserItems([s_users[2]]),
            ["DefaultOpen"] = true,
        }, p => p.Add(x => x.ItemText, UserName));

        Assert.Single(cut.OptionTexts());

        await cut.ClickOptionAsync("Carol");

        Assert.Equal(3, cut.Instance.ValueChanges[^1].Value);
    }

    [Fact(DisplayName = "degrades the label of an externally filtered item after its window is gone")]
    [Trait("Requirement", "CMB-EDGE-04")]
    public async Task Degrades_the_label_of_an_externally_filtered_item_after_its_window_is_gone()
    {
        User external = new(99, "External user");
        ComboboxItemCollection<User, int> items = UserItems(s_users[..1]);
        Dictionary<string, object?> Props(User[] results) => new() { ["Items"] = items, ["FilteredItems"] = UserItems(results), ["DefaultOpen"] = true };
        IRenderedComponent<ComboboxTypedHarness<int>> cut = Render<int>(Props([external]), p => p.Add(x => x.ItemText, id => id == external.Id ? external.Name : UserName(id)));

        await cut.ClickOptionAsync("External user");

        Assert.Equal("External user", cut.InputText());

        // Nothing from a past window is remembered: keep the item in the data or supply itemToStringLabel to keep the label.
        cut.Render(p => p.Add(x => x.RootParameters, Props([])));

        Assert.Equal("99", cut.InputText());
    }

    [Fact(DisplayName = "relabels a borrowed value when a later window carries a fresher item")]
    [Trait("Requirement", "CMB-EDGE-12")]
    public void Relabels_a_borrowed_value_when_a_later_window_carries_a_fresher_item()
    {
        ComboboxItemCollection<User, int> items = UserItems(s_users[..1]);
        Dictionary<string, object?> Props(User[] results) => new() { ["Items"] = items, ["FilteredItems"] = UserItems(results), ["Value"] = 99 };
        IRenderedComponent<ComboboxTypedHarness<int>> cut = Render<int>(Props([new(99, "Ann")]), p => p.Add(x => x.WithList, false));

        Assert.Equal("Ann", cut.InputText());

        // The same record comes back renamed: the label must follow the newer item, not the first one the collection
        // happened to borrow.
        cut.Render(p => p.Add(x => x.RootParameters, Props([new(99, "Anna")])));

        Assert.Equal("Anna", cut.InputText());
    }

    // Two render roots share one collection, as two Base UI roots share a module-scope one.
    [Fact(DisplayName = "resolves each root against its own external window when the collection is shared")]
    [Trait("Requirement", "CMB-EDGE-12")]
    public void Resolves_each_root_against_its_own_external_window_when_the_collection_is_shared()
    {
        ComboboxItemCollection<Person, string> shared = PersonItems([]);
        Dictionary<string, object?> Props(Person[] results) => new() { ["Items"] = shared, ["FilteredItems"] = PersonItems(results), ["Value"] = "user-1" };
        IRenderedComponent<ComboboxTypedHarness<string>> a = Render<string>(Props([new("user-1", "Alice")]), p => p.Add(x => x.WithList, false));
        IRenderedComponent<ComboboxTypedHarness<string>> b = Render<string>(Props([new("user-1", "Alicia")]), p => p.Add(x => x.WithList, false));

        Assert.Equal("Alice", a.InputText());
        Assert.Equal("Alicia", b.InputText());

        // Window items never touch the shared collection: with its own window gone, the first root degrades to the raw value
        // rather than picking up the label from the sibling's window.
        a.Render(p => p.Add(x => x.RootParameters, Props([])));

        Assert.Equal("user-1", a.InputText());
        Assert.Equal("Alicia", b.InputText());
    }

    [Fact(DisplayName = "labels multiple selected values from the current external window")]
    [Trait("Requirement", "CMB-EDGE-12")]
    public void Labels_multiple_selected_values_from_the_current_external_window()
    {
        ComboboxItemCollection<Person, string> shared = PersonItems([]);
        Dictionary<string, object?> Props(Person[] results) => new()
        {
            ["Multiple"] = true,
            ["Items"] = shared,
            ["FilteredItems"] = PersonItems(results),
            ["Values"] = (IReadOnlyList<string>)["user-1", "user-2"],
        };
        IRenderedComponent<ComboboxTypedHarness<string>> cut = Render<string>(Props([new("user-1", "Alice"), new("user-2", "Bob")]), p => p
            .Add(x => x.WithValue, true)
            .Add(x => x.WithList, false));

        Assert.Equal("Alice, Bob", cut.Find("[data-testid=value]").TextContent);

        // Each selected value degrades independently when its item leaves the window.
        cut.Render(p => p.Add(x => x.RootParameters, Props([new("user-2", "Bob")])));

        Assert.Equal("user-1, Bob", cut.Find("[data-testid=value]").TextContent);
    }

    // Base UI's renderToString: HtmlRenderer renders the same HTML a prerender does, and runs no OnAfterRender.
    [Fact(DisplayName = "resolves a server-rendered defaultValue label from the external window")]
    [Trait("Requirement", "CMB-STA-19")]
    [Trait("Requirement", "CMB-EDGE-06")]
    public async Task Resolves_a_server_rendered_defaultValue_label_from_the_external_window()
    {
        await using HtmlRenderer renderer = new(test.Context.Services, NullLoggerFactory.Instance);
        string html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            HtmlRootComponent root = await renderer.RenderComponentAsync<ComboboxTypedHarness<string>>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(ComboboxTypedHarness<string>.RootParameters)] = new Dictionary<string, object?>
                {
                    ["Items"] = PersonItems([]),
                    ["FilteredItems"] = PersonItems([new("user-1", "Alice")]),
                    ["DefaultValue"] = "user-1",
                },
                [nameof(ComboboxTypedHarness<string>.WithList)] = false,
            }));
            return root.ToHtmlString();
        });
        using AngleSharp.Html.Dom.IHtmlDocument document = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html);

        Assert.Equal("Alice", document.QuerySelector("[data-testid=input]")!.GetAttribute("value"));
    }

    [Fact(DisplayName = "resolves virtualized collection items to their derived value")]
    [Trait("Requirement", "CMB-API-22")]
    [Trait("Requirement", "CMB-API-66")]
    public async Task Resolves_virtualized_collection_items_to_their_derived_value()
    {
        IRenderedComponent<ComboboxTypedHarness<int>> cut = Render<int>(new() { ["Items"] = UserItems(s_users), ["Virtualized"] = true, ["DefaultOpen"] = true }, p => p.Add(x => x.ItemText, UserName));

        await cut.ClickOptionAsync("Bob");

        Assert.Equal(2, cut.Instance.ValueChanges[^1].Value);
    }

    // Base UI's cases, one value type each: 0 (int), '' (string), false (bool), 10n (BigInteger).
    [Theory(DisplayName = "resolves $label as a primitive selection")]
    [Trait("Requirement", "CMB-API-68")]
    [InlineData("Zero")]
    [InlineData("Empty string")]
    [InlineData("False")]
    [InlineData("Bigint")]
    public void Resolves_a_primitive_selection(string label)
    {
        switch (label)
        {
            case "Zero":
                AssertPrimitiveSelection(0, label, "0");
                break;
            case "Empty string":
                AssertPrimitiveSelection(string.Empty, label, string.Empty);
                break;
            case "False":
                AssertPrimitiveSelection(false, label, "false");
                break;
            default:
                AssertPrimitiveSelection(new BigInteger(10), label, "10");
                break;
        }
    }

    [Fact(DisplayName = "resolves a duplicated derived value to the same label regardless of lookup order")]
    [Trait("Requirement", "CMB-EDGE-02")]
    public void Resolves_a_duplicated_derived_value_to_the_same_label_regardless_of_lookup_order()
    {
        CapturingLoggerProvider logs = new();
        test.Context.Services.AddLogging(builder => builder.AddProvider(logs));
        User[] duplicated = [new(1, "First"), new(2, "Bob"), new(1, "Clone"), new(2, "Copy")];
        Dictionary<string, object?> Props(int value) => new() { ["Items"] = UserItems(duplicated), ["Value"] = value };

        // Resolving an absent value first exercises the fallback path, which must not change what the duplicated value
        // resolves to: the first occurrence always wins.
        IRenderedComponent<ComboboxTypedHarness<int>> cut = Render<int>(Props(99), p => p.Add(x => x.WithList, false));
        cut.Render(p => p.Add(x => x.RootParameters, Props(1)));

        Assert.Equal("First", cut.InputText());
        Assert.Contains(logs.Messages, message => message.Contains("derived the value 1", StringComparison.Ordinal));
        Assert.Contains(logs.Messages, message => message.Contains("derived the value 2", StringComparison.Ordinal));
    }

    // useFilteredItems().slice(0, 1) is the List's ItemContent with the harness's Window of one item.
    [Fact(DisplayName = "resolves externally virtualized item indexes and callbacks in the derived value domain")]
    [Trait("Requirement", "CMB-API-66")]
    public async Task Resolves_externally_virtualized_item_indexes_and_callbacks_in_the_derived_value_domain()
    {
        IRenderedComponent<ComboboxTypedHarness<int>> cut = Render<int>(new() { ["Items"] = UserItems(s_users), ["Virtualized"] = true, ["DefaultOpen"] = true }, p => p
            .Add(x => x.ItemText, UserName)
            .Add(x => x.Window, 1));

        await cut.TypeAsync(test, "bo");
        await cut.HighlightAsync(test, 0, "Bob");

        Assert.Equal(2, cut.Instance.Highlighted[^1]);

        await cut.EnterAsync(test, "Bob");

        Assert.Equal(2, cut.Instance.ValueChanges[^1].Value);
    }

    private static ComboboxItemCollection<User, int> UserItems(User[] users) => ComboboxItems.Create<User, int>(users, user => user.Id, user => user.Name);

    private static ComboboxItemCollection<ApiUser, string> ApiUserItems(ApiUser[] users) => ComboboxItems.Create<ApiUser, string>(users, user => user.Id, user => user.Name);

    private static ComboboxItemCollection<Person, string> PersonItems(Person[] people) => ComboboxItems.Create<Person, string>(people, person => person.Id, person => person.Name);

    private static string UserName(int id) => s_users.First(user => user.Id == id).Name;

    private static string ApiUserName(string id) => s_apiUsers.First(user => user.Id == id).Name;

    private void AssertPrimitiveSelection<TValue>(TValue value, string label, string serialized)
    {
        ComboboxItemCollection<(TValue Value, string Label), TValue> items = ComboboxItems.Create<(TValue Value, string Label), TValue>([(value, label)], item => item.Value, item => item.Label);

        IRenderedComponent<ComboboxTypedHarness<TValue>> cut = Render<TValue>(new()
        {
            ["Items"] = items,
            ["DefaultValue"] = value,
            ["Name"] = "choice",
            ["DefaultOpen"] = true,
        }, p => p
            .Add(x => x.WithValue, true)
            .Add(x => x.ValuePlaceholder, "Pick one")
            .Add(x => x.ItemText, _ => label));

        Assert.Equal(label, cut.InputText());
        Assert.Equal(label, cut.Find("[data-testid=value]").TextContent);
        Assert.Equal("true", cut.Option(label).GetAttribute("aria-selected"));
        Assert.Equal(serialized, cut.Find("input[name=choice]").GetAttribute("value") ?? string.Empty);
    }

    // The options the list navigation holds: its attach's, then its updates'.
    private string LastListNavigationOptions()
    {
        string last = string.Empty;
        foreach (JSRuntimeInvocation attach in test.Composite.Invocations["attach"])
        {
            if (attach.Arguments[1] is string json && json.Contains("\"virtual\":true", StringComparison.Ordinal))
            {
                last = json;
            }
        }

        foreach (JSRuntimeInvocation update in test.CompositeHandle.Invocations["update"])
        {
            if (update.Arguments[0] is string json && json.Contains("\"virtual\":true", StringComparison.Ordinal))
            {
                last = json;
            }
        }

        return last;
    }

    private IRenderedComponent<ComboboxTypedHarness<TValue>> Render<TValue>(Dictionary<string, object?> root, Action<ComponentParameterCollectionBuilder<ComboboxTypedHarness<TValue>>>? configure = null) =>
        test.Context.Render<ComboboxTypedHarness<TValue>>(p =>
        {
            p.Add(x => x.RootParameters, root);
            configure?.Invoke(p);
        });

    private sealed record User(int Id, string Name);

    private sealed record ApiUser(string Id, string Name, string Role, string Email);

    private sealed record Person(string Id, string Name);

    private sealed record Employee(string Id, string Name, string Department);

    private sealed record Contact(int Id, string Name, string Email);

    private sealed record Record(int Id, string Name, object? Items);

    private sealed record Discount(int Id, int? Value, string Label);

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public List<string> Messages { get; } = [];

        /// <inheritdoc />
        public ILogger CreateLogger(string categoryName) => new Logger(this);

        /// <inheritdoc />
        public void Dispose()
        {
        }

        private sealed class Logger(CapturingLoggerProvider provider) : ILogger
        {
            /// <inheritdoc />
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            /// <inheritdoc />
            public bool IsEnabled(LogLevel logLevel) => true;

            /// <inheritdoc />
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => provider.Messages.Add(formatter(state, exception));
        }
    }
}

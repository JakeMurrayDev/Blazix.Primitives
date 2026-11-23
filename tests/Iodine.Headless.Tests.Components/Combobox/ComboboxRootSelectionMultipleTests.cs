using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// ComboboxRoot.test.tsx's `selection behavior > multiple` titles at L2 (batch A). As in the single titles, the highlight is
// the store's ActiveIndex (Base UI's ActiveIndexProbe) and the selected index its SelectedIndex (SelectedIndexProbe); the
// highlight the module gives an opening list is the selected index C# sends it (ListSelectedIndex), and a highlight C#
// decides is the request it sends (LastHighlightRequest). user.click on an option moves the pointer onto it first, which the
// list module reports as a hover highlight (PointerClickOptionAsync); fireEvent.click does not. Without Items the items'
// order is the list module's report (ReportOrderAsync).
/// <summary>Combobox's root at L2: multiple selection.</summary>
public sealed class ComboboxRootSelectionMultipleTests : IDisposable
{
    private static readonly string[] s_fruits = ["apple", "banana", "cherry"];

    private static readonly string[] s_appleBanana = ["apple", "banana"];

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "should handle multiple selection")]
    [Trait("Requirement", "CMB-API-04")]
    [Trait("Requirement", "CMB-EVT-03")]
    public async Task Should_handle_multiple_selection()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = test.Context.Render<ComboboxRootBatchAHost>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Multiple"] = true })
            .Add(x => x.InlineList, true)
            .Add(x => x.StaticItems, (IReadOnlyList<string>)["a", "b", "c"]));

        await cut.PointerClickOptionAsync(test, "a");

        Assert.Equal(["a"], Assert.Single(cut.Instance.ValuesChanges).Values);

        await cut.PointerClickOptionAsync(test, "b");

        Assert.Equal(2, cut.Instance.ValuesChanges.Count);
        Assert.Equal(["a", "b"], cut.Instance.ValuesChanges[1].Values);
    }

    // `apple` renders first but sits in the middle of the value array, so only the rendered order points at it.
    [Fact(DisplayName = "anchors the highlight to the first selected item in rendered order regardless of value order")]
    [Trait("Requirement", "CMB-STA-25")]
    public async Task Anchors_the_highlight_to_the_first_selected_item_in_rendered_order_regardless_of_value_order()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = RenderMultiplePopup(["banana", "apple", "cherry"]);

        await cut.PressTriggerAsync(test);

        Assert.Equal(0, cut.Store().State.SelectedIndex);
        Assert.Equal(0, cut.ListSelectedIndex());
    }

    [Fact(DisplayName = "anchors the highlight to the first selected item with individually rendered items")]
    [Trait("Requirement", "CMB-STA-25")]
    public async Task Anchors_the_highlight_to_the_first_selected_item_with_individually_rendered_items()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = test.Context.Render<ComboboxRootBatchAHost>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Multiple"] = true, ["DefaultValues"] = (IReadOnlyList<string>)["banana", "apple", "cherry"] })
            .Add(x => x.StaticItems, (IReadOnlyList<string>)s_fruits));

        await cut.PressInputAsync(test);
        await cut.ReportOrderAsync(test);

        Assert.Equal(0, cut.Store().State.SelectedIndex);
        Assert.Equal(0, cut.ListSelectedIndex());
    }

    // `spinach` renders first but is not the last value, so the rendered order and the end of the value array differ.
    [Fact(DisplayName = "anchors to the first selected item in rendered order across groups")]
    [Trait("Requirement", "CMB-STA-25")]
    public async Task Anchors_to_the_first_selected_item_in_rendered_order_across_groups()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = test.Context.Render<ComboboxRootBatchAHost>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Multiple"] = true, ["DefaultValues"] = (IReadOnlyList<string>)["spinach", "plum"] })
            .Add(x => x.ListContent, (RenderFragment)GroupedList));

        await cut.PressInputAsync(test);
        await cut.ReportOrderAsync(test);

        Assert.Equal("spinach", cut.OptionTexts()[1]);
        Assert.Equal(1, cut.Store().State.SelectedIndex);
        Assert.Equal(1, cut.ListSelectedIndex());
    }

    // A controlled consumer may decline the change without calling cancel(): the query still clears, and the highlight
    // stays on the pressed item.
    [Fact(DisplayName = "keeps the highlight on an item a controlled value refused to select")]
    [Trait("Requirement", "CMB-API-01")]
    [Trait("Requirement", "CMB-EVT-07")]
    public async Task Keeps_the_highlight_on_an_item_a_controlled_value_refused_to_select()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = test.Context.Render<ComboboxRootBatchAHost>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Items"] = (IReadOnlyList<string>)s_fruits, ["Multiple"] = true })
            .Add(x => x.ControlValues, true)
            .Add(x => x.InitialValues, (IReadOnlyList<string>)["apple"])
            .Add(x => x.WithTrigger, true)
            .Add(x => x.TriggerValue, true)
            .Add(x => x.InputInsidePopup, true));
        cut.Instance.RefuseValueChanges = true;
        await cut.PressTriggerAsync(test);
        await cut.TypeAsync(test, "banana");
        Assert.DoesNotContain("apple", cut.OptionTexts());

        await cut.PointerClickOptionAsync(test, "banana");

        Assert.Equal(string.Empty, cut.InputText());
        Assert.Equal("banana", cut.OptionTexts()[1]);
        Assert.Equal(1, cut.Store().State.ActiveIndex);
        Assert.Equal("false", cut.Option("banana").GetAttribute("aria-selected"));
    }

    [Fact(DisplayName = "keeps the toggled item highlighted when selecting with the keyboard while filtering")]
    [Trait("Requirement", "CMB-STA-26")]
    public async Task Keeps_the_toggled_item_highlighted_when_selecting_with_the_keyboard_while_filtering()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = RenderMultiplePopup(s_appleBanana);
        await cut.PressTriggerAsync(test);
        await cut.TypeAsync(test, "cherry");
        Assert.DoesNotContain("apple", cut.OptionTexts());

        await cut.HighlightAsync(test, 0, "cherry");
        Assert.Equal(0, cut.Store().State.ActiveIndex);

        await cut.EnterAsync(test, "cherry");

        Assert.Equal(string.Empty, cut.InputText());
        Assert.Equal("cherry", cut.OptionTexts()[2]);
        Assert.Equal(2, cut.Store().State.ActiveIndex);
    }

    [Fact(DisplayName = "moves the anchor when the anchor item value changes")]
    [Trait("Requirement", "CMB-STA-27")]
    public async Task Moves_the_anchor_when_the_anchor_item_value_changes()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = RenderKeptMounted(positional: true);
        await cut.ReportOrderAsync(test);
        Assert.Equal(0, cut.Store().State.SelectedIndex);

        await cut.Instance.SetStaticItemsAsync(["date", "banana", "cherry"]);
        await cut.ReportOrderAsync(test);

        Assert.Equal(2, cut.Store().State.SelectedIndex);
    }

    [Fact(DisplayName = "moves the anchor to the next selected item when the anchor item leaves the list")]
    [Trait("Requirement", "CMB-STA-27")]
    public async Task Moves_the_anchor_to_the_next_selected_item_when_the_anchor_item_leaves_the_list()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = RenderKeptMounted(positional: false);
        await cut.ReportOrderAsync(test);
        Assert.Equal(0, cut.Store().State.SelectedIndex);

        await cut.Instance.SetStaticItemsAsync(["banana", "cherry"]);
        await cut.ReportOrderAsync(test);

        // `banana` now occupies index 0 but is not selected, so `cherry` takes the anchor.
        Assert.Equal(1, cut.Store().State.SelectedIndex);
    }

    [Fact(DisplayName = "restores the anchor rather than the toggled item when deselecting while filtering")]
    [Trait("Requirement", "CMB-STA-26")]
    public async Task Restores_the_anchor_rather_than_the_toggled_item_when_deselecting_while_filtering()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = RenderMultiplePopup(s_appleBanana);
        await cut.PressTriggerAsync(test);
        await cut.TypeAsync(test, "banana");
        Assert.DoesNotContain("apple", cut.OptionTexts());

        await cut.PointerClickOptionAsync(test, "banana");

        Assert.Equal(string.Empty, cut.InputText());
        Assert.Equal("apple", cut.OptionTexts()[0]);
        Assert.Equal(0, cut.Store().State.ActiveIndex);
    }

    [Fact(DisplayName = "restores the highlight to the first selected item when clearing (input inside popup)")]
    [Trait("Requirement", "CMB-STA-26")]
    public async Task Restores_the_highlight_to_the_first_selected_item_when_clearing()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = RenderMultiplePopup(s_appleBanana);
        await cut.PressTriggerAsync(test);
        await cut.TypeAsync(test, "banana");
        Assert.DoesNotContain("apple", cut.OptionTexts());

        cut.Instance.Highlights.Clear();
        await cut.ClearAsync(test);

        Assert.Equal(0, cut.Store().State.ActiveIndex);
        Assert.Equal(0, test.LastHighlightRequest());
        Assert.Equal([("apple", EventReason.None, 0)], cut.Instance.Highlights);
    }

    [Fact(DisplayName = "keeps the popup open and highlights the newly selected item after clearing its query")]
    [Trait("Requirement", "CMB-STA-26")]
    [Trait("Requirement", "CMB-EVT-10")]
    public async Task Keeps_the_popup_open_and_highlights_the_newly_selected_item_after_clearing_its_query()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = RenderMultiplePopup(["apple"]);
        await cut.PressTriggerAsync(test);
        await cut.TypeAsync(test, "cherry");

        cut.Instance.Highlights.Clear();
        await cut.PointerClickOptionAsync(test, "cherry");

        Assert.Equal(string.Empty, cut.InputText());
        Assert.True(cut.HasListbox());
        Assert.Equal("true", cut.Option("apple").GetAttribute("aria-selected"));
        Assert.Equal("true", cut.Option("cherry").GetAttribute("aria-selected"));
        Assert.Equal(2, cut.Store().State.ActiveIndex);
        Assert.Contains(("cherry", EventReason.None, 2), cut.Instance.Highlights);
    }

    // The open highlights apple through the selected index the module receives; the click on banana is preceded by the
    // pointer's hover highlight.
    [Fact(DisplayName = "keeps the toggled item highlighted when deselecting a selected item")]
    [Trait("Requirement", "CMB-PTR-05")]
    public async Task Keeps_the_toggled_item_highlighted_when_deselecting_a_selected_item()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = RenderMultiplePopup(s_appleBanana);
        await cut.PressTriggerAsync(test);
        Assert.Equal(0, cut.ListSelectedIndex());

        await cut.PointerClickOptionAsync(test, "banana");

        Assert.Equal("false", cut.Option("banana").GetAttribute("aria-selected"));
        Assert.True(cut.HasListbox());
        Assert.Equal("true", cut.Option("apple").GetAttribute("aria-selected"));
        Assert.Equal(1, cut.Store().State.ActiveIndex);
    }

    [Fact(DisplayName = "clears the highlight when filtering deselects the only selected item")]
    [Trait("Requirement", "CMB-STA-27")]
    public async Task Clears_the_highlight_when_filtering_deselects_the_only_selected_item()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = RenderMultiplePopup(["banana"]);
        await cut.PressTriggerAsync(test);
        await cut.TypeAsync(test, "banana");

        await cut.PointerClickOptionAsync(test, "banana");

        Assert.Equal(string.Empty, cut.InputText());
        Assert.Equal("false", cut.Option("banana").GetAttribute("aria-selected"));
        Assert.Null(cut.Store().State.ActiveIndex);
        Assert.Equal(-1, test.LastHighlightRequest());
    }

    // The reopened list's highlight of apple is the module's, from the selected index it receives.
    [Fact(DisplayName = "clears a closing query and restores the first selected item on reopen")]
    [Trait("Requirement", "CMB-STA-30")]
    public async Task Clears_a_closing_query_and_restores_the_first_selected_item_on_reopen()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = RenderMultiplePopup(s_appleBanana);
        await cut.PressTriggerAsync(test);
        await cut.TypeAsync(test, "cherry");
        Assert.DoesNotContain("banana", cut.OptionTexts());

        await cut.EscapeAsync(test);
        await cut.CompleteCloseAsync();
        Assert.False(cut.HasListbox());

        await cut.PressTriggerAsync(test);

        Assert.Equal(string.Empty, cut.InputText());
        Assert.Equal(["apple", "banana", "cherry"], cut.OptionTexts());
        Assert.Equal(0, cut.Store().State.SelectedIndex);
        Assert.Equal(0, cut.ListSelectedIndex());
    }

    // Opening already highlights the first selected item in this layout, so clearing the query returns to the same anchor.
    [Fact(DisplayName = "restores the highlight when clearing with the input outside the popup")]
    [Trait("Requirement", "CMB-STA-26")]
    public async Task Restores_the_highlight_when_clearing_with_the_input_outside_the_popup()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = test.Context.Render<ComboboxRootBatchAHost>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?>
            {
                ["Items"] = (IReadOnlyList<string>)s_fruits,
                ["Multiple"] = true,
                ["DefaultValues"] = (IReadOnlyList<string>)s_appleBanana,
            }));
        await cut.PressInputAsync(test);
        await cut.TypeAsync(test, "banana");
        Assert.DoesNotContain("apple", cut.OptionTexts());

        await cut.ClearAsync(test);

        Assert.Equal("apple", cut.OptionTexts()[0]);
        Assert.Equal(0, cut.Store().State.ActiveIndex);
        Assert.Equal(0, test.LastHighlightRequest());
    }

    // The external Clear button is pressed with user.click: its press outside the popup closes it, then its click sets the
    // value.
    [Fact(DisplayName = "resets selectedIndex when clearing all selections while open")]
    [Trait("Requirement", "CMB-STA-25")]
    public async Task Resets_selectedIndex_when_clearing_all_selections_while_open()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = RenderControlledValues(s_appleBanana, withItems: true);
        Assert.False(cut.HasListbox());

        await cut.PressInputAsync(test);

        Assert.True(cut.HasListbox());
        Assert.Equal(0, cut.Store().State.SelectedIndex);

        await cut.OutsidePressAsync(test);
        await cut.Instance.SetValuesAsync([]);

        Assert.Null(cut.Store().State.SelectedIndex);
    }

    // The external button is pressed with user.click: its press outside the popup closes it, then its click sets the value.
    [Fact(DisplayName = "re-syncs selectedIndex after an external controlled update when closing")]
    [Trait("Requirement", "CMB-STA-25")]
    public async Task Re_syncs_selectedIndex_after_an_external_controlled_update_when_closing()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = RenderControlledValues(["apple"], withItems: true);
        await cut.PressInputAsync(test);
        Assert.True(cut.HasListbox());
        Assert.Equal(0, cut.Store().State.SelectedIndex);

        await cut.OutsidePressAsync(test);
        await cut.Instance.SetValuesAsync(["cherry"]);
        await cut.CompleteCloseAsync();

        Assert.Equal(2, cut.Store().State.SelectedIndex);
        Assert.False(cut.HasListbox());

        await cut.PressInputAsync(test);

        Assert.True(cut.HasListbox());
        Assert.Equal(2, cut.Store().State.SelectedIndex);
    }

    [Fact(DisplayName = "clears selectedIndex when all values are removed externally while closed (no items prop)")]
    [Trait("Requirement", "CMB-STA-25")]
    public async Task Clears_selectedIndex_when_all_values_are_removed_externally_while_closed()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = RenderControlledValues(s_appleBanana, withItems: false);
        await cut.PressInputAsync(test);
        await cut.ReportOrderAsync(test);
        Assert.True(cut.HasListbox());
        Assert.Equal(0, cut.Store().State.SelectedIndex);

        await cut.EscapeAsync(test);
        await cut.CompleteCloseAsync();
        Assert.False(cut.HasListbox());

        await cut.Instance.SetValuesAsync([]);
        Assert.Null(cut.Store().State.SelectedIndex);

        await cut.PressInputAsync(test);
        await cut.ReportOrderAsync(test);

        Assert.True(cut.HasListbox());
        Assert.NotEqual(1, cut.Store().State.ActiveIndex);
        Assert.Null(cut.ListSelectedIndex());
    }

    [Fact(DisplayName = "should create multiple hidden inputs for form submission")]
    [Trait("Requirement", "CMB-API-25")]
    [Trait("Requirement", "CMB-DOM-19")]
    public void Should_create_multiple_hidden_inputs_for_form_submission()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = test.Context.Render<ComboboxRootBatchAHost>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?>
            {
                ["Multiple"] = true,
                ["Values"] = (IReadOnlyList<string>)["a", "b", "c"],
                ["Name"] = "languages",
            })
            .Add(x => x.StaticItems, (IReadOnlyList<string>)["a", "b"]));

        foreach (string item in new[] { "a", "b", "c" })
        {
            IElement input = Assert.Single(cut.FindAll("input"), element => element.GetAttribute("value") == item);
            Assert.Equal("hidden", input.GetAttribute("type"));
            Assert.Equal("INPUT", input.TagName);
        }
    }

    // FormData leaves out disabled controls: no enabled control carries the name.
    [Fact(DisplayName = "does not submit multiple values when disabled")]
    [Trait("Requirement", "CMB-API-30")]
    [Trait("Requirement", "CMB-DOM-19")]
    public void Does_not_submit_multiple_values_when_disabled()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = test.Context.Render<ComboboxRootBatchAHost>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?>
            {
                ["Multiple"] = true,
                ["Disabled"] = true,
                ["Name"] = "x",
                ["DefaultValues"] = (IReadOnlyList<string>)["a"],
            }));

        Assert.DoesNotContain(cut.FindAll("[name=x]"), element => !element.HasAttribute("disabled"));
    }

    private static void GroupedList(RenderTreeBuilder builder)
    {
        builder.OpenComponent<ComboboxList<string>>(0);
        builder.AddComponentParameter(1, "data-testid", "list");
        builder.AddComponentParameter(2, nameof(ComboboxList<string>.ChildContent), (RenderFragment)(list =>
        {
            Group(list, 0, "Vegetables", ["artichoke", "spinach"]);
            Group(list, 1, "Fruits", ["apple", "plum"]);
        }));
        builder.CloseComponent();
    }

    private static void Group(RenderTreeBuilder builder, int key, string label, string[] items)
    {
        builder.OpenComponent<ComboboxGroup<string>>(0);
        builder.SetKey(key);
        builder.AddComponentParameter(1, nameof(ComboboxGroup<string>.ChildContent), (RenderFragment)(group =>
        {
            group.OpenComponent<ComboboxGroupLabel>(0);
            group.AddComponentParameter(1, nameof(ComboboxGroupLabel.ChildContent), (RenderFragment)(text => text.AddContent(0, label)));
            group.CloseComponent();
            foreach (string item in items)
            {
                group.OpenComponent<ComboboxItem<string>>(2);
                group.SetKey(item);
                group.AddComponentParameter(3, nameof(ComboboxItem<string>.Value), item);
                group.AddComponentParameter(4, nameof(ComboboxItem<string>.ChildContent), (RenderFragment)(text => text.AddContent(0, item)));
                group.CloseComponent();
            }
        }));
        builder.CloseComponent();
    }

    // ComboboxRoot.test.tsx:100-136 (MultiplePopupCombobox): Items apple, banana and cherry, multiple, a Trigger, and the
    // input inside the popup.
    private IRenderedComponent<ComboboxRootBatchAHost> RenderMultiplePopup(IReadOnlyList<string> defaultValues) =>
        test.Context.Render<ComboboxRootBatchAHost>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?>
            {
                ["Items"] = (IReadOnlyList<string>)s_fruits,
                ["Multiple"] = true,
                ["DefaultValues"] = defaultValues,
            })
            .Add(x => x.WithTrigger, true)
            .Add(x => x.InputInsidePopup, true));

    // ComboboxRoot.test.tsx:3261-3282 and :3297-3319: multiple, apple and cherry selected, a kept-mounted static list.
    private IRenderedComponent<ComboboxRootBatchAHost> RenderKeptMounted(bool positional) =>
        test.Context.Render<ComboboxRootBatchAHost>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Multiple"] = true, ["DefaultValues"] = (IReadOnlyList<string>)["apple", "cherry"] })
            .Add(x => x.StaticItems, (IReadOnlyList<string>)s_fruits)
            .Add(x => x.KeepMounted, true)
            .Add(x => x.Positional, positional));

    private IRenderedComponent<ComboboxRootBatchAHost> RenderControlledValues(IReadOnlyList<string> values, bool withItems) =>
        test.Context.Render<ComboboxRootBatchAHost>(p =>
        {
            p.Add(x => x.RootParameters, withItems
                ? new Dictionary<string, object?> { ["Items"] = (IReadOnlyList<string>)s_fruits, ["Multiple"] = true }
                : new Dictionary<string, object?> { ["Multiple"] = true });
            p.Add(x => x.ControlValues, true).Add(x => x.InitialValues, values);
            if (!withItems)
            {
                p.Add(x => x.StaticItems, (IReadOnlyList<string>)s_fruits);
            }
        });
}

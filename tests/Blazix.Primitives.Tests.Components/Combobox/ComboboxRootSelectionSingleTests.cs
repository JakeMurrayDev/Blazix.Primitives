using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// ComboboxRoot.test.tsx's `selection behavior > single` titles at L2 (batch A; the twelve `skipIf(isJSDOM)` titles are L4).
// The highlight is the store's: Base UI's ActiveIndexProbe and SelectedIndexProbe read `activeIndex` and `selectedIndex`, as
// these titles read the store's ActiveIndex and SelectedIndex. `data-highlighted` and `aria-activedescendant` are written by
// the `composite` module: a highlight C# decides is the request it sends (LastHighlightRequest), and the highlight the module
// gives an opening list is the selected index C# sends it (ListSelectedIndex, CMB-KEY-01). Without Items the items' order is
// the list module's report (ReportOrderAsync), sent after every change of the rendered items.
/// <summary>Combobox's root at L2: single selection.</summary>
public sealed class ComboboxRootSelectionSingleTests : IDisposable
{
    private static readonly string[] s_appleBanana = ["apple", "banana"];

    private static readonly string[] s_fruits = ["apple", "banana", "cherry"];

    private static readonly string[] s_capitalFruits = ["Apple", "Banana", "Cherry"];

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "fires onOpenChange once with reason item-press on mouse click")]
    [Trait("Requirement", "CMB-EVT-10")]
    public async Task Fires_onOpenChange_once_with_reason_item_press_on_mouse_click()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = Render(new() { ["Items"] = (IReadOnlyList<string>)s_appleBanana });
        await cut.PressInputAsync(test);
        Assert.True(cut.HasListbox());

        cut.Instance.OpenChanges.Clear();
        await cut.ClickOptionAsync("apple");
        await cut.CompleteCloseAsync();

        Assert.False(cut.HasListbox());
        (bool open, EventReason reason, EventSnapshot? @event) = Assert.Single(cut.Instance.OpenChanges);
        Assert.False(open);
        Assert.Equal(EventReason.ItemPress, reason);
        Assert.True(@event!.Kind is EventKind.MouseEvent or EventKind.PointerEvent);
    }

    [Fact(DisplayName = "should auto-close popup after selection when open state is uncontrolled")]
    [Trait("Requirement", "CMB-PTR-05")]
    public async Task Should_auto_close_popup_after_selection_when_open_state_is_uncontrolled()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = Render(new() { ["Items"] = (IReadOnlyList<string>)s_fruits }, p => p.Add(x => x.WithTrigger, true));
        IElement input = cut.Find("[data-testid=input]");

        await cut.PressTriggerAsync(test);

        Assert.True(cut.HasListbox());
        Assert.Equal("true", input.GetAttribute("aria-expanded"));

        await cut.ClickOptionAsync("apple");
        await cut.CompleteCloseAsync();

        Assert.False(cut.HasListbox());
        Assert.Equal("false", cut.Find("[data-testid=input]").GetAttribute("aria-expanded"));
    }

    // AsyncItemsCombobox: the consumer narrows its items to the selection once the popup finished closing.
    [Fact(DisplayName = "syncs selected index when items change after close")]
    [Trait("Requirement", "CMB-STA-25")]
    public async Task Syncs_selected_index_when_items_change_after_close()
    {
        Dictionary<string, object?> root = new() { ["Items"] = (IReadOnlyList<string>)s_capitalFruits };
        IRenderedComponent<ComboboxRootBatchAHost> cut = Render(root);
        string? selected = null;
        cut.Instance.OnValue = args => selected = args.Value;
        cut.Instance.OnComplete = opened =>
        {
            if (!opened && selected is not null)
            {
                root["Items"] = (IReadOnlyList<string>)[selected];
            }
        };

        await cut.PressInputAsync(test);
        Assert.True(cut.HasListbox());
        await cut.ClickOptionAsync("Cherry");
        await cut.CompleteCloseAsync();
        Assert.False(cut.HasListbox());

        await cut.PressInputAsync(test);

        Assert.True(cut.HasListbox());
        Assert.True(cut.Option("Cherry").HasAttribute("data-selected"));
    }

    [Fact(DisplayName = "should not auto-close popup when open state is controlled")]
    [Trait("Requirement", "CMB-API-01")]
    [Trait("Requirement", "CMB-API-05")]
    public async Task Should_not_auto_close_popup_when_open_state_is_controlled()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = Render(new() { ["Items"] = (IReadOnlyList<string>)s_fruits, ["Open"] = true }, p => p.Add(x => x.WithTrigger, true));

        await cut.ClickOptionAsync("apple");
        await cut.CompleteCloseAsync();

        Assert.True(cut.HasListbox());
    }

    [Fact(DisplayName = "should show all items when query is empty with enhanced filter")]
    [Trait("Requirement", "CMB-API-14")]
    public void Should_show_all_items_when_query_is_empty_with_enhanced_filter()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = Render(new() { ["Items"] = (IReadOnlyList<string>)s_fruits, ["DefaultOpen"] = true });

        Assert.Equal(["apple", "banana", "cherry"], cut.OptionTexts());
    }

    [Fact(DisplayName = "should not auto-close during browser autofill")]
    [Trait("Requirement", "CMB-EVT-11")]
    public async Task Should_not_auto_close_during_browser_autofill()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = Render(new() { ["Items"] = (IReadOnlyList<string>)s_fruits, ["Name"] = "test", ["DefaultOpen"] = true });
        Assert.True(cut.HasListbox());

        await cut.AutofillAsync(test, "apple");

        Assert.True(cut.HasListbox());
    }

    [Fact(DisplayName = "does not navigate kept-mounted items while controlled closed")]
    [Trait("Requirement", "CMB-API-48")]
    public async Task Does_not_navigate_kept_mounted_items_while_controlled_closed()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = Render(
            new() { ["Items"] = (IReadOnlyList<string>)s_appleBanana, ["Open"] = false, ["AutoHighlight"] = true },
            p => p.Add(x => x.KeepMounted, true));

        await cut.TypeAsync(test, "banana");

        Assert.Null(cut.Store().State.ActiveIndex);
        Assert.True(test.LastHighlightRequest() is null or -1);
    }

    [Fact(DisplayName = "restores the selected item instead of the first item when clearing with autoHighlight")]
    [Trait("Requirement", "CMB-STA-26")]
    public async Task Restores_the_selected_item_instead_of_the_first_item_when_clearing_with_autoHighlight()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = Render(
            new() { ["Items"] = (IReadOnlyList<string>)s_fruits, ["DefaultValue"] = "banana", ["AutoHighlight"] = true },
            PopupInput);
        await cut.PressTriggerAsync(test);

        // With autoHighlight, filtering strongly highlights the first match.
        await cut.TypeAsync(test, "a");
        Assert.Equal(0, cut.Store().State.ActiveIndex);
        Assert.Equal(0, test.LastHighlightRequest());

        // Clearing returns the highlight to the selected item, not the first item.
        await cut.ClearAsync(test);

        Assert.Equal("banana", cut.OptionTexts()[1]);
        Assert.Equal(1, cut.Store().State.ActiveIndex);
        Assert.Equal(1, test.LastHighlightRequest());
    }

    [Fact(DisplayName = "emits onItemHighlighted for the selected item when the query is cleared")]
    [Trait("Requirement", "CMB-STA-24")]
    public async Task Emits_onItemHighlighted_for_the_selected_item_when_the_query_is_cleared()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = Render(new() { ["Items"] = (IReadOnlyList<string>)s_fruits, ["DefaultValue"] = "banana" }, PopupInput);
        await cut.PressTriggerAsync(test);
        await cut.TypeAsync(test, "cherry");
        cut.Instance.Highlights.Clear();

        await cut.ClearAsync(test);

        // A programmatic (`none`) highlight of the selected item, which virtualized lists rely on to scroll it into view.
        Assert.Contains(("banana", EventReason.None, 1), cut.Instance.Highlights);
    }

    [Fact(DisplayName = "consumes a query-clear restore when the selected item is absent")]
    [Trait("Requirement", "CMB-STA-26")]
    public async Task Consumes_a_query_clear_restore_when_the_selected_item_is_absent()
    {
        bool includeSelected = true;
        IRenderedComponent<ComboboxRootBatchAHost> cut = Render(
            new() { ["DefaultValue"] = "banana", ["DefaultOpen"] = true, ["AutoHighlight"] = true },
            p => p
                .Add(x => x.ControlInput, true)
                .Add(x => x.InputInsidePopup, true)
                .Add(x => x.VisibleItems, input => input.Length != 0 ? ["apple"] : includeSelected ? s_fruits : ["apple", "cherry"]));
        await cut.ReportOrderAsync(test);

        await cut.TypeAsync(test, "a");
        await cut.ReportOrderAsync(test);
        Assert.Equal(0, cut.Store().State.ActiveIndex);

        includeSelected = false;
        await cut.Instance.RerenderAsync();
        cut.Instance.Highlights.Clear();
        await cut.ClearAsync(test);
        await cut.ReportOrderAsync(test);

        Assert.Equal(2, cut.OptionTexts().Count);
        Assert.Null(cut.Store().State.ActiveIndex);
        Assert.DoesNotContain(cut.Instance.Highlights, highlight => highlight.Value == "banana");
    }

    [Fact(DisplayName = "clears selectedIndex when the value is cleared externally while closed (no items prop)")]
    [Trait("Requirement", "CMB-STA-25")]
    public async Task Clears_selectedIndex_when_the_value_is_cleared_externally_while_closed()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = RenderControlled("banana");
        await OpenStaticAsync(cut);
        Assert.Equal(1, cut.Store().State.SelectedIndex);

        await cut.EscapeAsync(test);
        await cut.CompleteCloseAsync();
        Assert.False(cut.HasListbox());

        await cut.Instance.SetValueAsync(null);
        Assert.Null(cut.Store().State.SelectedIndex);

        await OpenStaticAsync(cut);

        Assert.Null(cut.Store().State.ActiveIndex);
        Assert.Null(cut.ListSelectedIndex());
    }

    [Fact(DisplayName = "clears selectedIndex when the value is set to an unmatched value while closed (no items prop)")]
    [Trait("Requirement", "CMB-STA-25")]
    [Trait("Requirement", "CMB-EDGE-11")]
    public async Task Clears_selectedIndex_when_the_value_is_set_to_an_unmatched_value_while_closed()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = RenderControlled("banana");
        await OpenStaticAsync(cut);
        Assert.Equal(1, cut.Store().State.SelectedIndex);

        await cut.EscapeAsync(test);
        await cut.CompleteCloseAsync();
        Assert.False(cut.HasListbox());

        await cut.Instance.SetValueAsync("dragonfruit");
        Assert.Null(cut.Store().State.SelectedIndex);

        await OpenStaticAsync(cut);

        Assert.Null(cut.Store().State.ActiveIndex);
        Assert.Null(cut.ListSelectedIndex());
    }

    // The portal mounts its children while closed; the index is the mounted item's.
    [Fact(DisplayName = "registers selectedIndex before opening with a keepMounted portal (no items prop)")]
    [Trait("Requirement", "CMB-API-48")]
    [Trait("Requirement", "CMB-STA-25")]
    public async Task Registers_selectedIndex_before_opening_with_a_keepMounted_portal()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = Render(
            new() { ["DefaultValue"] = "banana" },
            p => p.Add(x => x.StaticItems, (IReadOnlyList<string>)s_fruits).Add(x => x.KeepMounted, true));

        await cut.ReportOrderAsync(test);

        Assert.Equal(1, cut.Store().State.SelectedIndex);
    }

    // The external button is pressed with fireEvent, so no outside press closes the popup.
    [Fact(DisplayName = "does not highlight the previous item after the value changes while open (no items prop)")]
    [Trait("Requirement", "CMB-STA-25")]
    public async Task Does_not_highlight_the_previous_item_after_the_value_changes_while_open()
    {
        IRenderedComponent<ComboboxRootBatchAHost> cut = RenderControlled("banana");
        await OpenStaticAsync(cut);
        Assert.Equal(1, cut.Store().State.SelectedIndex);

        await cut.Instance.SetValueAsync("cherry");
        Assert.True(cut.HasListbox());

        await cut.EscapeAsync(test);
        await cut.CompleteCloseAsync();
        Assert.False(cut.HasListbox());

        // Closing reconciles the index to the new selection (cherry).
        Assert.Equal(2, cut.Store().State.SelectedIndex);

        cut.Instance.Highlights.Clear();
        await OpenStaticAsync(cut);

        Assert.Equal(2, cut.ListSelectedIndex());
        Assert.NotEqual(1, cut.Store().State.ActiveIndex);
        Assert.DoesNotContain(cut.Instance.Highlights, highlight => highlight.Value == "banana");
    }

    // The consumer recreates the controlled value object (same id) at every render; isItemEqualToValue compares the ids.
    [Fact(DisplayName = "re-highlights the selection on reopen when the value object is recreated with equal contents (no items prop)")]
    [Trait("Requirement", "CMB-STA-25")]
    [Trait("Requirement", "CMB-EDGE-11")]
    public async Task Re_highlights_the_selection_on_reopen_when_the_value_object_is_recreated_with_equal_contents()
    {
        Fruit[] fruits = [new(1, "apple"), new(2, "banana"), new(3, "cherry")];
        List<Fruit?> highlighted = [];
        void Parameters(ComponentParameterCollectionBuilder<ComboboxRoot<Fruit>> p) => p
            .Add(x => x.Value, new Fruit(2, "banana"))
            .Add(x => x.IsItemEqualToValue, (a, b) => a.Id == b.Id)
            .Add(x => x.ItemToStringLabel, fruit => fruit.Label)
            .Add(x => x.OnItemHighlighted, EventCallback.Factory.Create<ComboboxItemHighlightedEventArgs<Fruit>>(this, args => highlighted.Add(args.Value)))
            .Add(x => x.ChildContent, (RenderFragment)(builder => FruitParts(builder, fruits)));
        IRenderedComponent<ComboboxRoot<Fruit>> cut = test.Context.Render<ComboboxRoot<Fruit>>(Parameters);

        await PressInputAsync(cut);
        await ReportOrderAsync(cut);
        Assert.Equal(1, cut.Instance.Store.ListNavigation.List.SelectedIndex);

        await cut.InvokeAsync(() => test.SessionTwin.OnRequest($$"""{"open":false,"reason":"escape-key","event":{{ComboboxTestContext.Key("Escape")}},"triggerId":null}"""));
        await cut.InvokeAsync(() => cut.Instance.Store.CompleteTransitionAsync(false));
        Assert.Empty(cut.FindAll("[role=listbox]"));

        // A render while closed recreates the value object with the same id.
        cut.Render(Parameters);

        highlighted.Clear();
        await PressInputAsync(cut);
        await ReportOrderAsync(cut);

        Assert.Equal(1, cut.Instance.Store.ListNavigation.List.SelectedIndex);
        Assert.DoesNotContain(highlighted, fruit => fruit is not null && fruit.Id != 2);
    }

    private static void PopupInput(ComponentParameterCollectionBuilder<ComboboxRootBatchAHost> p) => p
        .Add(x => x.WithTrigger, true)
        .Add(x => x.TriggerValue, true)
        .Add(x => x.InputInsidePopup, true);

    private static void FruitParts(RenderTreeBuilder builder, Fruit[] fruits)
    {
        builder.OpenComponent<ComboboxInput>(0);
        builder.AddComponentParameter(1, "data-testid", "input");
        builder.CloseComponent();
        builder.OpenComponent<ComboboxPortal>(2);
        builder.AddComponentParameter(3, nameof(ComboboxPortal.ChildContent), (RenderFragment)(portal =>
        {
            portal.OpenComponent<ComboboxPositioner>(0);
            portal.AddComponentParameter(1, nameof(ComboboxPositioner.ChildContent), (RenderFragment)(positioner =>
            {
                positioner.OpenComponent<ComboboxPopup>(0);
                positioner.AddComponentParameter(1, nameof(ComboboxPopup.ChildContent), (RenderFragment)(popup =>
                {
                    popup.OpenComponent<ComboboxList<Fruit>>(0);
                    popup.AddComponentParameter(1, nameof(ComboboxList<Fruit>.ChildContent), (RenderFragment)(list =>
                    {
                        foreach (Fruit fruit in fruits)
                        {
                            list.OpenComponent<ComboboxItem<Fruit>>(0);
                            list.SetKey(fruit.Id);
                            list.AddComponentParameter(1, nameof(ComboboxItem<Fruit>.Value), fruit);
                            list.AddComponentParameter(2, nameof(ComboboxItem<Fruit>.ChildContent), (RenderFragment)(text => text.AddContent(0, fruit.Label)));
                            list.CloseComponent();
                        }
                    }));
                    popup.CloseComponent();
                }));
                positioner.CloseComponent();
            }));
            portal.CloseComponent();
        }));
        builder.CloseComponent();
    }

    private Task PressInputAsync(IRenderedComponent<ComboboxRoot<Fruit>> cut)
    {
        string id = cut.Find("[data-testid=input]").Id!;
        JSRuntimeInvocation attach = test.Interactions.Invocations["attach"].Last(invocation => invocation.Arguments[1] is string json && json.Contains("\"triggerId\":\"" + id + "\"", StringComparison.Ordinal));
        InteractionsTwin twin = ((DotNetObjectReference<InteractionsTwin>)attach.Arguments[2]!).Value;
        return cut.InvokeAsync(() => twin.OnRequest($$"""{"open":true,"reason":"input-press","event":{{ComboboxTestContext.Click}},"triggerId":"{{id}}"}"""));
    }

    private Task ReportOrderAsync(IRenderedComponent<ComboboxRoot<Fruit>> cut)
    {
        string ids = string.Join(",", cut.FindAll("[role=option]").Select(option => System.Text.Json.JsonSerializer.Serialize(option.Id)));
        JSRuntimeInvocation attach = test.Composite.Invocations["attach"].Last(invocation => invocation.Arguments[1] is string json && json.Contains("\"reportItems\":true", StringComparison.Ordinal));
        CompositeTwin twin = ((DotNetObjectReference<CompositeTwin>)attach.Arguments[2]!).Value;
        return cut.InvokeAsync(() => twin.OnItemsChange("{\"ids\":[" + ids + "]}"));
    }

    // user.click(input), then the opened list's registration.
    private async Task OpenStaticAsync(IRenderedComponent<ComboboxRootBatchAHost> cut)
    {
        await cut.PressInputAsync(test);
        await cut.ReportOrderAsync(test);
        Assert.True(cut.HasListbox());
    }

    // The App of ComboboxRoot.test.tsx:2659-2684: a controlled value over static apple, banana and cherry.
    private IRenderedComponent<ComboboxRootBatchAHost> RenderControlled(string value) =>
        test.Context.Render<ComboboxRootBatchAHost>(p => p
            .Add(x => x.ControlValue, true)
            .Add(x => x.InitialValue, value)
            .Add(x => x.StaticItems, (IReadOnlyList<string>)s_fruits));

    private IRenderedComponent<ComboboxRootBatchAHost> Render(Dictionary<string, object?> root, Action<ComponentParameterCollectionBuilder<ComboboxRootBatchAHost>>? host = null) =>
        test.Context.Render<ComboboxRootBatchAHost>(p =>
        {
            p.Add(x => x.RootParameters, root);
            host?.Invoke(p);
        });

    private sealed class Fruit(int id, string label)
    {
        public int Id => id;

        public string Label => label;
    }
}

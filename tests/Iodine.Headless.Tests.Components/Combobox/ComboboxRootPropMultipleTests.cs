using Bunit;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// ComboboxRoot.test.tsx's `prop: multiple` titles cited at L2 (the `skipIf(isJSDOM)` close-animation titles are L4). Typing
// is the `combobox` module's typed change (one report for user.type's whole text), a press of the input or the trigger is
// the `interactions` module's request, Escape is the session's dismissal, and the arrow keys and Enter are the list
// navigation's highlight report and the input handle's Enter (ComboboxRootBatchBSteps). The input's text is the store's.
// The popup's exit is the `transitions` module's, which reports its end at once without an animation (FinishExitAsync).
/// <summary>Combobox's root at L2: ComboboxRoot.test.tsx's <c>prop: multiple</c> block.</summary>
public sealed class ComboboxRootPropMultipleTests : IDisposable
{
    private static readonly string[] s_items = ["apple", "apricot", "banana"];

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "\"multiple\" clears uncontrolled input after select when filtering")]
    [Trait("Requirement", "CMB-PTR-05")]
    public async Task Multiple_clears_uncontrolled_input_after_select_when_filtering()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = Render(new() { ["Multiple"] = true }, ["apple", "banana"]);

        await cut.TypeAsync(test, "app");
        await cut.ClickOptionAsync("apple");

        // After selecting while filtering, uncontrolled input clears.
        Assert.Equal(string.Empty, cut.InputText());
    }

    // The pointer's highlight is the list navigation's report of a mousemove; aria-activedescendant follows the store's
    // highlight, which C# never asks the module to clear here (no new highlight request after the click).
    [Fact(DisplayName = "keeps the active item when the post-selection input clear is canceled")]
    [Trait("Requirement", "CMB-API-03")]
    [Trait("Requirement", "CMB-EVT-07")]
    public async Task Keeps_the_active_item_when_the_post_selection_input_clear_is_canceled()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = Render(
            new() { ["Multiple"] = true, ["DefaultOpen"] = true, ["Items"] = (IReadOnlyList<string>)s_items },
            inputInside: true);
        cut.Instance.OnInput = args =>
        {
            if (args.Details.Reason == EventReason.InputClear)
            {
                args.Details.Cancel();
            }
        };

        await cut.TypeAsync(test, "app");
        await cut.HighlightAsync(test, 0, "apple", ComboboxTestContext.Event(ComboboxTestContext.Click, "mousemove", 2));
        Assert.Equal(0, cut.Instance.Root!.Store.State.ActiveIndex);
        List<string> requests = test.HighlightRequests();

        await cut.ClickOptionAsync("apple");

        Assert.Equal(EventReason.InputClear, cut.Instance.InputChanges[^1].Reason);
        Assert.Equal("app", cut.InputText());
        Assert.Equal(0, cut.Instance.Root.Store.State.ActiveIndex);
        Assert.Equal(requests, test.HighlightRequests());
    }

    [Fact(DisplayName = "keeps the filter text when the \"item-press\" close is canceled (input outside popup)")]
    [Trait("Requirement", "CMB-PTR-05")]
    [Trait("Requirement", "CMB-EVT-07")]
    public async Task Keeps_the_filter_text_when_the_item_press_close_is_canceled()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = Render(new() { ["Multiple"] = true }, s_items);
        cut.Instance.OnOpen = args =>
        {
            if (!args.Value && args.Details.Reason == EventReason.ItemPress)
            {
                args.Details.Cancel();
            }
        };

        await cut.TypeAsync(test, "app");
        await cut.ClickOptionAsync("apple");

        Assert.Equal("app", cut.InputText());
        Assert.True(cut.HasList());
        Assert.Equal("true", cut.Option("apple").GetAttribute("aria-selected"));

        await cut.EscapeAsync(test);
        await cut.FinishExitAsync();

        Assert.False(cut.HasList());
        Assert.Equal(string.Empty, cut.InputText());
    }

    [Fact(DisplayName = "marks the close-path clear as an item press only when an item press closed the popup (input outside popup)")]
    [Trait("Requirement", "CMB-PTR-05")]
    [Trait("Requirement", "CMB-EVT-04")]
    [Trait("Requirement", "CMB-EVT-08")]
    public async Task Marks_the_close_path_clear_as_an_item_press_only_when_an_item_press_closed_the_popup()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = Render(new() { ["Multiple"] = true }, s_items);

        await cut.TypeAsync(test, "app");
        cut.Instance.InputChanges.Clear();

        await cut.ClickOptionAsync("apple");

        Assert.Equal(string.Empty, cut.InputText());
        Assert.True(cut.Instance.InputChanges.Find(change => change.Text.Length == 0).IsItemPress);

        await cut.TypeAsync(test, "app");
        cut.Instance.InputChanges.Clear();

        // Closing with the keyboard clears the same way, but no item was pressed.
        await cut.EscapeAsync(test);

        Assert.Equal(string.Empty, cut.InputText());
        Assert.False(cut.Instance.InputChanges.Find(change => change.Text.Length == 0).IsItemPress);
    }

    [Fact(DisplayName = "keeps the filter text when the selection clear is canceled (input inside popup)")]
    [Trait("Requirement", "CMB-PTR-05")]
    [Trait("Requirement", "CMB-EVT-07")]
    public async Task Keeps_the_filter_text_when_the_selection_clear_is_canceled_with_the_input_inside()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = Render(new() { ["Multiple"] = true, ["DefaultOpen"] = true }, s_items, inputInside: true);
        cut.Instance.OnInput = CancelItemPress;

        await cut.TypeAsync(test, "app");
        await cut.ClickOptionAsync("apple");

        Assert.Equal("app", cut.InputText());
        Assert.True(cut.HasList());
    }

    [Fact(DisplayName = "keeps the filter text in an inline list when the selection clear is canceled")]
    [Trait("Requirement", "CMB-PTR-05")]
    public async Task Keeps_the_filter_text_in_an_inline_list_when_the_selection_clear_is_canceled()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = test.Context.Render<ComboboxRootBatchBHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Multiple"] = true, ["Inline"] = true, ["Open"] = true })
            .Add(x => x.Inline, true)
            .Add(x => x.StaticItems, s_items));
        cut.Instance.OnInput = CancelItemPress;

        await cut.TypeAsync(test, "app");
        await cut.ClickOptionAsync("apple");

        Assert.Equal("app", cut.InputText());
        Assert.Equal("true", cut.Option("apple").GetAttribute("aria-selected"));
    }

    [Fact(DisplayName = "still clears the kept filter text once the popup closes")]
    [Trait("Requirement", "CMB-STA-30")]
    [Trait("Requirement", "CMB-EVT-07")]
    public async Task Still_clears_the_kept_filter_text_once_the_popup_closes()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = Render(new() { ["Multiple"] = true }, s_items, inputInside: true, trigger: true);
        cut.Instance.OnInput = CancelItemPress;

        await cut.PressTriggerAsync(test);
        await cut.TypeAsync(test, "app");
        await cut.ClickOptionAsync("apple");

        Assert.Equal("app", cut.InputText());

        // The cleanup clear that runs after the popup closes doesn't carry `isItemPress`, so the recipe doesn't cancel it
        // and the next open starts with a fresh filter.
        await cut.EscapeAsync(test);
        await cut.FinishExitAsync();
        Assert.False(cut.HasList());

        await cut.PressTriggerAsync(test);

        Assert.Equal(string.Empty, cut.InputText());
    }

    [Fact(DisplayName = "clears the input with the \"input-clear\" reason and the item click event on pointer selection")]
    [Trait("Requirement", "CMB-PTR-05")]
    [Trait("Requirement", "CMB-EVT-04")]
    [Trait("Requirement", "CMB-EVT-08")]
    public async Task Clears_the_input_with_the_input_clear_reason_and_the_item_click_event_on_pointer_selection()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = Render(new() { ["Multiple"] = true, ["DefaultOpen"] = true }, s_items, inputInside: true);

        await cut.TypeAsync(test, "app");
        cut.Instance.InputChanges.Clear();

        await cut.ClickOptionAsync("apple");

        Assert.Contains(cut.Instance.InputChanges, change => change.Text.Length == 0 && change.Reason == EventReason.InputClear);
        Assert.Equal("click", cut.Instance.InputChanges[^1].EventType);
        Assert.True(cut.Instance.InputChanges[^1].IsItemPress);
    }

    [Fact(DisplayName = "clears the input with the \"input-clear\" reason and the originating keydown event on keyboard selection")]
    [Trait("Requirement", "CMB-PTR-05")]
    [Trait("Requirement", "CMB-EVT-04")]
    public async Task Clears_the_input_with_the_input_clear_reason_and_the_originating_keydown_event_on_keyboard_selection()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = Render(new() { ["Multiple"] = true, ["DefaultOpen"] = true }, s_items, inputInside: true);

        await cut.TypeAsync(test, "app");
        await cut.HighlightAsync(test, 0, "apple");
        Assert.Equal(0, cut.Instance.Root!.Store.State.ActiveIndex);

        cut.Instance.InputChanges.Clear();
        await cut.EnterAsync(test, "apple");

        Assert.Contains(cut.Instance.InputChanges, change => change.Text.Length == 0 && change.Reason == EventReason.InputClear);

        // Keyboard activation synthesizes a click on the highlighted item, but the details carry the originating keydown so
        // consumers see the real user gesture.
        Assert.Equal("keydown", cut.Instance.InputChanges[^1].EventType);
        Assert.True(cut.Instance.InputChanges[^1].IsItemPress);
    }

    [Fact(DisplayName = "does not close popup when filtering with input inside popup in multiple mode")]
    [Trait("Requirement", "CMB-PTR-05")]
    public async Task Does_not_close_popup_when_filtering_with_input_inside_popup_in_multiple_mode()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = test.Context.Render<ComboboxRootBatchBHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Multiple"] = true, ["Items"] = (IReadOnlyList<string>)s_items })
            .Add(x => x.WithTrigger, true)
            .Add(x => x.TriggerContent, builder =>
            {
                builder.OpenComponent<ComboboxValue<string>>(0);
                builder.CloseComponent();
            })
            .Add(x => x.InputInsidePopup, true));

        await cut.PressTriggerAsync(test);
        await cut.TypeAsync(test, "app");
        await cut.ClickOptionAsync("apple");

        Assert.True(cut.Instance.Root!.Store.IsOpen);
        Assert.True(cut.HasList());
        Assert.Equal(string.Empty, cut.InputText());
    }

    [Fact(DisplayName = "\"multiple\" clears typed input on close when no selection made")]
    [Trait("Requirement", "CMB-STA-30")]
    public async Task Multiple_clears_typed_input_on_close_when_no_selection_made()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = Render(new() { ["Multiple"] = true, ["DefaultOpen"] = true }, ["apple", "banana"]);

        await cut.TypeAsync(test, "app");

        // Close without selecting.
        await cut.EscapeAsync(test);
        await cut.FinishExitAsync();

        Assert.False(cut.HasList());
        Assert.Equal(string.Empty, cut.InputText());
        Assert.Equal((string.Empty, EventReason.InputClear), (cut.Instance.InputChanges[^1].Text, cut.Instance.InputChanges[^1].Reason));
    }

    [Fact(DisplayName = "does not clear popup input after canceled multiple value changes")]
    [Trait("Requirement", "CMB-API-03")]
    [Trait("Requirement", "CMB-STA-30")]
    [Trait("Requirement", "CMB-EVT-07")]
    public async Task Does_not_clear_popup_input_after_canceled_multiple_value_changes()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = Render(new() { ["Multiple"] = true }, s_items, inputInside: true, trigger: true);
        cut.Instance.OnValues = args => args.Details.Cancel();

        await cut.PressTriggerAsync(test);
        Assert.True(cut.HasOption("apple"));
        await cut.TypeAsync(test, "app");
        cut.Instance.InputChanges.Clear();

        await cut.ClickOptionAsync("apple");

        Assert.NotEmpty(cut.Instance.ValuesChanges);
        Assert.Equal("app", cut.InputText());
        Assert.Empty(cut.Instance.InputChanges);
    }

    [Fact(DisplayName = "\"single\" clears typed input on close when no selection made (input outside popup)")]
    [Trait("Requirement", "CMB-STA-30")]
    public async Task Single_clears_typed_input_on_close_when_no_selection_made()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = Render(new() { ["DefaultOpen"] = true }, ["apple", "banana"]);

        await cut.TypeAsync(test, "zz");

        // Close without selecting.
        await cut.EscapeAsync(test);
        await cut.FinishExitAsync();

        Assert.False(cut.HasList());
        Assert.Equal(string.Empty, cut.InputText());
        Assert.Equal((string.Empty, EventReason.InputClear), (cut.Instance.InputChanges[^1].Text, cut.Instance.InputChanges[^1].Reason));
    }

    private static void CancelItemPress(ValueChangeEventArgs<string> args)
    {
        if ((args.Details as ComboboxChangeEventDetails)?.IsItemPress == true)
        {
            args.Details.Cancel();
        }
    }

    private IRenderedComponent<ComboboxRootBatchBHarness> Render(Dictionary<string, object?> parameters, IReadOnlyList<string>? staticItems = null, bool inputInside = false, bool trigger = false) =>
        test.Context.Render<ComboboxRootBatchBHarness>(p => p
            .Add(x => x.RootParameters, parameters)
            .Add(x => x.StaticItems, staticItems)
            .Add(x => x.InputInsidePopup, inputInside)
            .Add(x => x.WithTrigger, trigger));
}

using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// CMB §3.13 and §12 at L2 (ComboboxItem.test.tsx). Each part's own renders are counted by a RenderCounter in its content: it
// renders exactly when the part renders, where bUnit's RenderCount also counts the renders of the parts inside.
/// <summary>Combobox's Item part at L2.</summary>
public sealed class ComboboxItemTests : IDisposable
{
    private static readonly string[] s_items = ["apple", "apricot", "avocado"];

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    // An input change keeps the filtered membership: no Item, and neither the Root nor the Popup, renders again. An open
    // or close that runs the List's template again is outside this row. Base UI's title passes no onInputValueChange; the
    // harness records one by default, and an EventCallback renders its receiver, which renders the Root's content as a
    // parent's state change does in React, so this title passes an empty one.
    [Fact(DisplayName = "does not re-render still-mounted items when filtering keeps the same membership")]
    [Trait("Requirement", "CMB-EDGE-10")]
    [Trait("Requirement", "CMB-API-55")]
    public async Task Does_not_rerender_still_mounted_items_when_filtering_keeps_the_same_membership()
    {
        int root = 0, popup = 0;
        Dictionary<string, int> items = [];
        IRenderedComponent<ComboboxHarness> cut = test.Context.Render<ComboboxHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?>
            {
                ["DefaultOpen"] = true,
                ["Items"] = (IReadOnlyList<string>)s_items,
                ["OnInputValueChange"] = default(EventCallback<ValueChangeEventArgs<string>>),
            })
            .Add(x => x.Extra, Counter(() => root++))
            .Add(x => x.PopupExtra, Counter(() => popup++))
            .Add(x => x.ItemExtra, item => Counter(() => items[item] = items.GetValueOrDefault(item) + 1)));
        Assert.Equal(3, cut.FindAll("[role=option]").Count);
        (int rootBefore, int popupBefore) = (root, popup);
        Dictionary<string, int> itemsBefore = new(items);

        // "a" matches all three items, so the filtered set is unchanged and every item stays mounted.
        await test.TypeAsync(cut, "a");

        Assert.Equal("a", cut.Instance.Root!.Store.State.InputValue);
        Assert.Equal(3, cut.FindAll("[role=option]").Count);
        Assert.Equal(itemsBefore, items);
        Assert.Equal(0, root - rootBefore);
        Assert.Equal(0, popup - popupBefore);
    }

    // CMB-STA-25 (ComboboxItem.tsx:98-109): without Items each rendered item writes its value at its index on every
    // change, so a positional template that hands an Item another value keeps the registry the highlight reads current.
    [Fact(DisplayName = "port: an item whose value changes under the same id updates the rendered registry")]
    [Trait("Requirement", "CMB-STA-25")]
    public async Task An_item_whose_value_changes_under_the_same_id_updates_the_rendered_registry()
    {
        IRenderedComponent<ComboboxPositionalHarness> cut = test.Context.Render<ComboboxPositionalHarness>(p => p.Add(x => x.Values, ["apple", "banana"]));
        string first = cut.Find("[data-testid=item-0]").Id!;
        string second = cut.Find("[data-testid=item-1]").Id!;
        await cut.InvokeAsync(() => TwinWith("\"reportItems\":true").OnItemsChange("{\"ids\":[\"" + first + "\",\"" + second + "\"]}"));
        await Highlight(cut, 1, second);
        Assert.Equal(["banana"], cut.Instance.Highlighted);

        cut.Render(p => p.Add(x => x.Values, ["apple", "cherry"]));
        await Highlight(cut, 0, first);
        await Highlight(cut, 1, second);

        Assert.Equal(second, cut.Find("[data-testid=item-1]").Id);
        Assert.Equal(["banana", "apple", "cherry"], cut.Instance.Highlighted);
    }

    // ADR 08 Amendments 2026-11-03 (2): an omitted Value is told by whether it was supplied, and stands for Base UI's
    // `null` (ComboboxItem.tsx:44), which compareItemEquality matches only to a null selection (itemEquality.ts:17-19).
    [Fact(DisplayName = "port: an item without a Value is the null item, not default(TValue), under a value-type root")]
    [Trait("Requirement", "CMB-API-56")]
    public async Task An_item_without_a_Value_is_the_null_item_under_a_value_type_root()
    {
        IRenderedComponent<ComboboxIntHarness> empty = test.Context.Render<ComboboxIntHarness>(p => p.Add(x => x.RootParameters, new Dictionary<string, object?> { ["DefaultOpen"] = true }));
        Assert.Equal("true", empty.Find("[data-testid=item-none]").GetAttribute("aria-selected"));

        IRenderedComponent<ComboboxIntHarness> zero = test.Context.Render<ComboboxIntHarness>(p => p.Add(x => x.RootParameters, new Dictionary<string, object?> { ["DefaultOpen"] = true, ["DefaultValue"] = 0 }));
        Assert.Equal("false", zero.Find("[data-testid=item-none]").GetAttribute("aria-selected"));

        IRenderedComponent<ComboboxIntHarness> one = test.Context.Render<ComboboxIntHarness>(p => p.Add(x => x.RootParameters, new Dictionary<string, object?> { ["DefaultOpen"] = true, ["DefaultValue"] = 1 }));
        await one.InvokeAsync(() => one.Find("[data-testid=item-none]").Click());
        Assert.False(one.Instance.Root!.Store.State.HasSelectedValue);
    }

    [Fact(DisplayName = "port: an item without a Value under a multiple root of a non-nullable value type throws")]
    [Trait("Requirement", "CMB-API-56")]
    public void An_item_without_a_Value_under_a_multiple_non_nullable_root_throws()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => test.Context.Render<ComboboxIntHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["DefaultOpen"] = true, ["Multiple"] = true })));

        Assert.StartsWith("Blazix: A `ComboboxItem` without a `Value` stands for `null`", error.Message, StringComparison.Ordinal);
    }

    // CMB-PTR-05 (AriaCombobox.tsx:885-892): a press inside an `<a href>` is the link's. The `combobox` module reports it
    // from the list's click listener, which runs before Blazor's, and reports null once the click is dispatched.
    [Fact(DisplayName = "clicking a link inside an item does not select or close (anchor with href)")]
    [Trait("Requirement", "CMB-PTR-05")]
    public async Task Clicking_a_link_inside_an_item_does_not_select_or_close()
    {
        IRenderedComponent<ComboboxLinkHarness> cut = test.Context.Render<ComboboxLinkHarness>(p => p.Add(x => x.Href, "/somewhere"));

        await ClickLinkAsync(cut, hash: false);

        Assert.False(cut.Instance.Root!.Store.State.HasSelectedValue);
        Assert.True(cut.Instance.Root.Store.IsOpen);
    }

    [Fact(DisplayName = "clicking a hash link inside an item does not select and closes popup (anchor with #hash)")]
    [Trait("Requirement", "CMB-PTR-05")]
    public async Task Clicking_a_hash_link_inside_an_item_does_not_select_and_closes_the_popup()
    {
        IRenderedComponent<ComboboxLinkHarness> cut = test.Context.Render<ComboboxLinkHarness>(p => p.Add(x => x.Href, "#section"));

        await ClickLinkAsync(cut, hash: true);

        Assert.False(cut.Instance.Root!.Store.State.HasSelectedValue);
        Assert.False(cut.Instance.Root.Store.IsOpen);
    }

    [Fact(DisplayName = "clicking an anchor without href behaves like normal item (selects and closes)")]
    [Trait("Requirement", "CMB-PTR-05")]
    public async Task Clicking_an_anchor_without_href_behaves_like_a_normal_item()
    {
        IRenderedComponent<ComboboxLinkHarness> cut = test.Context.Render<ComboboxLinkHarness>(p => p.Add(x => x.Href, null));
        Assert.False(cut.Find("[data-testid=link-one]").HasAttribute("href"));

        await cut.InvokeAsync(() => cut.Find("[data-testid=link-one]").Click());

        Assert.Equal("one", cut.Instance.Root!.Store.SelectedValue);
        Assert.False(cut.Instance.Root.Store.IsOpen);
    }

    // Final review N1: on Server the end reports (OnItemLinkPress {href:null}, OnSelectionEvent {event:null}) can be processed
    // while an async consumer onclick yields; the item takes both before the consumer's handler runs.
    [Fact(DisplayName = "port: an item's yielding onclick keeps the link press and Enter's attribution its end reports clear meanwhile")]
    [Trait("Requirement", "CMB-PTR-05")]
    [Trait("Requirement", "CMB-KEY-04")]
    public async Task A_yielding_onclick_keeps_the_link_press_and_the_attribution()
    {
        TaskCompletionSource yielded = new();
        IRenderedComponent<ComboboxLinkHarness> link = test.Context.Render<ComboboxLinkHarness>(p => p
            .Add(x => x.Href, "/somewhere")
            .Add(x => x.ItemClick, _ => yielded.Task));
        ComboboxTwin list = test.TwinOf(ComboboxTwin.ListPart);
        await link.InvokeAsync(() => list.OnItemLinkPress("""{"href":"/somewhere"}"""));
        Task click = link.InvokeAsync(() => link.Find("[data-testid=link-one]").ClickAsync(new MouseEventArgs()));
        await link.InvokeAsync(() => list.OnItemLinkPress("""{"href":null}"""));
        yielded.SetResult();
        await click;
        Assert.Empty(link.Instance.ValueChanges);

        TaskCompletionSource second = new();
        IRenderedComponent<ComboboxLinkHarness> plain = test.Context.Render<ComboboxLinkHarness>(p => p
            .Add(x => x.Href, null)
            .Add(x => x.ItemClick, _ => second.Task));
        ComboboxTwin input = test.TwinOf(ComboboxTwin.InputPart);
        await plain.InvokeAsync(() => input.OnSelectionEvent($$"""{"event":{{ComboboxTestContext.Key("Enter")}}}"""));
        Task enter = plain.InvokeAsync(() => plain.Find("[data-testid=link-one]").ClickAsync(new MouseEventArgs()));
        await plain.InvokeAsync(() => input.OnSelectionEvent("""{"event":null}"""));
        second.SetResult();
        await enter;

        ValueChangeEventArgs<string> change = Assert.Single(plain.Instance.ValueChanges);
        Assert.Equal("keydown", change.Details.Event?.Type);
    }

    // ComboboxList.tsx:83-99: the list's module handle clicks the highlighted item for its own Enter, between two reports on
    // the list's twin, as the input's handle does for the input's Enter.
    [Fact(DisplayName = "port: the list's own Enter attributes the selection to its keydown")]
    [Trait("Requirement", "CMB-KEY-04")]
    public async Task The_lists_own_Enter_attributes_the_selection_to_its_keydown()
    {
        IRenderedComponent<ComboboxLinkHarness> cut = test.Context.Render<ComboboxLinkHarness>(p => p.Add(x => x.Href, null));
        ComboboxTwin list = test.TwinOf(ComboboxTwin.ListPart);

        await cut.InvokeAsync(() => list.OnSelectionEvent($$"""{"event":{{ComboboxTestContext.Key("Enter")}}}"""));
        await cut.InvokeAsync(() => cut.Find("[data-testid=link-one]").Click());
        await cut.InvokeAsync(() => list.OnSelectionEvent("""{"event":null}"""));

        ValueChangeEventArgs<string> change = Assert.Single(cut.Instance.ValueChanges);
        Assert.Equal("keydown", change.Details.Event?.Type);
    }

    private async Task ClickLinkAsync(IRenderedComponent<ComboboxLinkHarness> cut, bool hash)
    {
        ComboboxTwin list = test.TwinOf(ComboboxTwin.ListPart);
        string href = cut.Find("[data-testid=link-one]").GetAttribute("href")!;
        Assert.Equal(hash, href.StartsWith('#'));
        await cut.InvokeAsync(() => list.OnItemLinkPress($$"""{"href":"{{href}}"}"""));
        await cut.InvokeAsync(() => cut.Find("[data-testid=link-one]").Click());
        await cut.InvokeAsync(() => list.OnItemLinkPress("""{"href":null}"""));
    }

    [Fact(DisplayName = "selects item and closes in single mode")]
    [Trait("Requirement", "CMB-API-56")]
    public async Task Selects_item_and_closes_in_single_mode()
    {
        IRenderedComponent<ComboboxStaticHost> cut = Static(new() { ["DefaultOpen"] = true }, ["one", "two"]);

        await cut.InvokeAsync(() => cut.Find("[data-testid=item-two]").Click());

        Assert.Equal("two", cut.Instance.Root!.Store.State.InputValue);
        Assert.False(cut.Instance.Root.Store.IsOpen);
    }

    [Fact(DisplayName = "reflects selected value with aria-selected when reopening")]
    [Trait("Requirement", "CMB-API-56")]
    [Trait("Requirement", "CMB-ARIA-09")]
    public async Task Reflects_selected_value_with_aria_selected_when_reopening()
    {
        IRenderedComponent<ComboboxStaticHost> cut = Static(new() { ["DefaultValue"] = "two" }, ["one", "two"]);

        await cut.InvokeAsync(() => test.SessionTwin.OnRequest($$"""{"open":true,"reason":"trigger-press","event":{{ComboboxTestContext.Click}},"triggerId":null}"""));

        Assert.Equal("true", cut.Find("[data-testid=item-two]").GetAttribute("aria-selected"));
        Assert.Equal("false", cut.Find("[data-testid=item-one]").GetAttribute("aria-selected"));
    }

    [Fact(DisplayName = "does not select disabled item")]
    [Trait("Requirement", "CMB-API-58")]
    public async Task Does_not_select_disabled_item()
    {
        IRenderedComponent<ComboboxStaticHost> cut = Static(new() { ["DefaultOpen"] = true }, ["one", "two"], value => value == "two" ? new Dictionary<string, object> { ["Disabled"] = true } : null);

        await cut.InvokeAsync(() => cut.Find("[data-testid=item-two]").Click());

        Assert.Equal(string.Empty, cut.Instance.Root!.Store.State.InputValue);
        Assert.False(cut.Instance.Root.Store.State.HasSelectedValue);
    }

    [Fact(DisplayName = "inherits the disabled state from the root")]
    [Trait("Requirement", "CMB-API-58")]
    public async Task Inherits_the_disabled_state_from_the_root()
    {
        int clicks = 0;
        IRenderedComponent<ComboboxStaticHost> cut = Static(new() { ["DefaultOpen"] = true, ["Disabled"] = true }, ["one"], _ => OnClick(_ => clicks++));
        Assert.True(cut.Find("[data-testid=item-one]").HasAttribute("data-disabled"));

        await cut.InvokeAsync(() => cut.Find("[data-testid=item-one]").Click());

        Assert.Equal(0, clicks);
        Assert.Equal(string.Empty, cut.Instance.Root!.Store.State.InputValue);
    }

    [Fact(DisplayName = "calls onClick when clicked with a pointer")]
    [Trait("Requirement", "CMB-API-60")]
    [Trait("Requirement", "CMB-EVT-10")]
    [Trait("Requirement", "CMB-EVT-12")]
    public async Task Calls_onClick_when_clicked_with_a_pointer()
    {
        int clicks = 0;
        IRenderedComponent<ComboboxStaticHost> cut = Static(new() { ["DefaultOpen"] = true }, ["apple", "banana"], _ => OnClick(_ => clicks++));

        await cut.InvokeAsync(() => cut.Find("[data-testid=item-banana]").Click());

        Assert.Equal(1, clicks);
        Assert.Equal("banana", cut.Instance.Root!.Store.SelectedValue);
    }

    // Enter's click is the module's: it reports the keydown, clicks the highlighted item and reports the attribution's end.
    [Fact(DisplayName = "calls onClick when selected with Enter key (via root interaction)")]
    [Trait("Requirement", "CMB-API-60")]
    [Trait("Requirement", "CMB-EVT-12")]
    public async Task Calls_onClick_when_selected_with_Enter_key()
    {
        int clicks = 0;
        List<string?> sources = [];
        IRenderedComponent<ComboboxStaticHost> cut = Static(
            new()
            {
                ["DefaultOpen"] = true,
                ["OnValueChange"] = EventCallback.Factory.Create<ValueChangeEventArgs<string>>(this, args => sources.Add(args.Details.Event?.Type)),
            },
            ["one", "two"],
            _ => OnClick(_ => clicks++));
        ComboboxTwin input = test.TwinOf(ComboboxTwin.InputPart);

        await cut.InvokeAsync(() => input.OnSelectionEvent($$"""{"event":{{ComboboxTestContext.Key("Enter")}}}"""));
        await cut.InvokeAsync(() => cut.Find("[data-testid=item-one]").Click());
        await cut.InvokeAsync(() => input.OnSelectionEvent("""{"event":null}"""));

        Assert.Equal(1, clicks);
        Assert.Equal(["keydown"], sources);
    }

    [Fact(DisplayName = "does not select the item when onClick prevents Base UI handler")]
    [Trait("Requirement", "CMB-API-60")]
    [Trait("Requirement", "CMB-EVT-12")]
    public async Task Does_not_select_the_item_when_onClick_prevents_the_handler()
    {
        int clicks = 0;
        IRenderedComponent<ComboboxStaticHost> cut = Static(new() { ["DefaultOpen"] = true }, ["one", "two"], value => value == "one" ? OnClick(e =>
        {
            clicks++;
            e.PreventPartHandler();
        }) : null);

        await cut.InvokeAsync(() => cut.Find("[data-testid=item-one]").Click());

        Assert.Equal(1, clicks);
        Assert.Equal(string.Empty, cut.Instance.Root!.Store.State.InputValue);
        Assert.True(cut.Instance.Root.Store.IsOpen);
    }

    [Fact(DisplayName = "multiple mode toggles selection and stays open")]
    [Trait("Requirement", "CMB-STA-13")]
    [Trait("Requirement", "CMB-DOM-09")]
    [Trait("Requirement", "CMB-PTR-05")]
    public async Task Multiple_mode_toggles_selection_and_stays_open()
    {
        IRenderedComponent<ComboboxStaticHost> cut = Static(new() { ["Multiple"] = true, ["DefaultOpen"] = true }, ["a", "b"]);

        await cut.InvokeAsync(() => cut.Find("[data-testid=item-a]").Click());
        Assert.Equal("true", cut.Find("[data-testid=item-a]").GetAttribute("aria-selected"));
        Assert.True(cut.Instance.Root!.Store.IsOpen);
        await cut.InvokeAsync(() => cut.Find("[data-testid=item-a]").Click());

        Assert.NotEqual("true", cut.Find("[data-testid=item-a]").GetAttribute("aria-selected"));
    }

    private static Dictionary<string, object> OnClick(Action<MouseEventArgs> handler) =>
        new() { ["onclick"] = EventCallback.Factory.Create(new object(), handler) };

    private IRenderedComponent<ComboboxStaticHost> Static(Dictionary<string, object?> root, IReadOnlyList<string> values, Func<string, IReadOnlyDictionary<string, object>?>? itemAttributes = null) =>
        test.Context.Render<ComboboxStaticHost>(p => p.Add(x => x.RootParameters, root).Add(x => x.Values, values).Add(x => x.ItemAttributes, itemAttributes));

    private Task Highlight(IRenderedComponent<ComboboxPositionalHarness> cut, int index, string itemId)
    {
        string json = $$"""{"index":{{index}},"itemId":"{{itemId}}","event":{{ComboboxTestContext.Key("ArrowDown")}},"revision":1000}""";
        return cut.InvokeAsync(() => TwinWith("\"virtual\":true").OnListHighlight(json));
    }

    private CompositeTwin TwinWith(string marker)
    {
        JSRuntimeInvocation attach = test.Composite.Invocations["attach"].Last(invocation => invocation.Arguments[1] is string json && json.Contains(marker, StringComparison.Ordinal));
        return ((DotNetObjectReference<CompositeTwin>)attach.Arguments[2]!).Value;
    }

    private static RenderFragment Counter(Action rendered) => builder =>
    {
        builder.OpenComponent<RenderCounter>(0);
        builder.AddComponentParameter(1, nameof(RenderCounter.Rendered), rendered);
        builder.CloseComponent();
    };
}

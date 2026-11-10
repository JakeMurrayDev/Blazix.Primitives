using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
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

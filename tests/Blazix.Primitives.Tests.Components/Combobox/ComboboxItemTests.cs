using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

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

    private static RenderFragment Counter(Action rendered) => builder =>
    {
        builder.OpenComponent<RenderCounter>(0);
        builder.AddComponentParameter(1, nameof(RenderCounter.Rendered), rendered);
        builder.CloseComponent();
    };
}

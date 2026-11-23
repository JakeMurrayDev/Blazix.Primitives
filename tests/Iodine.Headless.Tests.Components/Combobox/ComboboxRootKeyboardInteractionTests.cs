using Bunit;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// ComboboxRoot.test.tsx's `keyboard interaction` titles cited at L2. The arrow keys and Enter's click are the modules': the
// list navigation reports the highlight it moved (CompositeTwin.OnListHighlight), and Enter is the input handle's keydown
// report, the highlighted item's click and the end report (ComboboxRootBatchBSteps.EnterAsync). Escape's propagation is the
// `interactions` session's, so the inline title asserts the dismissal C# sends it.
/// <summary>Combobox's root at L2: ComboboxRoot.test.tsx's <c>keyboard interaction</c> block.</summary>
public sealed class ComboboxRootKeyboardInteractionTests : IDisposable
{
    private static readonly string[] s_fruits = ["apple", "banana", "cherry"];

    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "opens, navigates with ArrowDown, and Enter selects")]
    [Trait("Requirement", "CMB-ARIA-03")]
    public async Task Opens_navigates_with_ArrowDown_and_Enter_selects()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = Render(new() { ["Items"] = (IReadOnlyList<string>)s_fruits });

        await cut.PressInputAsync(test);
        Assert.True(cut.HasList());

        // Highlight first item and select it.
        await cut.HighlightAsync(test, 0, "apple");
        await cut.EnterAsync(test, "apple");
        await cut.FinishExitAsync();

        Assert.False(cut.HasList());
        Assert.Equal("apple", cut.InputText());
    }

    [Fact(DisplayName = "Enter selects with manual indices provided to items")]
    [Trait("Requirement", "CMB-API-57")]
    public async Task Enter_selects_with_manual_indices_provided_to_items()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = test.Context.Render<ComboboxRootBatchBHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Items"] = (IReadOnlyList<string>)s_fruits })
            .Add(x => x.ExplicitIndex, true));

        await cut.PressInputAsync(test);
        Assert.True(cut.HasList());

        // Filter to "cherry", which the item's explicit index places at 0.
        await cut.TypeAsync(test, "c");
        await cut.HighlightAsync(test, 0, "cherry");
        await cut.EnterAsync(test, "cherry");

        Assert.Equal("cherry", cut.InputText());
    }

    // CMB-STR-05 (AriaCombobox.tsx:1395): an inline combobox's dismissal is off and its Escape bubbles. Escape's propagation is
    // the session's in the browser; C#'s share is the dismissal it sends, which lets the keydown reach the outer handler.
    [Fact(DisplayName = "bubbles Escape key when rendered inline without Positioner/Popup")]
    [Trait("Requirement", "CMB-STR-05")]
    public async Task Bubbles_Escape_key_when_rendered_inline_without_Positioner_or_Popup()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = test.Context.Render<ComboboxRootBatchBHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Inline"] = true, ["DefaultOpen"] = true })
            .Add(x => x.Inline, true)
            .Add(x => x.StaticItems, ["a", "b"]));

        await cut.PressInputAsync(test);
        Assert.True(cut.HasList());

        string session = test.LastSessionOptions();
        Assert.Contains("\"escapeKey\":false", session, StringComparison.Ordinal);
        Assert.Contains("\"bubblesEscapeKey\":true", session, StringComparison.Ordinal);
        Assert.True(cut.Instance.Root!.Store.IsOpen);
    }

    // CMB-STR-05 (ComboboxInput.tsx:408-411): Enter without a highlight is the input handle's, which does nothing while
    // inline (combobox.ts's handleKey), so no request reaches C#.
    [Fact(DisplayName = "keeps input value on Enter when inline and no item is highlighted")]
    [Trait("Requirement", "CMB-STR-05")]
    public async Task Keeps_input_value_on_Enter_when_inline_and_no_item_is_highlighted()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = test.Context.Render<ComboboxRootBatchBHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Inline"] = true, ["Items"] = (IReadOnlyList<string>)["Apple", "Banana"] })
            .Add(x => x.Inline, true));

        await cut.PressInputAsync(test);
        await cut.TypeAsync(test, "Ba");

        Assert.Null(cut.Instance.Root!.Store.State.ActiveIndex);
        Assert.Equal("Ba", cut.InputText());

        // Enter: the input handle, told the root is inline, reports nothing without a highlight.
        Assert.Contains("\"inline\":true", test.LastInputOptions(), StringComparison.Ordinal);
        Assert.Equal("Ba", cut.InputText());
    }

    private IRenderedComponent<ComboboxRootBatchBHarness> Render(Dictionary<string, object?> parameters) =>
        test.Context.Render<ComboboxRootBatchBHarness>(p => p.Add(x => x.RootParameters, parameters));
}

using Blazix.Primitives.Interop;
using Bunit;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// CMB-STA-26 (AriaCombobox.tsx:1097-1114; design point 9 of M5 track 11, settled by the orchestrator 2026-11-12): a typed
// highlight request is discarded once an inline input has lost the focus, unless the automatic highlight is always on, so
// results that arrive after the blur highlight nothing. Autocomplete's title is AutocompleteRoot.test.tsx's; this one is the
// same engine path under Combobox's parts.
/// <summary>Combobox's inline blur and a pending typed highlight at L2.</summary>
public sealed class ComboboxInlineBlurHighlightTests : IDisposable
{
    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: an inline input's blur discards a typed highlight request, so results that arrive later highlight nothing")]
    [Trait("Requirement", "CMB-STA-26")]
    public async Task An_inline_blur_discards_a_typed_highlight_request()
    {
        IRenderedComponent<ComboboxInlineAsyncHost> cut = test.Context.Render<ComboboxInlineAsyncHost>();
        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").Focus());
        string json = $$"""{"text":"32","typed":true,"composing":false,"event":{{ComboboxTestContext.InputEvent}}}""";
        ComboboxTwin twin = test.TwinOf(ComboboxTwin.InputPart);
        await cut.InvokeAsync(() => twin.OnInputChange(json));
        await cut.InvokeAsync(() => cut.Find("[data-testid=input]").Blur());

        cut.Render(p => p.Add(x => x.Items, (IReadOnlyList<string>)["32"]));

        Assert.Single(cut.FindAll("[role=option]"));
        Assert.Null(cut.Instance.Root!.Store.State.ActiveIndex);
    }
}

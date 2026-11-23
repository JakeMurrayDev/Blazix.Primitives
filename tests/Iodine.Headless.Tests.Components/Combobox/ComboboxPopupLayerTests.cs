using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// CMB §3.13 and §3.16 at L2. Base UI tests the Backdrop and the Arrow only through describeConformance (a batch of its own);
// these port titles cover what the parts render from the store: the backdrop's role, hidden state, open state and style, and
// the arrow's aria-hidden, open state and missing-positioner error.
/// <summary>Combobox's Backdrop and Arrow at L2.</summary>
public sealed class ComboboxPopupLayerTests : IDisposable
{
    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    // CMB-DOM-24 (ComboboxPopup.tsx:65, derived at every render): `{rootId}-popup` while the input is inside the popup. The
    // input inside registers that layout during the first render, after the popup's parameter set (review F20).
    [Fact(DisplayName = "port: the popup's id follows the input's layout without a parameter set of its own")]
    [Trait("Requirement", "CMB-DOM-24")]
    public void The_popups_id_follows_the_inputs_layout()
    {
        IRenderedComponent<ComboboxHarness> cut = test.Context.Render<ComboboxHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["DefaultOpen"] = true, ["Id"] = "fruit" })
            .Add(x => x.InputInsidePopup, true));

        Assert.Equal("fruit-popup", cut.Find("[data-testid=popup]").Id);
        Assert.Equal("fruit-popup", cut.Instance.Root!.Store.State.ElementIds.Popup);
    }

    [Fact(DisplayName = "port: the backdrop renders a presentation div, hidden while the popup is unmounted")]
    [Trait("Requirement", "CMB-STA-08")]
    [Trait("Requirement", "CMB-DOM-14")]
    public void The_backdrop_is_hidden_while_unmounted()
    {
        IElement backdrop = Render(new(), Backdrop(null)).Find("[data-testid=backdrop]");

        Assert.Equal("div", backdrop.TagName.ToLowerInvariant());
        Assert.Equal("presentation", backdrop.GetAttribute("role"));
        Assert.True(backdrop.HasAttribute("hidden"));
        Assert.True(backdrop.HasAttribute("data-closed"));
    }

    [Fact(DisplayName = "port: an open popup's backdrop shows with data-open, user-select none under the consumer's style")]
    [Trait("Requirement", "CMB-STA-08")]
    [Trait("Requirement", "CMB-DOM-14")]
    public void The_open_backdrop_shows()
    {
        IElement backdrop = Render(new() { ["DefaultOpen"] = true }, Backdrop("color: red")).Find("[data-testid=backdrop]");

        Assert.False(backdrop.HasAttribute("hidden"));
        Assert.True(backdrop.HasAttribute("data-open"));
        string style = backdrop.GetAttribute("style")!;
        Assert.Contains("user-select: none", style, StringComparison.Ordinal);
        Assert.Contains("color: red", style, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: the arrow renders an aria-hidden div with the popup's open state")]
    [Trait("Requirement", "CMB-STA-11")]
    [Trait("Requirement", "CMB-DOM-15")]
    public void The_arrow_renders_the_open_state()
    {
        IRenderedComponent<ComboboxHarness> cut = test.Context.Render<ComboboxHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["DefaultOpen"] = true })
            .Add(x => x.PopupExtra, (RenderFragment)Arrow));
        IElement arrow = cut.Find("[data-testid=arrow]");

        Assert.Equal("true", arrow.GetAttribute("aria-hidden"));
        Assert.True(arrow.HasAttribute("data-open"));
    }

    [Fact(DisplayName = "port: an arrow outside a positioner throws the missing positioner error")]
    [Trait("Requirement", "CMB-STR-02")]
    public void An_arrow_outside_a_positioner_throws()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Render(new(), Arrow));

        Assert.Equal("Blazix: `<ComboboxPopup>` and `<ComboboxArrow>` must be used within the `<ComboboxPositioner>` component.", error.Message);
    }

    private static void Arrow(RenderTreeBuilder builder)
    {
        builder.OpenComponent<ComboboxArrow>(0);
        builder.AddComponentParameter(1, "data-testid", "arrow");
        builder.CloseComponent();
    }

    private static RenderFragment Backdrop(string? style) => builder =>
    {
        builder.OpenComponent<ComboboxBackdrop>(0);
        builder.AddComponentParameter(1, "data-testid", "backdrop");
        if (style is not null)
        {
            builder.AddComponentParameter(2, "style", style);
        }

        builder.CloseComponent();
    };

    private IRenderedComponent<ComboboxHarness> Render(Dictionary<string, object?> root, RenderFragment extra) =>
        test.Context.Render<ComboboxHarness>(p => p.Add(x => x.RootParameters, root).Add(x => x.Extra, extra));
}

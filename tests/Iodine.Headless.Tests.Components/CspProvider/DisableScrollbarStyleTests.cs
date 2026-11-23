using Blazix.Primitives.Utilities;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The scrollbar style emitter at L2 (UTL-DOM-32, DOM-33): CSPProvider.test.tsx's titles on a test-only emitting part, as
/// <c>ScrollArea.Root</c> and <c>Select.Popup</c> render it (ScrollArea is incidental here, ADR 13 Amendments 2026-10-22).
/// Each emitter renders its own inline <c>style</c> element (design point 5 of M5 track 4, settled by the orchestrator
/// 2026-11-04), and the class stays applied when the element is disabled.
/// </summary>
public sealed class DisableScrollbarStyleTests : IDisposable
{
    private readonly BunitContext context = new();

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "applies nonce to inline style tags")]
    [Trait("Requirement", "UTL-DOM-32")]
    [Trait("Requirement", "UTL-DOM-35")]
    public void Applies_nonce_to_inline_style_tags()
    {
        IRenderedComponent<CspProvider> cut = context.Render<CspProvider>(p => p
            .Add(x => x.Nonce, "test-nonce")
            .AddChildContent<TestScrollbarStyleEmitter>());

        AngleSharp.Dom.IElement style = cut.Find("style");
        Assert.Equal(".base-ui-disable-scrollbar{scrollbar-width:none}.base-ui-disable-scrollbar::-webkit-scrollbar{display:none}", style.TextContent);
        Assert.Equal("test-nonce", style.GetAttribute("nonce"));
    }

    [Fact(DisplayName = "renders inline style tags by default")]
    [Trait("Requirement", "UTL-DOM-32")]
    public void Renders_inline_style_tags_by_default()
    {
        IRenderedComponent<TestScrollbarStyleEmitter> cut = context.Render<TestScrollbarStyleEmitter>();

        AngleSharp.Dom.IElement style = cut.Find("style");
        Assert.Equal(".base-ui-disable-scrollbar{scrollbar-width:none}.base-ui-disable-scrollbar::-webkit-scrollbar{display:none}", style.TextContent);
        Assert.False(style.HasAttribute("nonce"));
        Assert.Equal("base-ui-disable-scrollbar", cut.Find("[data-testid=viewport]").ClassName);
    }

    [Fact(DisplayName = "does not render inline style tags when disableStyleElements is true")]
    [Trait("Requirement", "UTL-DOM-33")]
    public void Does_not_render_inline_style_tags_when_disableStyleElements_is_true()
    {
        IRenderedComponent<CspProvider> cut = context.Render<CspProvider>(p => p
            .Add(x => x.DisableStyleElements, true)
            .AddChildContent<TestScrollbarStyleEmitter>());

        Assert.Equal("base-ui-disable-scrollbar", cut.Find("[data-testid=viewport]").ClassName);
        Assert.Empty(cut.FindAll("style"));
    }

    /// <summary>A test-only emitting part: the style before its element, which carries the class, as ScrollArea's viewport does.</summary>
    private sealed class TestScrollbarStyleEmitter : ComponentBase
    {
        [CascadingParameter]
        private CspProviderContext? Csp { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenRegion(0);
            DisableScrollbarStyle.Render(builder, Csp);
            builder.CloseRegion();
            builder.OpenElement(1, "div");
            builder.AddAttribute(2, "data-testid", "viewport");
            builder.AddAttribute(3, "class", DisableScrollbarStyle.ClassName);
            builder.CloseElement();
        }
    }
}

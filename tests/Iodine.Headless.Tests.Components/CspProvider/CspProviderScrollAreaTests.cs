using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace Iodine.Headless.Tests.Components;

/// <summary>
/// CSPProvider.test.tsx's three titles on the real <see cref="ScrollAreaRoot"/> and <see cref="ScrollAreaViewport"/>, as Base
/// UI runs them (UTL-DOM-32's ScrollArea emission clause and UTL-DOM-33's ScrollArea half; design point 11 (a) of the
/// ScrollArea track), beside the test-only emitter's titles in <see cref="DisableScrollbarStyleTests"/>.
/// </summary>
public sealed class CspProviderScrollAreaTests : IDisposable
{
    private const string Css = ".base-ui-disable-scrollbar{scrollbar-width:none}.base-ui-disable-scrollbar::-webkit-scrollbar{display:none}";

    private readonly BunitContext context = new();

    public CspProviderScrollAreaTests()
    {
        context.Services.AddIodineHeadless();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "port: applies nonce to inline style tags (ScrollArea)")]
    [Trait("Requirement", "UTL-DOM-32")]
    [Trait("Requirement", "UTL-DOM-35")]
    [Trait("Requirement", "SCR-DOM-05")]
    public void Applies_nonce_to_inline_style_tags()
    {
        IRenderedComponent<CspProvider> cut = context.Render<CspProvider>(p => p
            .Add(x => x.Nonce, "test-nonce")
            .AddChildContent<ScrollAreaRoot>(r => r.AddChildContent<ScrollAreaViewport>()));

        IElement style = cut.Find("style");
        Assert.Equal(Css, style.TextContent);
        Assert.Equal("test-nonce", style.GetAttribute("nonce"));
    }

    [Fact(DisplayName = "port: renders inline style tags by default (ScrollArea)")]
    [Trait("Requirement", "UTL-DOM-32")]
    [Trait("Requirement", "SCR-DOM-05")]
    [Trait("Requirement", "SCR-DOM-06")]
    public void Renders_inline_style_tags_by_default()
    {
        IRenderedComponent<ScrollAreaRoot> cut = context.Render<ScrollAreaRoot>(r => r.AddChildContent<ScrollAreaViewport>(v => v.AddUnmatched("data-testid", "viewport")));

        IElement style = cut.Find("style");
        Assert.Equal(Css, style.TextContent);
        Assert.False(style.HasAttribute("nonce"));
        Assert.Equal("base-ui-disable-scrollbar", cut.Find("[data-testid=viewport]").ClassName);
    }

    [Fact(DisplayName = "port: does not render inline style tags when disableStyleElements is true (ScrollArea)")]
    [Trait("Requirement", "UTL-DOM-33")]
    [Trait("Requirement", "SCR-DOM-05")]
    public void Does_not_render_inline_style_tags_when_disableStyleElements_is_true()
    {
        IRenderedComponent<CspProvider> cut = context.Render<CspProvider>(p => p
            .Add(x => x.DisableStyleElements, true)
            .AddChildContent<ScrollAreaRoot>(r => r.AddChildContent<ScrollAreaViewport>(v => v.AddUnmatched("data-testid", "viewport"))));

        Assert.Equal("base-ui-disable-scrollbar", cut.Find("[data-testid=viewport]").ClassName);
        Assert.Empty(cut.FindAll("style"));
    }
}

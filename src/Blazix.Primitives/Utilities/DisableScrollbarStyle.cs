using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Utilities;

// UTL-DOM-32, UTL-DOM-33 (utils/styles.tsx, styleDisableScrollbar): the scrollbar-hiding stylesheet ScrollArea.Root and
// Select.Popup emit, with the nearest CspProvider's nonce, and none under DisableStyleElements while the class stays applied.
// Design point 5 of M5 track 4 (settled by the orchestrator 2026-11-04): each emitter renders its own inline <style>, on
// prerender too; React 19 hoists and de-duplicates it per document (href, precedence) and Blazor does neither, a dated
// deviation on UTL-DOM-32's de-duplication clause. The caller wraps the call in a region at its own constant sequence number
// (BLZX2002), so the sequence numbers here are constants of the region.
/// <summary>Renders the inline style that hides an element's scrollbars.</summary>
internal static class DisableScrollbarStyle
{
    /// <summary>Represents the class name that hides an element's scrollbars once the style applies.</summary>
    public const string ClassName = "base-ui-disable-scrollbar";

    /// <summary>Represents the text of the style element.</summary>
    public const string Css = ".base-ui-disable-scrollbar{scrollbar-width:none}.base-ui-disable-scrollbar::-webkit-scrollbar{display:none}";

    /// <summary>Renders the style element, unless the nearest provider disables style elements.</summary>
    /// <param name="builder">The builder, inside a region the caller opened.</param>
    /// <param name="csp">The cascaded provider configuration, or <see langword="null"/> outside a provider.</param>
    public static void Render(RenderTreeBuilder builder, CspProviderContext? csp)
    {
        if (CspProviderContext.ReadDisableStyleElements(csp))
        {
            return;
        }

        builder.OpenElement(0, "style");
        if (CspProviderContext.ReadNonce(csp) is { } nonce)
        {
            builder.AddAttribute(1, "nonce", nonce);
        }

        builder.AddContent(2, Css);
        builder.CloseElement();
    }
}

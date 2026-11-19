using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

// PSH-STR-08, PSH-DOM-13 (utils/FocusGuard.tsx:10-42): the visually hidden, tabbable span a portal, the navigation menu's
// inline viewport and the toast viewport render around content whose focus a module routes. One renderer for the three
// (the Toast track's DP3 (a), chosen by the user 2026-11-19; granted by the orchestrator for `Popup/`); each caller's
// module routes the guard's focus, so the span carries no handler.
/// <summary>Renders a focus guard.</summary>
internal static class FocusGuard
{
    /// <summary>Renders one focus guard, in a region of its own so every guard of a part shares its sequence numbers.</summary>
    /// <param name="builder">The render tree builder of the part.</param>
    /// <param name="type">The guard's <c>data-type</c>, such as <c>outside</c> for a portal's; <see langword="null"/> for none.</param>
    public static void Render(RenderTreeBuilder builder, string? type = null)
    {
        builder.OpenRegion(0);
        builder.OpenElement(0, "span");
        builder.AddAttribute(1, "tabindex", "0");
        builder.AddAttribute(2, PopupMarkerAttributes.FocusGuard, string.Empty);
        if (type is not null)
        {
            builder.AddAttribute(3, "data-type", type);
        }

        builder.AddAttribute(4, "aria-hidden", "true");
        builder.AddAttribute(5, "style", VisuallyHidden.Style);
        builder.CloseElement();
        builder.CloseRegion();
    }
}

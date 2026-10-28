using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives;

// PSH-STR-09, PSH-DOM-14, PSH-ARIA-06 (utils/InternalBackdrop.tsx): the invisible element that covers the page under a
// modal popup, rendered by Dialog's Portal and Popover's Positioner (design point 3 of M4 track 8: one shared internal
// writer in Popup/, settled by the orchestrator 2026-10-28). Popover's cut-out is the `popup` module's `clip-path` write (ADR 04
// Amendments 2026-10-27), so no cut-out is rendered here. The outside press names it by its id.
/// <summary>Renders a modal popup's internal backdrop.</summary>
internal static class PopupInternalBackdrop
{
    private const string Style = "position: fixed; inset: 0; user-select: none; -webkit-user-select: none";

    /// <summary>Renders the internal backdrop in a region of its own; call it first in the owning fragment, whose sequence 0 the region takes.</summary>
    /// <param name="builder">The owning part's render tree builder.</param>
    /// <param name="id">The id the backdrop renders.</param>
    /// <param name="inert">Whether the backdrop carries <c>inert</c>, as while its popup closes.</param>
    /// <param name="capture">Receives the backdrop's element reference, or <see langword="null"/> when the owner needs none.</param>
    public static void Render(RenderTreeBuilder builder, string id, bool inert, Action<ElementReference>? capture = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.OpenRegion(0);
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "id", id);
        builder.AddAttribute(2, "role", "presentation");

        // Floating UI's outside press treats it as an element that existed when the popup rendered.
        builder.AddAttribute(3, PopupMarkerAttributes.Inert, string.Empty);
        builder.AddAttribute(4, "style", Style);
        if (inert)
        {
            builder.AddAttribute(5, "inert", true);
        }

        if (capture is not null)
        {
            builder.AddElementReferenceCapture(6, capture);
        }

        builder.CloseElement();
        builder.CloseRegion();
    }
}

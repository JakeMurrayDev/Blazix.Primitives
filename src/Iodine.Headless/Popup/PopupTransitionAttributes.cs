using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// D11 (TRN-STA-03; ADR 04 Amendments 2026-10-18 and 2026-10-21, the handover for a newly rendered element; settled by the
// orchestrator 2026-11-10): a popup part's `transitions` handle attaches after the render that created its element, and
// after the portal's move, so until the attach C# renders the status attributes itself. The opening render of a popup that
// is not kept mounted then already carries data-starting-style. From the attach on the part renders the values it rendered
// last (frozen), so a diff never fights the module; a released handle releases them. DialogTransitions' rule, shared by
// the popup families' parts.
/// <summary>Renders a popup part's transition status attributes until its <c>transitions</c> handle takes them over.</summary>
internal sealed class PopupTransitionAttributes
{
    private bool frozen;
    private bool renderedStarting;
    private bool renderedEnding;

    /// <summary>Adds the status attributes the part renders: its own until the handover, then the frozen ones.</summary>
    /// <param name="attributes">The part's attributes.</param>
    /// <param name="status">The transition status of this render.</param>
    public void Add(AttributeList attributes, TransitionStatus? status)
    {
        if (!frozen)
        {
            renderedStarting = status == TransitionStatus.Starting;
            renderedEnding = status == TransitionStatus.Ending;
        }

        if (renderedStarting)
        {
            attributes.Set(PopupDataAttributes.StartingStyle, string.Empty);
        }

        if (renderedEnding)
        {
            attributes.Set(PopupDataAttributes.EndingStyle, string.Empty);
        }
    }

    /// <summary>Freezes the rendered attributes; call it right before the handle's attach is sent.</summary>
    public void HandOver() => frozen = true;

    /// <summary>Renders the status again; call it when the handle is released while the part lives on.</summary>
    public void Release() => frozen = false;
}

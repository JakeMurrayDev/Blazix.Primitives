using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// CTX-STR-05, DOM-06, ARIA-03 (Base UI's ContextMenu.Separator is Separator; context-menu/index.parts.ts:21,
// separator/Separator.tsx:25). ADR 08 §3: the state record is sealed, partial and [PartState]; the default projection gives
// data-orientation.
/// <summary>Represents the state of a context menu separator, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Orientation">The separator's orientation; projected as <c>data-orientation</c>.</param>
[PartState]
public sealed partial record ContextMenuSeparatorState(Orientation Orientation);

// context-menu/index.ts's ContextMenuSeparatorDataAttributes (Separator's).
/// <summary>Provides the names of the data attributes a context menu separator renders.</summary>
public static class ContextMenuSeparatorDataAttributes
{
    /// <summary>Represents the attribute that names the separator's orientation: <c>data-orientation</c>.</summary>
    public const string Orientation = "data-orientation";
}

// Base UI's ContextMenu.Separator (context-menu/index.parts.ts:21 re-exports Separator); the contract is
// docs/components/context-menu.md §3.4 (CTX-STR-05, DOM-06, ARIA-03). Its own part over SeparatorAttributes (the
// MenuSeparator and ToolbarSeparator precedent; ADR 08 Amendments 2026-11-03 (3), chosen by the user): it needs no menu
// ancestor and does not register with the item list.
/// <summary>
/// Represents a line that separates items of a context menu, Base UI's <c>ContextMenu.Separator</c>, rendered as a
/// <c>div</c>.
/// </summary>
public sealed class ContextMenuSeparator : PartBase<ContextMenuSeparatorState>
{
    private ContextMenuSeparatorState? state;

    /// <summary>Gets or sets the separator's orientation.</summary>
    /// <value><see cref="Blazix.Primitives.Orientation.Horizontal"/> by default.</value>
    [Parameter]
    public Orientation Orientation { get; set; } = Orientation.Horizontal;

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override ContextMenuSeparatorState State
    {
        get
        {
            if (state is null || state.Orientation != Orientation)
            {
                state = new ContextMenuSeparatorState(Orientation);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => SeparatorAttributes.Write(attributes, Orientation);
}

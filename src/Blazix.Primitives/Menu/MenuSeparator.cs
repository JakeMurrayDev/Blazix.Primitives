using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// MNU §3.18 (Base UI's Menu.Separator is Separator; separator/Separator.tsx:25). ADR 08 §3: the state record is sealed,
// partial and [PartState]; the default projection gives data-orientation.
/// <summary>Represents the state of a menu separator, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Orientation">The separator's orientation; projected as <c>data-orientation</c>.</param>
[PartState]
public sealed partial record MenuSeparatorState(Orientation Orientation);

// menu/index.ts's MenuSeparatorDataAttributes (Separator's).
/// <summary>Provides the names of the data attributes a menu separator renders.</summary>
public static class MenuSeparatorDataAttributes
{
    /// <summary>Represents the attribute that names the separator's orientation: <c>data-orientation</c>.</summary>
    public const string Orientation = "data-orientation";
}

// Base UI's Menu.Separator (menu/index.parts.ts re-exports Separator); the contract is docs/components/menu.md §3.18
// (MNU-STR-10, DOM-27, ARIA-12). Its own part over SeparatorAttributes (the ToolbarSeparator precedent; ADR 08 Amendments
// 2026-11-03 (3), chosen by the user).
/// <summary>Represents a line that separates items of a menu, Base UI's <c>Menu.Separator</c>, rendered as a <c>div</c>.</summary>
public sealed class MenuSeparator : PartBase<MenuSeparatorState>
{
    private MenuSeparatorState? state;

    /// <summary>Gets or sets the separator's orientation.</summary>
    /// <value><see cref="Blazix.Primitives.Orientation.Horizontal"/> by default.</value>
    [Parameter]
    public Orientation Orientation { get; set; } = Orientation.Horizontal;

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override MenuSeparatorState State
    {
        get
        {
            if (state is null || state.Orientation != Orientation)
            {
                state = new MenuSeparatorState(Orientation);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => SeparatorAttributes.Write(attributes, Orientation);
}

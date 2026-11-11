using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// CMB §3.26, STA-15 (combobox/separator/ComboboxSeparator.tsx; ListboxSeparatorAttributes).
/// <summary>Represents the state of a combobox separator, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Orientation">The separator's orientation; projected as <c>data-orientation</c>.</param>
[PartState]
public sealed partial record ComboboxSeparatorState(
    Orientation Orientation);

// utils/listbox-separator/ListboxSeparator.tsx (CMB-DOM-16; CMB-API-69 names the class).
/// <summary>Provides the names of the data attributes a combobox separator renders.</summary>
public static class ComboboxSeparatorDataAttributes
{
    /// <summary>Represents the attribute whose value is the separator's orientation: <c>data-orientation</c>.</summary>
    public const string Orientation = "data-orientation";
}

// Base UI's Combobox.Separator (combobox/separator/ComboboxSeparator.tsx; CMB §3.26, STR-08): the listbox separator's
// attributes (ListboxSeparatorAttributes, UTL), data-orientation from the state.
/// <summary>Represents a separator between a combobox's items, Base UI's <c>Combobox.Separator</c>, rendered as a <c>div</c>.</summary>
public sealed class ComboboxSeparator : PartBase<ComboboxSeparatorState>
{
    private ComboboxSeparatorState state = new(Primitives.Orientation.Horizontal);

    /// <summary>Gets or sets the separator's orientation.</summary>
    /// <value>The default is <see cref="Orientation.Horizontal"/>.</value>
    [Parameter]
    public Orientation Orientation { get; set; } = Primitives.Orientation.Horizontal;

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override ComboboxSeparatorState State
    {
        get
        {
            if (state.Orientation != Orientation)
            {
                state = new ComboboxSeparatorState(Orientation);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => ComboboxStaticAttributes.WriteSeparator(attributes);
}

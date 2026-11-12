using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// AUT-STR-02, STR-05 (M5 track 11; decision 12, ADR 08 Amendments 2026-11-03 (3)): the Autocomplete parts of this file
// are Combobox's, as sealed shells over the same internal behaviours with the family `Autocomplete`; the comments below
// cite the Combobox rows the Autocomplete document adopts (autocomplete.md §2.1).

// CMB §3.26, STA-15 (combobox/separator/AutocompleteSeparator.tsx; ListboxSeparatorAttributes).
/// <summary>Represents the state of an autocomplete separator, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Orientation">The separator's orientation; projected as <c>data-orientation</c>.</param>
[PartState]
public sealed partial record AutocompleteSeparatorState(
    Orientation Orientation);

// utils/listbox-separator/ListboxSeparator.tsx (CMB-DOM-16; CMB-API-69 names the class).
/// <summary>Provides the names of the data attributes an autocomplete separator renders.</summary>
public static class AutocompleteSeparatorDataAttributes
{
    /// <summary>Represents the attribute whose value is the separator's orientation: <c>data-orientation</c>.</summary>
    public const string Orientation = "data-orientation";
}

// Base UI's Combobox.Separator (combobox/separator/AutocompleteSeparator.tsx; CMB §3.26, STR-08): the listbox separator's
// attributes (ListboxSeparatorAttributes, UTL), data-orientation from the state.
/// <summary>Represents a separator between an autocomplete's items, Base UI's <c>Autocomplete.Separator</c>, rendered as a <c>div</c>.</summary>
public sealed class AutocompleteSeparator : PartBase<AutocompleteSeparatorState>
{
    private AutocompleteSeparatorState state = new(Primitives.Orientation.Horizontal);

    /// <summary>Gets or sets the separator's orientation.</summary>
    /// <value>The default is <see cref="Orientation.Horizontal"/>.</value>
    [Parameter]
    public Orientation Orientation { get; set; } = Primitives.Orientation.Horizontal;

    /// <inheritdoc />
    protected override string DefaultTagName => "div";

    /// <inheritdoc />
    protected override AutocompleteSeparatorState State
    {
        get
        {
            if (state.Orientation != Orientation)
            {
                state = new AutocompleteSeparatorState(Orientation);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes) => ComboboxStaticAttributes.WriteSeparator(attributes);
}

using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// MNU §3.22 state object (filter-dropdown/input/FilterDropdownInput.tsx:55-57,141-147; MNU-STA-22). Highlighted depends on
// focus-visible and keyboard modality, both known only when the event happens: the menu-filter module writes
// data-highlighted and C# mirrors it (M5 track 12's design point 5 (a), settled by the orchestrator 2026-11-11).
/// <summary>Represents the state of a menu's filter input, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Highlighted">
/// A value indicating whether the input shows its focus ring, cleared while keyboard navigation highlights an item; its
/// <c>data-highlighted</c> attribute is written by the browser-side behavior.
/// </param>
[PartState]
public sealed partial record MenuInputState([property: MirroredField] bool Highlighted);

// menu/input/MenuInputDataAttributes.ts (MNU-DOM-21).
/// <summary>Provides the names of the data attributes a menu's filter input renders.</summary>
public static class MenuInputDataAttributes
{
    /// <summary>Represents the attribute present while the input shows its focus ring: <c>data-highlighted</c>.</summary>
    public const string Highlighted = "data-highlighted";
}

// Base UI's Menu.Input (menu/input/MenuInput.tsx, filter-dropdown/input/FilterDropdownInput.tsx); the contract is
// docs/components/menu.md §3.22 (MNU-STR-15, API-59, DOM-21, ARIA-14, STA-22, EVT-16, EVT-19, KEY-31, A11Y-02). value and
// aria-activedescendant are the browser-side behavior's after attach (the Input/Field.Control precedent of ADR 04
// Amendments 2026-10-22; ADR 04 :504).
/// <summary>
/// Represents the text input that filters a menu's items, Base UI's <c>Menu.Input</c>, rendered as an <c>input</c> with
/// the <c>searchbox</c> role.
/// </summary>
/// <remarks>It must be placed in a menu wrapped in <see cref="MenuFilterProvider"/>.</remarks>
public sealed class MenuInput : PartBase<MenuInputState>
{
    private MenuInputState state = new(false);
    private MenuFilterRootContext? filter;

    [CascadingParameter]
    private MenuRootContext? RootContext { get; set; }

    /// <summary>Gets or sets a value indicating whether the input ignores user interaction.</summary>
    /// <value>The default is <see langword="false"/>. The input is also disabled while its menu is.</value>
    [Parameter]
    public bool Disabled { get; set; }

    /// <inheritdoc />
    protected override string DefaultTagName => "input";

    /// <inheritdoc />
    protected override MenuInputState State => state;

    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    /// <inheritdoc />
    protected override void OnInitialized()
    {
        // MNU-EDGE-04, STR-15.
        filter = RootContext?.Filter ?? throw MenuParts.MissingFilter("Input");
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (state.Highlighted != filter!.InputHighlighted)
        {
            state = new MenuInputState(filter.InputHighlighted);
        }
    }

    // MNU-DOM-21 (filter-dropdown/input/FilterDropdownInput.tsx:44-53, 150-163; menu/input/MenuInput.tsx:32-65), API-59: the
    // id stays on the part's own element (issue 115); disabled is the root's or the part's. value is the committed query,
    // which C# renders (C# -> JS, ADR 04 Amendments 2026-10-22); aria-activedescendant and data-highlighted are the
    // browser-side behaviors'.
    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        attributes.Set("type", "text");
        attributes.Set("role", "searchbox");
        attributes.Set("inputmode", "search");
        attributes.Set("autocomplete", "off");
        attributes.Set("spellcheck", "false");
        attributes.Set("autocorrect", "off");
        attributes.Set("autocapitalize", "none");
        if (filter!.ListId is { } listId)
        {
            attributes.Set("aria-controls", listId);
        }

        if (Disabled || RootContext!.State.Disabled)
        {
            attributes.Set("disabled", true);
        }

        attributes.Set("value", filter.Query);
    }
}

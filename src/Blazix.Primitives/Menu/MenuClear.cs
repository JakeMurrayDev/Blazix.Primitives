using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// MNU §3.23 state object (filter-dropdown/clear/FilterDropdownClear.tsx; MNU-API-60).
/// <summary>Represents the state of a menu's clear button, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the button ignores user interaction; projected as <c>data-disabled</c>.</param>
[PartState]
public sealed partial record MenuClearState(bool Disabled);

// menu/clear/MenuClearDataAttributes.ts (MNU-DOM-22).
/// <summary>Provides the names of the data attributes a menu's clear button renders.</summary>
public static class MenuClearDataAttributes
{
    /// <summary>Represents the attribute present while the button is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";
}

// Base UI's Menu.Clear (menu/clear/MenuClear.tsx, filter-dropdown/clear/FilterDropdownClear.tsx); the contract is
// docs/components/menu.md §3.23 (MNU-API-60, DOM-22, PTR-20, PTR-21; UBT-PTR-01). Interface commit: the acting bodies throw
// until their red-first commits.
/// <summary>
/// Represents the button that clears a menu's filter query, Base UI's <c>Menu.Clear</c>, rendered as a <c>button</c> only
/// while the query is not empty.
/// </summary>
/// <remarks>It must be placed in a menu wrapped in <see cref="MenuFilterProvider"/>.</remarks>
public sealed class MenuClear : PartBase<MenuClearState>
{
    /// <summary>Gets or sets a value indicating whether the button ignores user interaction.</summary>
    /// <value>The default is <see langword="false"/>. The button is also disabled while its menu is.</value>
    [Parameter]
    public bool Disabled { get; set; }

    /// <summary>Gets or sets a value indicating whether the rendered element is a native <c>button</c>.</summary>
    /// <value>The default is <see langword="true"/>. Set it to <see langword="false"/> when the element rendered in its place is not a button.</value>
    [Parameter]
    public bool NativeButton { get; set; } = true;

    /// <inheritdoc />
    protected override string DefaultTagName => "button";

    /// <inheritdoc />
    protected override MenuClearState State => throw new NotImplementedException();

    /// <inheritdoc />
    protected override void OnInitialized() => throw new NotImplementedException();
}

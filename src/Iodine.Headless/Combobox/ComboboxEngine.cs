using Blazix.Primitives.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// The engine's configuration (combobox/root/AriaCombobox.tsx, the internal engine ComboboxRoot.tsx and AutocompleteRoot.tsx
// configure; CMB §3.1, §16 Q4; ADR 08 Amendments 2026-11-03 (3): Combobox is written as behaviours plus thin parts and
// Autocomplete shells them). The selection mode and the engine-only inputs Combobox hides sit on the shared root behaviour
// from the first commit (M5 track 10 brief, "Adoption"); Autocomplete proves `None`.
/// <summary>Specifies what the engine remembers of an item press, Base UI's <c>selectionMode</c>.</summary>
internal enum ComboboxSelectionMode
{
    /// <summary>The last selected value (<c>single</c>).</summary>
    Single,

    /// <summary>Every selected value (<c>multiple</c>).</summary>
    Multiple,

    /// <summary>Nothing: the input text is the value (<c>none</c>, Autocomplete).</summary>
    None,
}

// AriaCombobox.tsx:287-292: `false | 'input-change' | 'always'`, derived from the public `boolean | 'always'`.
/// <summary>Specifies when the engine highlights the first item by itself.</summary>
internal enum ComboboxAutoHighlightMode
{
    /// <summary>Never (<c>false</c>).</summary>
    Off,

    /// <summary>After the user types, keeping the highlight while the query changes (<c>true</c>, Base UI's <c>'input-change'</c>).</summary>
    InputChange,

    /// <summary>As soon as the list opens (<c>'always'</c>).</summary>
    Always,
}

// AriaCombobox.tsx:1931-1938, the engine's ARIA `autoComplete` mode; Combobox passes `List` (ComboboxRoot.tsx omits the prop).
/// <summary>Specifies the engine's ARIA autocomplete mode, rendered as <c>aria-autocomplete</c>.</summary>
internal enum ComboboxAriaAutoComplete
{
    /// <summary>Items are filtered by the input text, which does not change with the highlight (<c>list</c>).</summary>
    List,

    /// <summary>Items are filtered and the input text follows the highlight (<c>both</c>).</summary>
    Both,

    /// <summary>Items are not filtered and the input text follows the highlight (<c>inline</c>).</summary>
    Inline,

    /// <summary>Items are not filtered and the input text does not change (<c>none</c>).</summary>
    None,
}

/// <summary>The names of the root parameters the engine's three controlled values read.</summary>
/// <param name="Value">The single-mode value's parameter, or <see langword="null"/> when the family has none.</param>
/// <param name="DefaultValue">The single-mode default's parameter, or <see langword="null"/>.</param>
/// <param name="Values">The multiple-mode value's parameter, or <see langword="null"/>.</param>
/// <param name="DefaultValues">The multiple-mode default's parameter, or <see langword="null"/>.</param>
/// <param name="InputValue">The input value's parameter, or <see langword="null"/> when the root gives the text as <c>InputText</c>.</param>
/// <param name="DefaultInputValue">The input value's default parameter, or <see langword="null"/> with <paramref name="InputValue"/>.</param>
internal readonly record struct ComboboxValueNames(string? Value, string? DefaultValue, string? Values, string? DefaultValues, string? InputValue, string? DefaultInputValue)
{
    /// <summary>Gets Combobox's names.</summary>
    public static ComboboxValueNames Combobox => new("Value", "DefaultValue", "Values", "DefaultValues", "InputValue", "DefaultInputValue");
}

// The services every part behaviour takes, so a shell passes one value (the family names its parts' diagnostics, ADR 08
// Amendments 2026-11-03 (3)).
/// <summary>The services and callbacks a combobox part behavior runs with.</summary>
/// <param name="Family">The family whose parts the diagnostics name: <c>Combobox</c> or <c>Autocomplete</c>.</param>
/// <param name="JSRuntime">The runtime the behavior's twins import their modules through.</param>
/// <param name="Logger">The part's logger.</param>
/// <param name="Ids">The scoped id generator.</param>
/// <param name="RequestRender">Asks the part to render, its <c>StateHasChanged</c>.</param>
/// <param name="InvokeAsync">Runs work on the part's renderer, its <c>InvokeAsync</c>.</param>
/// <param name="DispatchException">Routes an exception to the part.</param>
internal readonly record struct ComboboxPartServices(
    string Family,
    IJSRuntime JSRuntime,
    ILogger Logger,
    PartIdGenerator Ids,
    Action RequestRender,
    Func<Func<Task>, Task> InvokeAsync,
    Func<Exception, Task> DispatchException);

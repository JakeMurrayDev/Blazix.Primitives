using System.Text.Json.Serialization;

namespace Blazix.Primitives.Interop;

// ADR 04 §1 (the collapsible component module, placement rule of 2026-10-18; Amendments 2026-10-21), §2 (Collapsible.Panel:
// hidden="until-found", the measured size variables; every animated part: data-starting-style, data-ending-style) and
// Amendments 2026-10-18 (handover at attach) and 2026-10-21 (openSelector). COL-ANI-13/20 (the suppression),
// COL-ANI-15/18/19 (PanelId, by which a withheld or replaced element is found again).
/// <summary>
/// The options of the <c>collapsible</c> module, sent on <c>attach</c> and on every <c>update</c>. From <c>attach</c>
/// on, the module owns the panel element's <c>style</c>, <c>data-starting-style</c>, <c>data-ending-style</c> and
/// <c>hidden="until-found"</c>; the part keeps rendering every other attribute.
/// </summary>
/// <remarks>
/// <see cref="TwinOptions.Style"/> is the consumer's merged style string; the module composes the element's style from
/// it, the measured size variables and its own temporary declarations.
/// </remarks>
internal sealed record CollapsibleOptions : TwinOptions
{
    /// <summary>Gets a value indicating whether the panel is open.</summary>
    /// <value><see langword="true"/> while open; the default is <see langword="false"/>. The module reads it while no element can be read.</value>
    public bool Open { get; init; }

    /// <summary>Gets a value indicating whether the panel was mounted when its element was rendered.</summary>
    /// <value>The mounted flag the part rendered; read when the module starts on an element.</value>
    public bool Mounted { get; init; }

    /// <summary>Gets the transition status the panel's element was rendered with.</summary>
    /// <value>The status the part rendered, or <see langword="null"/> for the unset status; read when the module starts on an element.</value>
    [JsonConverter(typeof(TransitionStatusJsonConverter))]
    public TransitionStatus? Status { get; init; }

    /// <summary>Gets a value indicating whether the element stays in the document while the panel is closed.</summary>
    /// <value><see langword="true"/> to keep the element; the default is <see langword="false"/>.</value>
    public bool KeepMounted { get; init; }

    /// <summary>Gets a value indicating whether the closed element is hidden with <c>hidden="until-found"</c>.</summary>
    /// <value><see langword="true"/> to let find-in-page reveal the closed panel; the default is <see langword="false"/>.</value>
    public bool HiddenUntilFound { get; init; }

    /// <summary>Gets a value indicating whether the panel's opening keyframe animation is suppressed until it closes.</summary>
    /// <value>
    /// <see langword="true"/> for a panel open on its first render or attached again while open, which keeps
    /// <c>animation-name: none</c> until the panel closes; read when the module starts on an element. The default is
    /// <see langword="false"/>.
    /// </value>
    public bool SuppressOpenAnimation { get; init; }

    /// <summary>Gets the panel's id.</summary>
    /// <value>The id the panel renders, by which the module finds a replaced or withheld element again.</value>
    public string PanelId { get; init; } = string.Empty;

    /// <summary>Gets the CSS selector that the element matches while the part renders it open.</summary>
    /// <value><c>[data-open]</c> for a panel; the module reads the open state from the element after every render, before paint.</value>
    public string? OpenSelector { get; init; }

    // ACC-DOM-17; ADR 04 Amendments 2026-10-26 (Accordion's names, an option of the module; chosen by the user 2026-10-26).
    /// <summary>Gets the name of the CSS variable that carries the panel's measured height.</summary>
    /// <value>
    /// The variable's name, such as <c>--accordion-panel-height</c>; <see langword="null"/>, the default and omitted from
    /// the JSON, for <c>--collapsible-panel-height</c>. Read when the module starts on an element.
    /// </value>
    public string? HeightVariable { get; init; }

    /// <summary>Gets the name of the CSS variable that carries the panel's measured width.</summary>
    /// <value>
    /// The variable's name, such as <c>--accordion-panel-width</c>; <see langword="null"/>, the default and omitted from
    /// the JSON, for <c>--collapsible-panel-width</c>. Read when the module starts on an element.
    /// </value>
    public string? WidthVariable { get; init; }
}

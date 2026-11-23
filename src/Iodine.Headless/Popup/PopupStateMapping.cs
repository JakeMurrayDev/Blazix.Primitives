using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// PSH-DOM-01..05; utils/popupStateMapping.ts. The four mappings the families' parts call: a trigger's Open is
// data-popup-open (PSH-DOM-01), a pressable trigger's adds data-pressed (PSH-DOM-02), a popup part's Open is data-open or
// data-closed (PSH-DOM-03) and its AnchorHidden data-anchor-hidden (PSH-DOM-04). popupTransitionStateMapping adds the
// transition status (PSH-DOM-05), whose attributes the `transitions` module writes (ADR 04 §2), so in C# it is the popup
// mapping. The shared static arrays make a render allocate nothing.
/// <summary>Provides the mappings from a popup part's open and anchor-hidden fields to their data attributes.</summary>
internal static class PopupStateMapping
{
    private const string OpenField = "Open";
    private const string AnchorHiddenField = "AnchorHidden";

    private static readonly StateDataAttribute[] s_triggerOpen = [new(PopupTriggerDataAttributes.PopupOpen, string.Empty)];
    private static readonly StateDataAttribute[] s_pressableTriggerOpen = [new(PopupTriggerDataAttributes.PopupOpen, string.Empty), new(PopupTriggerDataAttributes.Pressed, string.Empty)];
    private static readonly StateDataAttribute[] s_open = [new(PopupDataAttributes.Open, string.Empty)];
    private static readonly StateDataAttribute[] s_closed = [new(PopupDataAttributes.Closed, string.Empty)];
    private static readonly StateDataAttribute[] s_anchorHidden = [new(PopupDataAttributes.AnchorHidden, string.Empty)];

    /// <summary>Gets the projection of a trigger's open field: <c>data-popup-open</c> while open, nothing otherwise.</summary>
    public static StateFieldProjection<bool> TriggerOpen { get; } = static open => open ? s_triggerOpen : null;

    /// <summary>Gets the projection of a pressable trigger's open field: <c>data-popup-open</c> and <c>data-pressed</c> while open.</summary>
    public static StateFieldProjection<bool> PressableTriggerOpen { get; } = static open => open ? s_pressableTriggerOpen : null;

    /// <summary>Gets the projection of a popup part's open field: <c>data-open</c> or <c>data-closed</c>.</summary>
    public static StateFieldProjection<bool> PopupOpen { get; } = static open => open ? s_open : s_closed;

    /// <summary>Gets the projection of a popup part's anchor-hidden field: <c>data-anchor-hidden</c> while hidden.</summary>
    public static StateFieldProjection<bool> AnchorHidden { get; } = static hidden => hidden ? s_anchorHidden : null;

    /// <summary>Creates the mapping of a trigger whose state's <c>Open</c> field is its <c>isOpenedByTrigger</c>.</summary>
    /// <typeparam name="TState">The type of the trigger's state record.</typeparam>
    /// <returns>The mapping.</returns>
    public static StateAttributesMapping<TState> CreateTrigger<TState>() => new StateAttributesMapping<TState>().Map(OpenField, TriggerOpen);

    /// <summary>Creates the mapping of a pressable trigger.</summary>
    /// <typeparam name="TState">The type of the trigger's state record.</typeparam>
    /// <returns>The mapping.</returns>
    public static StateAttributesMapping<TState> CreatePressableTrigger<TState>() => new StateAttributesMapping<TState>().Map(OpenField, PressableTriggerOpen);

    /// <summary>Creates the mapping of a popup, positioner, backdrop or arrow part with <c>Open</c> and <c>AnchorHidden</c> fields.</summary>
    /// <typeparam name="TState">The type of the part's state record.</typeparam>
    /// <returns>The mapping.</returns>
    public static StateAttributesMapping<TState> CreatePopup<TState>() =>
        new StateAttributesMapping<TState>().Map(OpenField, PopupOpen).Map(AnchorHiddenField, AnchorHidden);

    /// <summary>Creates the mapping of a part with only an <c>Open</c> field projected as <c>data-open</c> or <c>data-closed</c>.</summary>
    /// <typeparam name="TState">The type of the part's state record.</typeparam>
    /// <returns>The mapping.</returns>
    public static StateAttributesMapping<TState> CreateOpen<TState>() => new StateAttributesMapping<TState>().Map(OpenField, PopupOpen);
}

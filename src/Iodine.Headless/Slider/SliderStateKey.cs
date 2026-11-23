using Blazix.Primitives.Utilities;

namespace Blazix.Primitives;

// SLD-STA-05: every part's state record carries the Root's fourteen fields. A part rebuilds its record only when this key
// changed (ADR 16 rule 7). The list compares by reference: the store makes a new one per change (SliderStoreState.Values).
/// <summary>The fourteen fields of a slider part's state, read from the store.</summary>
/// <param name="ActiveThumbIndex">The active thumb's index, or -1.</param>
/// <param name="Disabled">Whether the slider is disabled.</param>
/// <param name="Dragging">Whether a gesture shows as a drag.</param>
/// <param name="Orientation">The axis.</param>
/// <param name="Max">The upper bound.</param>
/// <param name="Min">The lower bound.</param>
/// <param name="MinStepsBetweenValues">The minimum number of steps between adjacent values.</param>
/// <param name="Step">The step.</param>
/// <param name="Values">The normalized values.</param>
/// <param name="Field">The field's flags.</param>
internal readonly record struct SliderStateKey(
    int ActiveThumbIndex,
    bool Disabled,
    bool Dragging,
    Orientation Orientation,
    double Max,
    double Min,
    double MinStepsBetweenValues,
    double Step,
    IReadOnlyList<double> Values,
    FieldStateSnapshot Field)
{
    /// <summary>Reads the key from the store's record.</summary>
    /// <param name="state">The record.</param>
    /// <returns>The key.</returns>
    public static SliderStateKey From(SliderStoreState state) =>
        new(state.ActiveThumbIndex, state.Disabled, state.Dragging, state.Orientation, state.Max, state.Min, state.MinStepsBetweenValues, state.Step, state.Values, state.Field);

    // The fields every part projects (SLD-DOM-02): a change of one renders the part.
    /// <summary>Selects the projected fields.</summary>
    /// <param name="state">The record.</param>
    /// <returns>The disabled, dragging and orientation flags and the field's flags.</returns>
    public static (bool Disabled, bool Dragging, Orientation Orientation, FieldStateSnapshot Field) Projected(SliderStoreState state) =>
        (state.Disabled, state.Dragging, state.Orientation, state.Field);
}

// SLD-DOM-05/06/09..12, SLD-I18N-03, UTL-DOM-13/37: the inline styles C# renders under the handover (ADR 04 Amendments
// 2026-10-18), once, before the `slider` module owns them; the module writes the same declarations in `component-under`.
/// <summary>Builds the inline styles of a slider's thumbs, indicators and inputs as their first render shows them.</summary>
internal static class SliderStyles
{
    // SLD-DOM-14: the shared visually hidden style with a 100% box, so VoiceOver's focus ring matches the thumb.
    private const string InputStyle = "clip-path: inset(50%); overflow: hidden; white-space: nowrap; border: 0; padding: 0; width: 100%; height: 100%; margin: 0; position: fixed; top: 0; left: 0";

    // SLD-I18N-03: `writing-mode` for a vertical slider.
    /// <summary>Returns the inline style of a thumb's input.</summary>
    /// <param name="vertical">Whether the slider is vertical.</param>
    /// <param name="rtl">Whether the direction is right to left.</param>
    /// <returns>The style, with <c>writing-mode</c> for a vertical slider.</returns>
    public static string Input(bool vertical, bool rtl) => !vertical ? InputStyle : rtl ? InputStyle + "; writing-mode: vertical-rl" : InputStyle + "; writing-mode: vertical-lr";

    // slider/thumb/SliderThumb.tsx:276-304.
    /// <summary>Returns the inline style of a thumb element before the module positions it.</summary>
    /// <param name="state">The store's record.</param>
    /// <param name="index">The thumb's index.</param>
    /// <returns>The style.</returns>
    public static string Thumb(SliderStoreState state, int index)
    {
        bool vertical = state.Orientation == Orientation.Vertical;
        double percent = index >= 0 && index < state.Values.Count ? RangeMath.ValueToPercent(state.Values[index], state.Min, state.Max) : double.NaN;
        if (!state.Inset && !double.IsFinite(percent))
        {
            return VisuallyHidden.Style;
        }

        string startEdge = vertical ? "bottom" : "inset-inline-start";
        string cross = vertical ? "left" : "top";
        string start = state.Inset ? "var(--position)" : NumberSerializer.Serialize(percent) + "%";
        string translate = (vertical || !state.Rtl ? "-50%" : "50%") + " " + (vertical ? "50%" : "-50%");
        string style = "position: absolute; " + startEdge + ": " + start + "; " + cross + ": 50%; translate: " + translate;
        int safeLastUsed = state.LastUsedThumbIndex >= 0 && state.LastUsedThumbIndex < state.Values.Count ? state.LastUsedThumbIndex : -1;
        string? zIndex = null;
        if (state.Values.Count > 1)
        {
            zIndex = state.ActiveThumbIndex == index ? "2" : safeLastUsed == index ? "1" : null;
        }
        else if (state.ActiveThumbIndex == index)
        {
            zIndex = "1";
        }

        if (zIndex is not null)
        {
            style += "; z-index: " + zIndex;
        }

        // SLD-DOM-10, SLD-ANI-02: C# never knows the inset position, so an inset thumb renders hidden at 0%.
        return state.Inset ? style + "; --position: 0%; visibility: hidden" : style;
    }

    // slider/indicator/SliderIndicator.tsx:11-48.
    /// <summary>Returns the inline style of an indicator before the module positions it.</summary>
    /// <param name="state">The store's record.</param>
    /// <returns>The style.</returns>
    public static string Indicator(SliderStoreState state)
    {
        bool vertical = state.Orientation == Orientation.Vertical;
        bool range = state.Values.Count > 1;
        string position = vertical ? "position: absolute; width: inherit" : "position: relative; height: inherit";
        string startEdge = vertical ? "bottom" : "inset-inline-start";
        string size = vertical ? "height" : "width";
        if (state.Inset)
        {
            // SLD-DOM-06, SLD-ANI-02: hidden with zero variables until the module's first pass.
            return range
                ? "visibility: hidden; " + position + "; --start-position: 0%; --relative-size: 0%; " + startEdge + ": var(--start-position); " + size + ": var(--relative-size)"
                : "visibility: hidden; " + position + "; --start-position: 0%; " + startEdge + ": 0; " + size + ": var(--start-position)";
        }

        double start = state.Values.Count > 0 ? RangeMath.ValueToPercent(state.Values[0], state.Min, state.Max) : 0;
        double end = state.Values.Count > 0 ? RangeMath.ValueToPercent(state.Values[^1], state.Min, state.Max) : 0;
        return range
            ? position + "; " + startEdge + ": " + NumberSerializer.Serialize(start) + "%; " + size + ": " + NumberSerializer.Serialize(end - start) + "%"
            : position + "; " + startEdge + ": 0; " + size + ": " + NumberSerializer.Serialize(start) + "%";
    }
}

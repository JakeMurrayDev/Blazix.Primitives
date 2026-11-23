using System.Globalization;
using Iodine.Headless.Store;

namespace Iodine.Headless;

// SLD §4 (slider/root/SliderRoot.tsx:110-299, SliderRootContext.ts); ADR 05 §1. The store every Slider part subscribes to,
// each with a selector over the fields it reads (design point 3 of track 4: a step renders the Root, the changed Thumb, the
// Value and the Indicator only). Values is the normalized list (SLD-STA-03), a new instance per change, so a selection on
// it compares by reference; ValueVersion moves with it. Left out: the raw value (the Root's, for SLD-STA-09's equality),
// the thumb registry and the inputs' ids (SliderRootContext, non-reactive), the gesture snapshot and the inset positions
// (the `slider` module's), the field's validity data (FieldRootContext).
/// <summary>Represents the state of one slider: the normalized values, the bounds and the fields its parts observe.</summary>
/// <param name="Values">The normalized values: clamped to the range and sorted, one per thumb.</param>
/// <param name="ValueVersion">A counter moved by every change of <paramref name="Values"/>.</param>
/// <param name="Range">Whether the slider is in range mode: its value is a list, even of one element.</param>
/// <param name="Min">The lower bound.</param>
/// <param name="Max">The upper bound.</param>
/// <param name="Step">The step.</param>
/// <param name="LargeStep">The large step of the Page keys and Shift with an arrow key.</param>
/// <param name="MinStepsBetweenValues">The minimum number of steps between adjacent values.</param>
/// <param name="Orientation">The axis of the slider.</param>
/// <param name="Rtl">Whether the nearest direction provider is right to left.</param>
/// <param name="Disabled">Whether the slider is disabled, its own flag or its field's.</param>
/// <param name="Dragging">Whether a pointer or touch interaction is in progress, as the browser-side behavior last reported.</param>
/// <param name="ActiveThumbIndex">The index of the thumb whose input has focus, or -1.</param>
/// <param name="LastUsedThumbIndex">The last index <paramref name="ActiveThumbIndex"/> took that was not -1, or -1.</param>
/// <param name="Field">The enclosing field's flags, or the default flags outside a field.</param>
/// <param name="RootId">The root's id.</param>
/// <param name="LabelledBy">The resolved <c>aria-labelledby</c> of the root and the inputs, or <see langword="null"/>.</param>
/// <param name="Name">The effective form name of the inputs, or <see langword="null"/>.</param>
/// <param name="Form">The id of the inputs' owning form, or <see langword="null"/>.</param>
/// <param name="Format">The numeric format string, or <see langword="null"/>.</param>
/// <param name="Culture">The culture that formats the values, or <see langword="null"/> for the current culture.</param>
/// <param name="ThumbAlignment">How a thumb at a bound aligns with the control's edge.</param>
/// <param name="ThumbsVersion">A counter moved by every report of the thumbs' order.</param>
/// <param name="Attached">Whether the browser-side behavior is attached, from which it owns the thumbs' and indicators' style and the inputs' value.</param>
[StoreState]
internal sealed partial record SliderStoreState(
    IReadOnlyList<double> Values,
    int ValueVersion,
    bool Range,
    double Min,
    double Max,
    double Step,
    double LargeStep,
    double MinStepsBetweenValues,
    Orientation Orientation,
    bool Rtl,
    bool Disabled,
    bool Dragging,
    int ActiveThumbIndex,
    int LastUsedThumbIndex,
    FieldStateSnapshot Field,
    string RootId,
    string? LabelledBy,
    string? Name,
    string? Form,
    string? Format,
    CultureInfo? Culture,
    SliderThumbAlignment ThumbAlignment,
    int ThumbsVersion,
    bool Attached)
    : IStoreState<SliderStoreState>
{
    /// <summary>Gets a value indicating whether a thumb at a bound stays inside the control.</summary>
    /// <value><see langword="true"/> for <see cref="SliderThumbAlignment.Edge"/> and <see cref="SliderThumbAlignment.EdgeClientOnly"/>.</value>
    public bool Inset => ThumbAlignment != SliderThumbAlignment.Center;
}

using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// SEL-STA-20/21/22/25, SEL-POS-14 (select/positioner/SelectPositioner.tsx:114): Base UI's `Side | 'none'`. Decision 8 of the
// M5 plan (chosen by the user 2026-11-03) and design point 2 of M5 track 9 (the name, pending the user): a family enum beside
// its first consumer (ADR 08 §3's enum rule, a family-prefixed literal set), projected kebab-case; the Trigger's
// `popupSide` stays `Side?` (SEL-STA-28: never `none`).
/// <summary>Specifies the side of the anchor a select's popup is placed on, or <see cref="None"/> while it is placed over the trigger.</summary>
public enum SelectSide
{
    /// <summary>The popup is placed over the trigger, the selected item's text aligned with the value's: <c>none</c>.</summary>
    None,

    /// <summary>Above the anchor: <c>top</c>.</summary>
    Top,

    /// <summary>Below the anchor: <c>bottom</c>.</summary>
    Bottom,

    /// <summary>Left of the anchor: <c>left</c>.</summary>
    Left,

    /// <summary>Right of the anchor: <c>right</c>.</summary>
    Right,

    /// <summary>After the anchor in the reading direction: <c>inline-end</c>.</summary>
    InlineEnd,

    /// <summary>Before the anchor in the reading direction: <c>inline-start</c>.</summary>
    InlineStart,
}

// The projections of a select's sides: SEL-DOM-05's `data-popup-side` from the resolved side, and the mirrored placement's
// `SelectSide` from the positioning mirror plus the aligned flag. The static arrays make a render allocate nothing.
/// <summary>Provides the side projections and conversions of a select's parts.</summary>
internal static class SelectSideAttributes
{
    private static readonly StateDataAttribute[][] s_popupSide =
    [
        [new(SelectTriggerDataAttributes.PopupSide, "top")],
        [new(SelectTriggerDataAttributes.PopupSide, "bottom")],
        [new(SelectTriggerDataAttributes.PopupSide, "left")],
        [new(SelectTriggerDataAttributes.PopupSide, "right")],
        [new(SelectTriggerDataAttributes.PopupSide, "inline-end")],
        [new(SelectTriggerDataAttributes.PopupSide, "inline-start")],
    ];

    /// <summary>Gets the projection of the trigger's popup side: <c>data-popup-side</c> while it is known.</summary>
    public static StateFieldProjection<Side?> PopupSide { get; } = static side => side is { } value ? s_popupSide[(int)value] : null;

    /// <summary>Converts a mirrored side into a select side.</summary>
    /// <param name="side">The positioning behavior's side, or <see langword="null"/> before its first report.</param>
    /// <param name="aligned">Whether aligned placement is active.</param>
    /// <returns><see cref="SelectSide.None"/> while aligned; the side otherwise; <see langword="null"/> before a report.</returns>
    public static SelectSide? From(Side? side, bool aligned)
    {
        if (aligned)
        {
            return SelectSide.None;
        }

        return side switch
        {
            null => null,
            Side.Top => SelectSide.Top,
            Side.Bottom => SelectSide.Bottom,
            Side.Left => SelectSide.Left,
            Side.Right => SelectSide.Right,
            Side.InlineEnd => SelectSide.InlineEnd,
            _ => SelectSide.InlineStart,
        };
    }
}

using Iodine.Headless.Rendering;

namespace Iodine.Headless;

// SCR §5 preamble, RND-DOM-06/07; scroll-area/root/stateAttributes.ts:5-15 (scrollAreaStateAttributesMapping): the six
// overflow fields to their hyphenated names, each empty when true and absent when false; `cornerHidden` to nothing, which
// the records express with [NotProjected] (SCR-DOM-04). Root, Viewport, Content and Scrollbar use it; the Thumb does not.
// The shared static arrays make a render allocate nothing.
/// <summary>Creates the data-attribute mapping of a scroll area part whose state carries the overflow fields.</summary>
internal static class ScrollAreaStateMapping
{
    private static readonly StateDataAttribute[] s_hasOverflowX = [new(ScrollAreaRootDataAttributes.HasOverflowX, string.Empty)];
    private static readonly StateDataAttribute[] s_hasOverflowY = [new(ScrollAreaRootDataAttributes.HasOverflowY, string.Empty)];
    private static readonly StateDataAttribute[] s_overflowXStart = [new(ScrollAreaRootDataAttributes.OverflowXStart, string.Empty)];
    private static readonly StateDataAttribute[] s_overflowXEnd = [new(ScrollAreaRootDataAttributes.OverflowXEnd, string.Empty)];
    private static readonly StateDataAttribute[] s_overflowYStart = [new(ScrollAreaRootDataAttributes.OverflowYStart, string.Empty)];
    private static readonly StateDataAttribute[] s_overflowYEnd = [new(ScrollAreaRootDataAttributes.OverflowYEnd, string.Empty)];

    /// <summary>Creates the mapping for a state record with the six overflow fields.</summary>
    /// <typeparam name="TState">The type of the part's state record.</typeparam>
    /// <returns>The mapping.</returns>
    public static StateAttributesMapping<TState> Create<TState>() => new StateAttributesMapping<TState>()
        .Map<bool>("HasOverflowX", static value => value ? s_hasOverflowX : null)
        .Map<bool>("HasOverflowY", static value => value ? s_hasOverflowY : null)
        .Map<bool>("OverflowXStart", static value => value ? s_overflowXStart : null)
        .Map<bool>("OverflowXEnd", static value => value ? s_overflowXEnd : null)
        .Map<bool>("OverflowYStart", static value => value ? s_overflowYStart : null)
        .Map<bool>("OverflowYEnd", static value => value ? s_overflowYEnd : null);
}

// ADR 04 Amendments 2026-10-18 (the handover): the consumer's merged style, read where the pipeline left it.
/// <summary>Reads the consumer's style from a part's merged attributes.</summary>
internal static class ScrollAreaStyles
{
    /// <summary>Returns the merged <c>style</c> attribute, which is the consumer's when the part adds none of its own.</summary>
    /// <param name="attributes">The merged attributes.</param>
    /// <returns>The style string, or <see langword="null"/>.</returns>
    public static string? Consumer(AttributeList attributes) =>
        attributes.TryGetValue(MergeProps.StyleAttribute, out AttributeValue style) && style.TryGetValue(out string text) ? text : null;

    /// <summary>Sets or removes the rendered <c>style</c> attribute.</summary>
    /// <param name="attributes">The merged attributes.</param>
    /// <param name="style">The style to render, or <see langword="null"/> for none.</param>
    public static void Set(AttributeList attributes, string? style)
    {
        if (style is null)
        {
            attributes.Remove(MergeProps.StyleAttribute);
        }
        else
        {
            attributes.Set(MergeProps.StyleAttribute, style);
        }
    }
}

// SCR-API-01..03 (scroll-area/root/ScrollAreaRoot.tsx:408-422, normalizeOverflowEdgeThreshold): a number applies to every
// edge, a record per edge with a missing edge 0, each normalized `max(0, value || 0)`, so a negative value and NaN are 0.
/// <summary>The four normalized overflow edge thresholds of a scroll area.</summary>
/// <param name="XStart">The threshold of the inline start edge.</param>
/// <param name="XEnd">The threshold of the inline end edge.</param>
/// <param name="YStart">The threshold of the top edge.</param>
/// <param name="YEnd">The threshold of the bottom edge.</param>
internal readonly record struct ScrollAreaThresholds(double XStart, double XEnd, double YStart, double YEnd)
{
    /// <summary>Normalizes a root's threshold parameter.</summary>
    /// <param name="value">The parameter, or <see langword="null"/> for none.</param>
    /// <returns>The four thresholds, each at least 0.</returns>
    public static ScrollAreaThresholds From(OverflowEdgeThresholdValue? value)
    {
        if (value is { } threshold)
        {
            if (threshold.TryGetValue(out double all))
            {
                double edge = Normalize(all);
                return new ScrollAreaThresholds(edge, edge, edge, edge);
            }

            if (threshold.TryGetValue(out ScrollAreaOverflowEdges? edges))
            {
                return new ScrollAreaThresholds(Normalize(edges.XStart), Normalize(edges.XEnd), Normalize(edges.YStart), Normalize(edges.YEnd));
            }
        }

        return default;
    }

    // `Math.max(0, value || 0)`: NaN and null are falsy, so they become 0, and Math.Max(0, NaN) would be NaN.
    private static double Normalize(double? value) => value is { } number && !double.IsNaN(number) ? Math.Max(0, number) : 0;
}

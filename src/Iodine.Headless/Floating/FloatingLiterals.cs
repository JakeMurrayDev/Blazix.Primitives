namespace Blazix.Primitives;

// The literals the positioning module reads (Interop/PositioningOptions): Base UI's `Side`, `Align`, `positionMethod`
// and collision-avoidance literals (internals/useAnchorPositioning.ts:66-125, :681). The CompositeLiterals precedent.
/// <summary>Converts the enums of floating positioning into the literals their browser-side behavior understands.</summary>
internal static class FloatingLiterals
{
    /// <summary>Returns the literal of a side.</summary>
    /// <param name="side">The side.</param>
    /// <returns><c>top</c>, <c>bottom</c>, <c>left</c>, <c>right</c>, <c>inline-end</c> or <c>inline-start</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="side"/> is not a defined <see cref="Side"/> value.</exception>
    public static string ToLiteral(Side side)
    {
        return side switch
        {
            Side.Top => "top",
            Side.Bottom => "bottom",
            Side.Left => "left",
            Side.Right => "right",
            Side.InlineEnd => "inline-end",
            Side.InlineStart => "inline-start",
            // The value is formatted as an integer: passing the enum as the actual value would box it.
            _ => throw new ArgumentOutOfRangeException(nameof(side), $"Blazix: `{(int)side}` is not a member of `Side`."),
        };
    }

    /// <summary>Returns the literal of an alignment.</summary>
    /// <param name="align">The alignment.</param>
    /// <returns><c>start</c>, <c>center</c> or <c>end</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="align"/> is not a defined <see cref="Align"/> value.</exception>
    public static string ToLiteral(Align align)
    {
        return align switch
        {
            Align.Start => "start",
            Align.Center => "center",
            Align.End => "end",
            _ => throw new ArgumentOutOfRangeException(nameof(align), $"Blazix: `{(int)align}` is not a member of `Align`."),
        };
    }

    /// <summary>Returns the literal of a position method.</summary>
    /// <param name="method">The position method.</param>
    /// <returns><c>absolute</c> or <c>fixed</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="method"/> is not a defined <see cref="PositionMethod"/> value.</exception>
    public static string ToLiteral(PositionMethod method)
    {
        return method switch
        {
            PositionMethod.Absolute => "absolute",
            PositionMethod.Fixed => "fixed",
            _ => throw new ArgumentOutOfRangeException(nameof(method), $"Blazix: `{(int)method}` is not a member of `PositionMethod`."),
        };
    }

    /// <summary>Returns the literal of a collision-avoidance mode.</summary>
    /// <param name="mode">The mode.</param>
    /// <returns><c>flip</c>, <c>shift</c> or <c>none</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not a defined <see cref="CollisionAvoidanceMode"/> value.</exception>
    public static string ToLiteral(CollisionAvoidanceMode mode)
    {
        return mode switch
        {
            CollisionAvoidanceMode.Flip => "flip",
            CollisionAvoidanceMode.Shift => "shift",
            CollisionAvoidanceMode.None => "none",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), $"Blazix: `{(int)mode}` is not a member of `CollisionAvoidanceMode`."),
        };
    }

    /// <summary>Returns the literal of a transition status.</summary>
    /// <param name="status">The status, or <see langword="null"/> for none.</param>
    /// <returns><c>starting</c>, <c>ending</c>, <c>idle</c>, or <see langword="null"/>.</returns>
    public static string? ToLiteral(TransitionStatus? status)
    {
        return status switch
        {
            TransitionStatus.Starting => "starting",
            TransitionStatus.Ending => "ending",
            TransitionStatus.Idle => "idle",
            _ => null,
        };
    }

    /// <summary>Parses the literal of a side the browser-side behavior reported.</summary>
    /// <param name="literal">The literal.</param>
    /// <returns>The side; <see cref="Side.Bottom"/> for an unknown literal.</returns>
    public static Side ParseSide(string literal)
    {
        return literal switch
        {
            "top" => Side.Top,
            "left" => Side.Left,
            "right" => Side.Right,
            "inline-end" => Side.InlineEnd,
            "inline-start" => Side.InlineStart,
            _ => Side.Bottom,
        };
    }

    /// <summary>Parses the literal of an alignment the browser-side behavior reported.</summary>
    /// <param name="literal">The literal.</param>
    /// <returns>The alignment; <see cref="Align.Center"/> for an unknown literal.</returns>
    public static Align ParseAlign(string literal)
    {
        return literal switch
        {
            "start" => Align.Start,
            "end" => Align.End,
            _ => Align.Center,
        };
    }

    /// <summary>Returns the literal of a fallback axis side.</summary>
    /// <param name="side">The fallback axis side.</param>
    /// <returns><c>start</c>, <c>end</c> or <c>none</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="side"/> is not a defined <see cref="FallbackAxisSide"/> value.</exception>
    public static string ToLiteral(FallbackAxisSide side)
    {
        return side switch
        {
            FallbackAxisSide.Start => "start",
            FallbackAxisSide.End => "end",
            FallbackAxisSide.None => "none",
            _ => throw new ArgumentOutOfRangeException(nameof(side), $"Blazix: `{(int)side}` is not a member of `FallbackAxisSide`."),
        };
    }
}

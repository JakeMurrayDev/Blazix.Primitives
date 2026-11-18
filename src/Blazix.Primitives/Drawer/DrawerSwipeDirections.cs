using Blazix.Primitives.Rendering;

namespace Blazix.Primitives;

// The popup's and the swipe area's data-swipe-direction (drawer/popup/DrawerPopup.tsx, drawer/swipe-area/DrawerSwipeArea.tsx
// stateAttributesMapping): the default projection would name the attribute data-swipedirection, so both parts map the field
// to the cached attribute of each member. The opposite direction is DrawerSwipeArea.tsx's oppositeSwipeDirection.
/// <summary>Provides the drawer parts' swipe direction attribute and the opposite of a direction.</summary>
internal static class DrawerSwipeDirections
{
    private static readonly StateDataAttribute[][] s_attributes =
    [
        [new(DrawerPopupDataAttributes.SwipeDirection, "up")],
        [new(DrawerPopupDataAttributes.SwipeDirection, "down")],
        [new(DrawerPopupDataAttributes.SwipeDirection, "left")],
        [new(DrawerPopupDataAttributes.SwipeDirection, "right")],
    ];

    /// <summary>Projects a direction to its <c>data-swipe-direction</c> attribute.</summary>
    /// <param name="direction">The direction.</param>
    /// <returns>The cached attribute.</returns>
    public static StateDataAttribute[]? Project(SwipeDirection direction) => s_attributes[(int)direction];

    /// <summary>Gets the opposite of a direction on the same axis.</summary>
    /// <param name="direction">The direction.</param>
    /// <returns><see cref="SwipeDirection.Down"/> for <see cref="SwipeDirection.Up"/>, and so on.</returns>
    public static SwipeDirection Opposite(SwipeDirection direction) => direction switch
    {
        SwipeDirection.Up => SwipeDirection.Down,
        SwipeDirection.Down => SwipeDirection.Up,
        SwipeDirection.Left => SwipeDirection.Right,
        _ => SwipeDirection.Left,
    };

    /// <summary>Gets a value indicating whether a direction is horizontal.</summary>
    /// <param name="direction">The direction.</param>
    /// <returns><see langword="true"/> for <see cref="SwipeDirection.Left"/> and <see cref="SwipeDirection.Right"/>.</returns>
    public static bool IsHorizontal(SwipeDirection direction) => direction is SwipeDirection.Left or SwipeDirection.Right;
}

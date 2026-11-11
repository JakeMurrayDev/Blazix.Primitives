namespace Blazix.Primitives.Interop;

// CTX-PTR-01, CTX-PTR-06, CTX-EVT-02, CTX-EVT-04 (ContextMenuTrigger.tsx:56-81): an open request at a point. PointerType is
// `mouse` for a right click (the `contextmenu` event) and `touch` for a long press (the `touchstart` event); the module
// already anchored the session at the point and armed the release.
/// <summary>Represents an open request a context menu trigger reported.</summary>
/// <param name="X">The point's horizontal viewport coordinate.</param>
/// <param name="Y">The point's vertical viewport coordinate.</param>
/// <param name="PointerType"><c>mouse</c> for a right click, <c>touch</c> for a long press.</param>
/// <param name="Event">The <c>contextmenu</c> or <c>touchstart</c> event.</param>
internal sealed record ContextMenuOpenRequest(double X, double Y, string PointerType, EventSnapshot? Event);

// CTX-PTR-05, CTX-EVT-05 (ContextMenuTrigger.tsx:96-127): a release outside the tree after the 500 ms window.
/// <summary>Represents a <c>cancel-open</c> close request a context menu trigger reported.</summary>
/// <param name="Event">The <c>mouseup</c> event.</param>
internal sealed record ContextMenuCancelOpen(EventSnapshot? Event);

/// <summary>Represents one touch of a <see cref="ContextMenuPress"/>.</summary>
/// <param name="Identifier">The touch's identifier.</param>
/// <param name="ClientX">The horizontal viewport coordinate.</param>
/// <param name="ClientY">The vertical viewport coordinate.</param>
/// <param name="ScreenX">The horizontal screen coordinate.</param>
/// <param name="ScreenY">The vertical screen coordinate.</param>
/// <param name="PageX">The horizontal page coordinate.</param>
/// <param name="PageY">The vertical page coordinate.</param>
internal sealed record ContextMenuTouch(long Identifier, double ClientX, double ClientY, double ScreenX, double ScreenY, double PageX, double PageY);

// CTX-API-06; design point 3 (a) of M5 track 7: the event of a consumer handler the Trigger declined, which the module
// hands over before `press` on WebAssembly (`OnPressing`) and reports on Server (`OnPress`). It carries what the
// snapshot does not, so C# rebuilds the handler's MouseEventArgs or TouchEventArgs.
/// <summary>Represents the event of a consumer handler on a context menu trigger.</summary>
/// <param name="ClientX">The horizontal viewport coordinate of a mouse event.</param>
/// <param name="ClientY">The vertical viewport coordinate of a mouse event.</param>
/// <param name="ScreenX">The horizontal screen coordinate of a mouse event.</param>
/// <param name="ScreenY">The vertical screen coordinate of a mouse event.</param>
/// <param name="PageX">The horizontal page coordinate of a mouse event.</param>
/// <param name="PageY">The vertical page coordinate of a mouse event.</param>
/// <param name="OffsetX">The horizontal coordinate in the target's padding box of a mouse event.</param>
/// <param name="OffsetY">The vertical coordinate in the target's padding box of a mouse event.</param>
/// <param name="Button">The button of a mouse event.</param>
/// <param name="Buttons">The buttons held during the event.</param>
/// <param name="Touches">The touches on the surface, for a touch event.</param>
/// <param name="TargetTouches">The touches that started on the target, for a touch event.</param>
/// <param name="ChangedTouches">The touches the event changed, for a touch event.</param>
/// <param name="Event">The event's snapshot.</param>
internal sealed record ContextMenuPress(
    double ClientX,
    double ClientY,
    double ScreenX,
    double ScreenY,
    double PageX,
    double PageY,
    double OffsetX,
    double OffsetY,
    long Button,
    long Buttons,
    IReadOnlyList<ContextMenuTouch>? Touches,
    IReadOnlyList<ContextMenuTouch>? TargetTouches,
    IReadOnlyList<ContextMenuTouch>? ChangedTouches,
    EventSnapshot? Event);

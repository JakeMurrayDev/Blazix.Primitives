namespace Iodine.Headless.Interop;

// DRW-DOM-07, DOM-20 (the Popup's and Backdrop's data-swiping, written by the module and mirrored by C#).
/// <summary>The payload of a change of whether the drawer's popup is being swiped.</summary>
/// <param name="Swiping">A value indicating whether the popup is being swiped.</param>
internal sealed record DrawerSwipingChange(bool Swiping);

// DRW-PTR-14; design point 10 (a) (chosen by the user 2026-11-19): the numbered request the options answer.
/// <summary>The payload of a swipe release that dismisses the drawer, reported where it could not be decided inside the dispatch.</summary>
/// <param name="Request">The number of the request, which the options answer as <see cref="DrawerOptions.AnsweredDismiss"/>.</param>
/// <param name="Event">The snapshot of the release event.</param>
internal sealed record DrawerDismissRequest(int Request, EventSnapshot Event);

// DRW-PTR-11, STA-03; M6 track 8b slice 3 (2026-11-20): the numbered snap point request the options answer, as the
// dismiss request is (design point 10 (a), chosen by the user 2026-11-19).
/// <summary>The payload of a swipe release that moves the drawer to a snap point, reported where it could not be decided inside the dispatch.</summary>
/// <param name="Request">The number of the request, which the options answer as <see cref="DrawerOptions.AnsweredSnap"/>.</param>
/// <param name="Index">The index of the requested snap point in <see cref="DrawerOptions.SnapPoints"/>, or <see langword="null"/> for none.</param>
/// <param name="Event">The snapshot of the release event.</param>
internal sealed record DrawerSnapPointRequest(int Request, int? Index, EventSnapshot Event);

// DRW-STA-10; M6 track 8b slice 3 (2026-11-21): the Popup's measured height, which its Root records as its popup height.
/// <summary>The payload of a measurement of the drawer's popup.</summary>
/// <param name="Height">The popup's height in pixels, borders included; 0 once it is no longer mounted.</param>
internal sealed record DrawerPopupHeightChange(double Height);

// DRW-PTR-28, PTR-30; M6 track 8b slice 4 (2026-11-21): the SwipeArea's numbered open or close request, answered as the
// dismiss request is (design point 10 (a), chosen by the user 2026-11-19).
/// <summary>The payload of a swipe area's request to open or close the drawer.</summary>
/// <param name="Request">The number of the request, which the options answer as <see cref="DrawerOptions.AnsweredOpen"/>.</param>
/// <param name="Open">A value indicating whether the gesture asks to open the drawer; <see langword="false"/> to close it.</param>
/// <param name="Event">The snapshot of the gesture's start event for an open during the drag, else of the release.</param>
internal sealed record DrawerOpenRequest(int Request, bool Open, EventSnapshot Event);

// DRW-DIS-01; M6 track 8c (2026-11-21): the CloseWatcher's close request where the `close-watcher` point could not decide it
// inside the `cancel` event (design point D1 (a), chosen by the user 2026-11-21).
/// <summary>The payload of a close request of the drawer's close watcher, reported where it could not be decided inside the event.</summary>
/// <param name="Request">The number of the request, in the order the module reported them.</param>
/// <param name="Cancelable">Whether the browser let the module prevent the event; when not, the close cannot be canceled.</param>
/// <param name="Event">The snapshot of the watcher's <c>cancel</c> event.</param>
internal sealed record DrawerCloseWatcherRequest(int Request, bool Cancelable, EventSnapshot Event);

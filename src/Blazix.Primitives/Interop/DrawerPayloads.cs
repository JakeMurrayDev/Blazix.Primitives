namespace Blazix.Primitives.Interop;

// DRW-DOM-07, DOM-20 (the Popup's and Backdrop's data-swiping, written by the module and mirrored by C#).
/// <summary>The payload of a change of whether the drawer's popup is being swiped.</summary>
/// <param name="Swiping">A value indicating whether the popup is being swiped.</param>
internal sealed record DrawerSwipingChange(bool Swiping);

// DRW-PTR-14; design point 10 (a) (chosen by the user 2026-11-19): the numbered request the options answer.
/// <summary>The payload of a swipe release that dismisses the drawer, reported where it could not be decided inside the dispatch.</summary>
/// <param name="Request">The number of the request, which the options answer as <see cref="DrawerOptions.AnsweredDismiss"/>.</param>
/// <param name="Event">The snapshot of the release event.</param>
internal sealed record DrawerDismissRequest(int Request, EventSnapshot Event);

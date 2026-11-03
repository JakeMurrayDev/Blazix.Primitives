namespace Blazix.Primitives.Interop;

// NAV-PTR-18, NAV-STA-06; design point 4 of M5 track 5 (a report with no veto, sent 2026-11-04, the user's): a Trigger's
// mouseenter, click or open key. Reason is the request's literal (`trigger-hover`, `trigger-press`, `list-navigation`),
// or null when the activation requests nothing (the menu is closed); Direction is the literal of the new activation
// direction (`left`, `right`, `up`, `down`), or null to keep the current one. On WebAssembly the module invokes the same
// method synchronously, so the request renders inside the dispatch (Base UI's flushSync).
/// <summary>Represents the activation a trigger reported.</summary>
/// <param name="Reason">The literal of the request's reason, or <see langword="null"/> when nothing is requested.</param>
/// <param name="Direction">The literal of the new activation direction, or <see langword="null"/> to keep the current one.</param>
/// <param name="Event">The event the activation came from.</param>
internal sealed record NavigationMenuActivation(string? Reason, string? Direction, EventSnapshot? Event);

// NAV-FOC-10, NAV-FOC-11, NAV-KEY-11, NAV-KEY-13: a blur or a guard that leaves the menu, a `focus-out` close request; the
// module sends one at a time until the Root's Answered moves (ADR 04 Amendments 2026-10-28, widened 2026-10-29).
/// <summary>Represents a focus-out close a trigger, a link or a guard reported.</summary>
/// <param name="Event">The focus event.</param>
internal sealed record NavigationMenuFocusOut(EventSnapshot? Event);

// NAV-STA-09, NAV-FOC-07, NAV-KEY-12: a change the module made to the Root's viewportInert, which C# renders as the inline
// Viewport's `inert`.
/// <summary>Represents a change of the viewport's inert flag the module reported.</summary>
/// <param name="Inert">The new value.</param>
internal sealed record NavigationMenuViewportInertChange(bool Inert);

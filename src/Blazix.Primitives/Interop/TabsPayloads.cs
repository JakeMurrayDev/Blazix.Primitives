namespace Blazix.Primitives.Interop;

// ADR 04 Amendments 2026-10-26 (chosen by the user 2026-10-26): on Server the module reports the Tab's id, the
// activationDirection and the event, and C# runs the request a round trip later; on WebAssembly the same selection is
// handed to the twin synchronously just before the `select` decision (design point 6 of the Tabs track, pending the user).
// TAB-PTR-01, TAB-KEY-10, TAB-EVT-02, TAB-STA-15.
/// <summary>The payload of an activation: the tab the gesture activates and the direction measured in its dispatch.</summary>
/// <param name="TabId">The id of the tab's element.</param>
/// <param name="Direction">The activation direction's literal: <c>left</c>, <c>right</c>, <c>up</c>, <c>down</c> or <c>none</c>.</param>
/// <param name="Event">The snapshot of the <c>click</c> or <c>focusin</c>, or <see langword="null"/> when handed over inside the dispatch.</param>
internal sealed record TabsSelection(string TabId, string Direction, EventSnapshot? Event);

// TAB-PTR-09 (the consumer's onclick and onfocus run for every gesture); design point 5 of the Tabs track (the report only
// while a consumer handler exists), pending the user.
/// <summary>The payload of a click or focus on a tab that the module does not activate.</summary>
/// <param name="TabId">The id of the tab's element.</param>
/// <param name="Event">The snapshot of the <c>click</c> or <c>focusin</c>.</param>
internal sealed record TabsTabEvent(string TabId, EventSnapshot Event);

// ADR 04 Amendments 2026-10-26: data-activation-direction is the module's from attach and C# mirrors it (RND-STA-01 row of
// ADR 01), once per change (ADR 16 rule 6). TAB-STA-14..18, TAB-DOM-02.
/// <summary>The payload of a change of the activation direction the module wrote on the parts.</summary>
/// <param name="Direction">The new direction's literal.</param>
internal sealed record TabsDirectionChange(string Direction);

// ADR 04 §2 (the Tabs.Indicator row); design point 2 of the Tabs track (one report per change of the six numbers, null
// while hidden or unmeasured), pending the user. TAB-STA-20, TAB-DOM-24.
/// <summary>The payload of a change of an indicator's measurement.</summary>
/// <param name="Position">The active tab's offsets, or <see langword="null"/> while no active tab is measurable.</param>
/// <param name="Size">The active tab's size, or <see langword="null"/> while no active tab is measurable.</param>
internal sealed record TabsIndicatorMeasurement(TabsTabPosition? Position, TabsTabSize? Size);

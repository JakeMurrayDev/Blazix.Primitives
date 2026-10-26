namespace Blazix.Primitives;

// TAB-STA-20, TAB-DOM-24; tabs/tab/TabsTab.tsx:217-222 (Tabs.Tab.Position). ADR 08 §1/§3 (companion types). Design
// point 2 of the Tabs track (a record class, mirrored once per change of the measurement) was settled by the
// orchestrator 2026-10-26.
/// <summary>
/// Represents the offsets of the active tab from the edges of its tab list, in CSS pixels, as a tab indicator measures
/// them.
/// </summary>
/// <param name="Left">The distance from the list's left edge to the tab's left edge.</param>
/// <param name="Right">The distance from the tab's right edge to the right edge of the list's scrollable content.</param>
/// <param name="Top">The distance from the list's top edge to the tab's top edge.</param>
/// <param name="Bottom">The distance from the tab's bottom edge to the bottom edge of the list's scrollable content.</param>
public sealed record TabsTabPosition(double Left, double Right, double Top, double Bottom);

namespace Blazix.Primitives;

// TAB-STA-20, TAB-DOM-24/25; tabs/tab/TabsTab.tsx:224-227 (Tabs.Tab.Size). ADR 08 §1/§3 (companion types). Design point 2
// of the Tabs track (a record class, mirrored once per change of the measurement) is pending the user.
/// <summary>Represents the size of the active tab, in CSS pixels, as a tab indicator measures it.</summary>
/// <param name="Width">The tab's CSS width, which ancestor transforms do not affect.</param>
/// <param name="Height">The tab's CSS height, which ancestor transforms do not affect.</param>
public sealed record TabsTabSize(double Width, double Height);

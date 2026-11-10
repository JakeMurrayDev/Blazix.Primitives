namespace Blazix.Primitives.Interop;

// ADR 04 §1 (the `navigation-menu` component module) and Amendments 2026-10-18 and 2026-11-03 (4) (chosen by the user
// 2026-11-03): one handle per part that owns a NavigationMenuTwin, joined to its Root's menu through the Root element,
// which every part but the Root's sends as its first reference (RootRef). Values never cross: the Root's options carry
// the literals and flags the module reads, the Trigger's whether its Item renders a Content, the Viewport's whether it is
// inside a Positioner; the module reads the parts' state from the attributes C# renders (data-open, data-popup-open,
// data-disabled). Fields a part does not use are left null and so not sent. Handlers read the options of the latest update.
/// <summary>Holds the options of the <c>navigation-menu</c> module's handle.</summary>
internal sealed record NavigationMenuOptions : TwinOptions
{
    /// <summary>Gets the part the handle is attached to, one of the <see cref="NavigationMenuTwin"/> part names.</summary>
    public string Part { get; init; } = NavigationMenuTwin.RootPart;

    /// <summary>Gets the index of the Root element in the references; every part but the Root sends it.</summary>
    public int? RootRef { get; init; }

    /// <summary>Gets the axis of the top-level list, <c>horizontal</c> or <c>vertical</c>; the Root's.</summary>
    public string? Orientation { get; init; }

    /// <summary>Gets a value indicating whether the reading direction is right to left; the Root's.</summary>
    public bool? Rtl { get; init; }

    /// <summary>Gets a value indicating whether the Root is nested in another menu; the Root's.</summary>
    public bool? Nested { get; init; }

    /// <summary>Gets a value indicating whether the popup is mounted, as the store last committed it; the Root's.</summary>
    public bool? Mounted { get; init; }

    /// <summary>Gets the literal of the latest close reason the Root recorded, or <see langword="null"/>; the Root's.</summary>
    public string? CloseReason { get; init; }

    /// <summary>Gets the Root's interactions session token, whose tree the blur checks read; the Root's.</summary>
    public string? SessionToken { get; init; }

    /// <summary>Gets the number of focus-out reports the Root has answered, committed or canceled; the Root's.</summary>
    public int? Answered { get; init; }

    /// <summary>Gets a value indicating whether the Trigger's item renders a content; <see langword="false"/> only when it renders none.</summary>
    public bool? HasContent { get; init; }

    /// <summary>Gets a value indicating whether the Viewport is inside a positioner; the Viewport's.</summary>
    public bool? HasPositioner { get; init; }
}

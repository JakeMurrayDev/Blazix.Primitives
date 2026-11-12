namespace Blazix.Primitives.Interop;

// M5 track 12's design point 1 (a), settled by the orchestrator 2026-11-11: the `menu-filter` component module, one handle per part that
// owns a MenuFilterTwin (the Input, the Trigger's relay, the List and the Popup), joined to its menu by MenuKey, a key C#
// generates per filterable root and never renders. Fields a part does not use are left null and so not sent; handlers read
// the options of the latest update.
/// <summary>Holds the options of the <c>menu-filter</c> module's handle.</summary>
internal sealed record MenuFilterOptions : TwinOptions
{
    /// <summary>Gets the part the handle is attached to, one of the <c>MenuFilterTwin</c> part names: <c>input</c>, <c>trigger</c>, <c>list</c>, <c>popup</c> or <c>clear</c>.</summary>
    public string Part { get; init; } = "input";

    /// <summary>Gets the key that joins the handles of one filterable menu.</summary>
    public string? MenuKey { get; init; }

    /// <summary>Gets the committed query C# last sent; the Input's.</summary>
    public string? Value { get; init; }

    /// <summary>Gets the count of committed values C# has sent, which tells a new commit from a repeated one; the Input's.</summary>
    public int? ValueRevision { get; init; }

    /// <summary>Gets a value indicating whether the menu is open; the Input's and the Trigger's.</summary>
    public bool? Open { get; init; }

    /// <summary>Gets a value indicating whether the menu opened from the keyboard, which seeds the input's focus ring; the Input's.</summary>
    public bool? KeyboardOpen { get; init; }

    /// <summary>Gets the axis of the menu's list, <c>horizontal</c> or <c>vertical</c>; the Input's.</summary>
    public string? Orientation { get; init; }

    /// <summary>Gets a value indicating whether the reading direction is right to left; the Input's.</summary>
    public bool? Rtl { get; init; }

    /// <summary>Gets a value indicating whether the menu is a submenu, whose close key returns to its parent; the Input's.</summary>
    public bool? Submenu { get; init; }

    /// <summary>Gets a value indicating whether the menu belongs to a menubar, where the Trigger's relay is off; the Trigger's.</summary>
    public bool? InBar { get; init; }

    /// <summary>Gets a value indicating whether C# holds a consumer key handler that the <c>navigate</c> point asks; the Input's and the Trigger's.</summary>
    public bool? DecidesKeyDown { get; init; }

    /// <summary>Gets the ids of the registered items whose rendered text C# needs; the List's.</summary>
    public IReadOnlyList<string>? TextIds { get; init; }

    /// <summary>Gets the automatic highlight mode an item-set change follows, <c>true</c> or <c>always</c>, or <see langword="null"/> for none; the List's.</summary>
    public string? AutoHighlight { get; init; }
}

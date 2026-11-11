using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// A test-only menubar parent that records the open changes its menus report: the titles confirming the Menu gaps the bar
/// needs closed (design point 2 of M5 track 8) run against it on <c>main</c>, before the <c>Menubar</c> part exists.
/// </summary>
internal sealed class MenubarRecordingParent : IMenuParentContext
{
    /// <summary>Gets the root id the bar gives its menus.</summary>
    public const string BarRootId = "bar-root";

    /// <summary>Gets the reported open changes, in order.</summary>
    public List<(MenuRootContext Menu, bool Open, EventReason Reason)> Changes { get; } = [];

    /// <inheritdoc />
    public MenuParentKind Kind => MenuParentKind.Menubar;

    /// <inheritdoc />
    public IMenuParentContext? Enclosing => null;

    /// <inheritdoc />
    public bool Modal => true;

    /// <inheritdoc />
    public bool Disabled => false;

    /// <inheritdoc />
    public string? RootId => BarRootId;

    /// <inheritdoc />
    public Blazix.Primitives.Orientation Orientation => Blazix.Primitives.Orientation.Horizontal;

    /// <inheritdoc />
    public bool HasSubmenuOpen => false;

    /// <inheritdoc />
    public string? CutoutElementId => null;

    /// <inheritdoc />
    public ElementReference? RelayTarget => null;

    /// <inheritdoc />
    public bool KeepOpenOnCrossOrientationKey => false;

    /// <inheritdoc />
    public int OutsidePressGrace => 0;

    /// <inheritdoc />
    public void SetBackdropElement(ElementReference? element)
    {
    }

    /// <inheritdoc />
    public void SetInternalBackdropElement(ElementReference? element)
    {
    }

    /// <inheritdoc />
    public void OnMenuOpenChange(MenuRootContext menu, bool open, EventReason reason) => Changes.Add((menu, open, reason));

    /// <inheritdoc />
    public IDisposable Subscribe(Action changed) => new Unsubscriber();

    private sealed class Unsubscriber : IDisposable
    {
        public void Dispose()
        {
        }
    }
}

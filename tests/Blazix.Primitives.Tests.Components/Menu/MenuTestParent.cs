using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// A test-only menu parent (MNU-STR-19, the brief's "proven at L2 on test-only parents"): what <c>Menubar</c> (track 8) and
/// <c>ContextMenuRoot</c> (track 7) will cascade, with fixed values.
/// </summary>
/// <param name="kind">The parent kind.</param>
internal sealed class MenuTestParent(MenuParentKind kind) : IMenuParentContext
{
    /// <inheritdoc />
    public MenuParentKind Kind => kind;

    /// <inheritdoc />
    public IMenuParentContext? Enclosing => null;

    /// <inheritdoc />
    public bool Modal => true;

    /// <inheritdoc />
    public bool Disabled => false;

    /// <inheritdoc />
    public string? RootId => null;

    /// <inheritdoc />
    public Blazix.Primitives.Orientation Orientation { get; init; } = Blazix.Primitives.Orientation.Horizontal;

    /// <inheritdoc />
    public bool HasSubmenuOpen => false;

    /// <inheritdoc />
    public string? CutoutElementId => null;

    /// <inheritdoc />
    public ElementReference? RelayTarget => null;

    /// <inheritdoc />
    public bool KeepOpenOnCrossOrientationKey => kind == MenuParentKind.ContextMenu;

    /// <inheritdoc />
    public int OutsidePressGrace => kind == MenuParentKind.ContextMenu ? 500 : 0;

    /// <inheritdoc />
    public void SetBackdropElement(ElementReference? element)
    {
    }

    /// <inheritdoc />
    public void SetInternalBackdropElement(ElementReference? element)
    {
    }

    /// <inheritdoc />
    public void OnMenuOpenChange(MenuRootContext menu, bool open, EventReason reason)
    {
    }

    /// <inheritdoc />
    public IDisposable Subscribe(Action changed) => new Unsubscriber();

    private sealed class Unsubscriber : IDisposable
    {
        public void Dispose()
        {
        }
    }
}

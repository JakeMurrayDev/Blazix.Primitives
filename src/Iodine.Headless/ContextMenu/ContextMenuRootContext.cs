using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// Base UI's ContextMenuRootContext (context-menu/root/ContextMenuRootContext.ts:5-17) as Menu's parent context (MNU-STR-19,
// IMenuParentContext; decision 12 of the M5 plan, ADR 08 Amendments 2026-11-03 (3), chosen by the user 2026-11-03). Its
// values are fixed (CTX-STA-05, STA-10, DOM-04, KEY-07 as ADR 04 Amendments 2026-11-04 settled it, DIS-01): what Menu's
// behaviours read for a context menu parent. Enclosing is the parent context cascaded above the root (design point 8 of M5
// track 6, settled by the orchestrator 2026-11-04), so a context menu inside a menubar resolves to the bar (CTX §16 Q15,
// replicated). The backdrops Menu's parts record go to the `context-menu` module through the root's handle (design point 2
// (a) of M5 track 7): ElementsChanged asks the root to send them. The pointer anchor, the release flag and the initial
// cursor point are the module's, in the menu's interactions session (design point 1 (a)). The Trigger reads the menu and
// the session token from here.
/// <summary>The parent context a context menu cascades to the menu it renders and to its trigger.</summary>
internal sealed class ContextMenuRootContext : IMenuParentContext
{
    // MNU-DIS-10 (MenuRoot.tsx:322-333).
    private const int Grace = 500;

    /// <summary>Occurs when the backdrop or the internal backdrop changed.</summary>
    public event Action? ElementsChanged;

    /// <summary>Initializes a new instance of the <see cref="ContextMenuRootContext"/> class.</summary>
    /// <param name="rootId">The tree's root id, which every popup of the context menu renders as <c>data-rootownerid</c>.</param>
    public ContextMenuRootContext(string rootId)
    {
        RootId = rootId;
    }

    /// <inheritdoc />
    public MenuParentKind Kind => MenuParentKind.ContextMenu;

    /// <inheritdoc />
    public IMenuParentContext? Enclosing { get; set; }

    /// <inheritdoc />
    public bool Modal => true;

    /// <inheritdoc />
    public bool Disabled => false;

    /// <inheritdoc />
    public string? RootId { get; }

    /// <inheritdoc />
    public Orientation Orientation => Orientation.Vertical;

    /// <inheritdoc />
    public bool HasSubmenuOpen => false;

    /// <inheritdoc />
    public string? CutoutElementId => null;

    /// <inheritdoc />
    public ElementReference? RelayTarget => null;

    /// <inheritdoc />
    public bool KeepOpenOnCrossOrientationKey => true;

    /// <inheritdoc />
    public int OutsidePressGrace => Grace;

    /// <summary>Gets the consumer Backdrop's element.</summary>
    /// <value>The element, or <see langword="null"/> while no Backdrop is rendered.</value>
    public ElementReference? BackdropElement { get; private set; }

    /// <summary>Gets the internal backdrop's element.</summary>
    /// <value>The element, or <see langword="null"/> while the root Positioner renders none.</value>
    public ElementReference? InternalBackdropElement { get; private set; }

    /// <summary>Gets or sets the context of the menu the context menu renders.</summary>
    /// <value>Set by the root at its first parameter set.</value>
    public MenuRootContext? Menu { get; set; }

    /// <inheritdoc />
    public void SetBackdropElement(ElementReference? element)
    {
        if (!Same(BackdropElement, element))
        {
            BackdropElement = element;
            ElementsChanged?.Invoke();
        }
    }

    /// <inheritdoc />
    public void SetInternalBackdropElement(ElementReference? element)
    {
        if (!Same(InternalBackdropElement, element))
        {
            InternalBackdropElement = element;
            ElementsChanged?.Invoke();
        }
    }

    /// <inheritdoc />
    /// <remarks>A context menu has nothing to do when its menu's open state changes: it has no sibling menus.</remarks>
    public void OnMenuOpenChange(MenuRootContext menu, bool open, EventReason reason)
    {
        _ = menu;
        _ = open;
        _ = reason;
    }

    /// <inheritdoc />
    /// <remarks>The values a context menu's parent context exposes never change, so a subscriber is never called.</remarks>
    public IDisposable Subscribe(Action changed)
    {
        _ = changed;
        return NoSubscription.Instance;
    }

    private static bool Same(ElementReference? a, ElementReference? b) =>
        a is null ? b is null : b is not null && string.Equals(a.Value.Id, b.Value.Id, StringComparison.Ordinal);

    /// <summary>The subscription to values that never change.</summary>
    private sealed class NoSubscription : IDisposable
    {
        /// <summary>Gets the one instance.</summary>
        public static NoSubscription Instance { get; } = new();

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}

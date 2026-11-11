using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// MBR §3.1, §4 (menubar/MenubarContext.ts:19-30, Menubar.tsx:46-128), M5 track 8: the bar's values as Menu's parent
// context (decision 12 of the M5 plan; ADR 08 Amendments 2026-11-03 (3)), cascaded fixed with a subscription (ADR 16
// rule 9).
// - The flag (MBR-STA-02, STA-03; Menubar.tsx:102-118): a bar menu's open sets it; its close clears it unless the reason
//   is `sibling-open` or `list-navigation`. Only a menu whose parent is the bar reports, so submenus never touch it.
// - The sibling close (MenuPositioner.tsx:134-154): on an open, every other bar menu the bar heard open is asked to close
//   with `sibling-open` and no event; a consumer's veto leaves two open.
// - The cut-out is the bar's own element, by its id (Base UI's `contentElement`); the relay target is the bar's element.
//   No backdrop is kept: a consumer Backdrop finds a context menu past the bar with `MenuParent.Find`.
// It is also the bar's floating node: a detached Root held by a bar trigger names its session as its parent.
/// <summary>The values of a menubar that its menus read as their parent.</summary>
internal sealed class MenubarContext : IMenuParentContext, IFloatingNodeContext
{
    private readonly List<Action> listeners = [];
    private readonly List<MenuRootContext> openMenus = [];
    private readonly Action flagChanged;
    private readonly Func<Exception, Task> dispatchException;

    /// <summary>Initializes a new instance of the <see cref="MenubarContext"/> class.</summary>
    /// <param name="flagChanged">Called after <see cref="HasSubmenuOpen"/> changed, before the subscribers.</param>
    /// <param name="dispatchException">Reports an exception of a sibling's close request to the renderer.</param>
    public MenubarContext(Action flagChanged, Func<Exception, Task> dispatchException)
    {
        this.flagChanged = flagChanged;
        this.dispatchException = dispatchException;
    }

    /// <inheritdoc />
    public MenuParentKind Kind => MenuParentKind.Menubar;

    /// <inheritdoc />
    public IMenuParentContext? Enclosing { get; set; }

    /// <inheritdoc />
    public bool Modal { get; private set; } = true;

    /// <inheritdoc />
    public bool Disabled { get; private set; }

    /// <inheritdoc />
    public string? RootId { get; private set; }

    /// <inheritdoc />
    public Orientation Orientation { get; private set; } = Orientation.Horizontal;

    /// <inheritdoc />
    public bool HasSubmenuOpen { get; private set; }

    /// <inheritdoc />
    public string? CutoutElementId => RootId;

    /// <inheritdoc />
    public ElementReference? RelayTarget { get; private set; }

    /// <inheritdoc />
    public bool KeepOpenOnCrossOrientationKey => false;

    /// <inheritdoc />
    public int OutsidePressGrace => 0;

    /// <inheritdoc />
    public string? SessionToken { get; init; }

    // MBR-EDGE-01 (menubar/MenubarContext.ts:24-30): the required read throws Base UI's message in the port's form. Menu's
    // parts read the context optionally and never call it.
    /// <summary>Returns the nearest menubar context, or throws when there is none.</summary>
    /// <param name="parent">The cascaded parent context, or <see langword="null"/>.</param>
    /// <returns>The menubar context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="parent"/> is not a menubar's context.</exception>
    public static MenubarContext Require(IMenuParentContext? parent) =>
        parent as MenubarContext ?? throw new InvalidOperationException(
            "Blazix: The `Menubar` context is missing. Menubar parts must be placed within `<Menubar>`.");

    /// <summary>Writes the bar's values from its parameters, notifying the subscribers when one changed.</summary>
    /// <param name="modal">The bar's <c>Modal</c>.</param>
    /// <param name="disabled">The bar's <c>Disabled</c>.</param>
    /// <param name="orientation">The bar's <c>Orientation</c>.</param>
    /// <param name="rootId">The bar's resolved id.</param>
    public void SetValues(bool modal, bool disabled, Orientation orientation, string? rootId)
    {
        if (Modal == modal && Disabled == disabled && Orientation == orientation && string.Equals(RootId, rootId, StringComparison.Ordinal))
        {
            return;
        }

        Modal = modal;
        Disabled = disabled;
        Orientation = orientation;
        RootId = rootId;
        Notify();
    }

    /// <summary>Writes the bar's element, notifying the subscribers when it changed.</summary>
    /// <param name="element">The bar's element, or <see langword="null"/>.</param>
    public void SetElement(ElementReference? element)
    {
        if (RelayTarget?.Id == element?.Id)
        {
            return;
        }

        RelayTarget = element;
        Notify();
    }

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
        if (open)
        {
            if (!openMenus.Contains(menu))
            {
                openMenus.Add(menu);
            }

            SetFlag(true);
            foreach (MenuRootContext other in openMenus.ToArray())
            {
                if (!ReferenceEquals(other, menu))
                {
                    _ = CloseSiblingAsync(other);
                }
            }
        }
        else
        {
            openMenus.Remove(menu);
            if (reason is not (EventReason.SiblingOpen or EventReason.ListNavigation))
            {
                SetFlag(false);
            }
        }
    }

    /// <inheritdoc />
    public IDisposable Subscribe(Action changed)
    {
        listeners.Add(changed);
        return new Subscription(listeners, changed);
    }

    private void SetFlag(bool value)
    {
        if (HasSubmenuOpen == value)
        {
            return;
        }

        HasSubmenuOpen = value;
        flagChanged();
        Notify();
    }

    private async Task CloseSiblingAsync(MenuRootContext menu)
    {
        try
        {
            await menu.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.SiblingOpen));
        }
        catch (Exception exception)
        {
            await dispatchException(exception);
        }
    }

    private void Notify()
    {
        foreach (Action listener in listeners.ToArray())
        {
            listener();
        }
    }

    private sealed class Subscription(List<Action> listeners, Action listener) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose() => listeners.Remove(listener);
    }
}

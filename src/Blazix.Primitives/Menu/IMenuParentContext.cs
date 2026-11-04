using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// MNU-STR-19, MNU §4 (menu/root/MenuRoot.tsx:94-122, 959-979; menubar/MenubarContext.ts:5-15;
// context-menu/root/ContextMenuRootContext.ts:5-17). Decision 12 of the M5 plan and ADR 08 Amendments 2026-11-03 (3)
// (chosen by the user 2026-11-03): ContextMenu and Menubar adopt Menu's behaviours, so the branches they switch on are
// coded here as data, against this one context, which Menubar (track 8) and ContextMenuRoot (track 7) implement and
// cascade with IsFixed. A submenu's parent (Base UI's `{ type: 'menu', store }`) is the enclosing MenuRootContext, not
// this interface. Design point 8 of M5 track 6 (pending the user): one cascade type, so the nearest of a bar and a
// context menu wins where Base UI checks the bar first (a dated note on MNU-STR-19).
/// <summary>Specifies the kind of parent a menu has, which picks its defaults and its branches.</summary>
internal enum MenuParentKind
{
    /// <summary>A top-level menu (Base UI's <c>{ type: undefined }</c>).</summary>
    None,

    /// <summary>A submenu inside another menu.</summary>
    Menu,

    /// <summary>One of a menubar's menus.</summary>
    Menubar,

    /// <summary>A context menu's own menu.</summary>
    ContextMenu,
}

/// <summary>
/// Defines the context a menubar or a context menu shares with the menu roots below it, read by Menu's behaviors for
/// the branches of their parent kind.
/// </summary>
internal interface IMenuParentContext
{
    /// <summary>Gets the kind of parent this context is.</summary>
    /// <value><see cref="MenuParentKind.Menubar"/> or <see cref="MenuParentKind.ContextMenu"/>.</value>
    MenuParentKind Kind { get; }

    // MNU-DIS-09, MNU-STR-17 (MenuPositioner.tsx:245, 264-268): the bar's own modal decides the scroll lock and the
    // internal backdrop of its menus.
    /// <summary>Gets a value indicating whether the parent is modal.</summary>
    /// <value>The menubar's <c>Modal</c>; <see langword="true"/> for a context menu.</value>
    bool Modal { get; }

    // MNU-STA-07 (MenuStore.ts:77-80).
    /// <summary>Gets a value indicating whether the parent is disabled, which disables its menus too.</summary>
    /// <value>The menubar's <c>Disabled</c>; <see langword="false"/> for a context menu.</value>
    bool Disabled { get; }

    // MenuStore.ts:91-97: the tree's root id, rendered as each popup's data-rootownerid.
    /// <summary>Gets the id of the menu tree's root.</summary>
    /// <value>The parent's generated id.</value>
    string? RootId { get; }

    // MNU-KEY-01, MNU-POS-02 (MenuRoot.tsx:522; MenuPositioner.tsx:98-102).
    /// <summary>Gets the parent's orientation.</summary>
    /// <value>The menubar's <c>Orientation</c>; <see cref="Orientation.Vertical"/> for a context menu.</value>
    Orientation Orientation { get; }

    // MNU-API-19 (MenuTrigger.tsx:173-180).
    /// <summary>Gets a value indicating whether one of the parent's menus is open.</summary>
    /// <value>The menubar's flag; <see langword="false"/> for a context menu.</value>
    bool HasSubmenuOpen { get; }

    // MNU-DOM-25 (MenuPositioner.tsx:272-276): the internal backdrop's cut-out is the bar's content element.
    /// <summary>Gets the id of the element the internal backdrop leaves uncovered.</summary>
    /// <value>The menubar's content element's id; <see langword="null"/> for a context menu.</value>
    string? CutoutElementId { get; }

    // MNU-KEY-21 (MenuRoot.tsx:692-700): a key the menu does not stop goes on to the bar's composite root.
    /// <summary>Gets the element a keydown the menu does not stop is relayed to.</summary>
    /// <value>The menubar's root element; <see langword="null"/> for a context menu.</value>
    ElementReference? RelayTarget { get; }

    // CTX-KEY-07 as the port settled it (ADR 04 Amendments 2026-11-04).
    /// <summary>Gets a value indicating whether the menu's cross-orientation close key leaves it open.</summary>
    /// <value><see langword="true"/> for a context menu's own menu.</value>
    bool KeepOpenOnCrossOrientationKey { get; }

    // MNU-DIS-10 (MenuRoot.tsx:317-332, 505-509).
    /// <summary>Gets the time after an open during which outside presses do not close the menu.</summary>
    /// <value>500 milliseconds for a context menu; 0 for a menubar.</value>
    int OutsidePressGrace { get; }

    // MNU-DOM-10, MNU-EDGE-15 (MenuBackdrop.tsx:29, 37-39).
    /// <summary>Records the menu's backdrop, which a context menu checks its own presses against.</summary>
    /// <param name="element">The backdrop's element, or <see langword="null"/> when it went away.</param>
    void SetBackdropElement(ElementReference? element);

    // MenuPositioner.tsx:283.
    /// <summary>Records the menu's internal backdrop.</summary>
    /// <param name="element">The internal backdrop's element, or <see langword="null"/> when it went away.</param>
    void SetInternalBackdropElement(ElementReference? element);

    // Menubar.tsx:120's `menuopenchange` listener as a C# call from the menu's commit path (plan §Settled, sheet C Q7):
    // the bar's HasSubmenuOpen and its sibling close.
    /// <summary>Tells the parent that one of its menus committed an open change.</summary>
    /// <param name="menu">The menu's context.</param>
    /// <param name="open">The committed open state.</param>
    /// <param name="reason">The change's reason.</param>
    void OnMenuOpenChange(MenuRootContext menu, bool open, EventReason reason);

    /// <summary>Subscribes to changes of the parent's values.</summary>
    /// <param name="changed">Called after one of the values changed.</param>
    /// <returns>The subscription; disposing it unsubscribes.</returns>
    IDisposable Subscribe(Action changed);
}

/// <summary>Represents the resolved parent of a menu.</summary>
/// <param name="Kind">The parent's kind.</param>
/// <param name="Menu">The enclosing menu's context when <paramref name="Kind"/> is <see cref="MenuParentKind.Menu"/>.</param>
/// <param name="Context">The menubar's or context menu's context, for those kinds.</param>
internal readonly record struct MenuParent(MenuParentKind Kind, MenuRootContext? Menu, IMenuParentContext? Context)
{
    /// <summary>Gets the parent of a top-level menu.</summary>
    public static MenuParent None => default;

    // MenuRoot.tsx:94-122.
    /// <summary>Resolves a menu's parent from what encloses its root.</summary>
    /// <param name="isSubmenu">Whether the root is a submenu's.</param>
    /// <param name="enclosingMenu">The nearest enclosing menu's context, or <see langword="null"/>.</param>
    /// <param name="parent">The nearest enclosing menubar's or context menu's context, or <see langword="null"/>.</param>
    /// <returns>A menu parent for a submenu inside a menu; else a menubar parent; else a context-menu parent with no enclosing menu; else none.</returns>
    public static MenuParent Resolve(bool isSubmenu, MenuRootContext? enclosingMenu, IMenuParentContext? parent) =>
        throw new NotImplementedException();
}

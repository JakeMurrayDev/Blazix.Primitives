using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// MNU §11 (MenuRootContext); menu/root/MenuRootContext.ts. The Root's behaviour cascades it fixed (ADR 16 rule 9) beside its
// store. Only the Root and the Trigger are generic (ADR 08 Amendments 2026-10-27, issue 116's finding), so every other part
// reads this non-generic base (the PopoverRootContext precedent): the state through IMenuState, a selector subscription,
// the writes the parts make, and the tree's C# work (the `item-press` and `cancel-open` closes, the parent-close cascade).
// A submenu's parent is its enclosing menu's context (MenuParent.Menu).
/// <summary>The state a menu root shares with the parts in its subtree, independent of its payload type.</summary>
internal abstract class MenuRootContext
{
    /// <summary>Gets the menu's current state.</summary>
    public abstract IMenuState State { get; }

    /// <summary>Gets the session token of the menu, which its browser-side behaviors join; never rendered.</summary>
    public abstract string? SessionToken { get; }

    /// <summary>Gets the root's logger, or <see langword="null"/>.</summary>
    public abstract ILogger? Logger { get; }

    /// <summary>Gets the name of the family whose root created the context, such as <c>Menu</c> or <c>ContextMenu</c>.</summary>
    public abstract string FamilyName { get; }

    /// <summary>Gets the menu's resolved parent.</summary>
    public abstract MenuParent Parent { get; }

    /// <summary>Gets a value indicating whether the menu keeps real focus on an element of its own and highlights by <c>aria-activedescendant</c>.</summary>
    /// <value><see langword="false"/> for a plain menu; a filterable menu's (track 12).</value>
    public abstract bool Virtual { get; }

    /// <summary>Gets a value indicating whether the root was given a handle.</summary>
    public abstract bool HasHandle { get; }

    /// <summary>Gets the list navigation's context, which the trigger and the popup's list handles join.</summary>
    public abstract ListNavigationContext List { get; }

    /// <summary>Gets the rendered id of the highlighted item, the list handle's mirror.</summary>
    public abstract string? HighlightedItemId { get; }

    /// <summary>Gets the root's <c>Orientation</c>.</summary>
    public abstract Orientation Orientation { get; }

    /// <summary>Throws the error of a menu part rendered outside a menu root.</summary>
    /// <param name="context">The cascaded context, or <see langword="null"/>.</param>
    /// <param name="familyName">The part's family, which names the root in the message.</param>
    /// <returns>The context.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="context"/> is <see langword="null"/>.</exception>
    public static MenuRootContext Require(MenuRootContext? context, string familyName) =>
        context ?? throw MenuParts.MissingRoot(familyName);

    /// <summary>Subscribes a part to the menu's state.</summary>
    /// <param name="changed">Called when one of the subscription's selections changed, the part's <c>StateHasChanged</c>.</param>
    /// <returns>The subscription; the part disposes it with itself.</returns>
    public abstract MenuSubscription Subscribe(Action changed);

    /// <summary>Registers a part that selects the open or mounted state and brings the render-batch boundary.</summary>
    /// <returns>The registration; disposing it unregisters the part.</returns>
    public abstract IDisposable RegisterBoundaryPart();

    /// <summary>Registers a listener for a change of whether the current close keeps the popup mounted, called before the write is published.</summary>
    /// <param name="listener">The listener, called with the new value.</param>
    /// <returns>The registration; disposing it unregisters the listener.</returns>
    public abstract IDisposable OnUnmountPreventionChanging(Action<bool> listener);

    /// <summary>Runs the store's render-batch boundary work.</summary>
    /// <returns>A task that completes when it has run.</returns>
    public abstract Task AtBatchBoundaryAsync();

    /// <summary>Runs the menu's open-change sequence.</summary>
    /// <param name="open">The requested open state.</param>
    /// <param name="details">The request's details.</param>
    /// <returns><see langword="true"/> if the change was applied.</returns>
    public abstract Task<bool> RequestOpenAsync(bool open, OpenChangeEventDetails details);

    // MNU-PTR-15, MNU-DIS-07 (useMenuItemCommonProps.ts:100-106; MenuPopup.tsx:104-120; MenuTrigger.tsx:156): the tree's
    // `close` event, which closes every menu of the tree, as C# store work (plan §Settled).
    /// <summary>Closes every open menu of the tree, from its root down.</summary>
    /// <param name="reason">The reason, <see cref="EventReason.ItemPress"/> or <see cref="EventReason.CancelOpen"/>.</param>
    /// <param name="snapshot">The snapshot of the event that caused the close, or <see langword="null"/>.</param>
    /// <returns>A task that completes when every close request has run.</returns>
    public abstract Task CloseTreeAsync(EventReason reason, EventSnapshot? snapshot);

    /// <summary>Records the popup element and the id it renders.</summary>
    /// <param name="element">The popup element.</param>
    /// <param name="id">The id it renders.</param>
    public abstract void SetPopupElement(ElementReference? element, string? id);

    /// <summary>Forgets the popup element.</summary>
    public abstract void ClearPopupElement();

    /// <summary>Records the positioner element.</summary>
    /// <param name="element">The positioner element, or <see langword="null"/>.</param>
    public abstract void SetPositionerElement(ElementReference? element);

    /// <summary>Mirrors the popup's reported transition status.</summary>
    /// <param name="status">The status.</param>
    public abstract void MirrorStatus(TransitionStatus? status);

    /// <summary>Applies a finished enter or exit transition of the popup.</summary>
    /// <param name="opened">Whether the enter finished.</param>
    /// <returns>A task that completes when the callbacks have run.</returns>
    public abstract Task CompleteTransitionAsync(bool opened);

    /// <summary>Writes the id of a mounted list.</summary>
    /// <param name="id">The list's id, or <see langword="null"/> when it went away.</param>
    public abstract void SetListElementId(string? id);

    /// <summary>Writes whether a viewport is mounted.</summary>
    /// <param name="adaptiveOrigin">Whether a viewport is mounted.</param>
    public abstract void SetAdaptiveOrigin(bool adaptiveOrigin);

    /// <summary>Writes the trigger-change instant type once the move has finished, if still open.</summary>
    public abstract void CompleteTriggerChange();

    /// <summary>Drops a stored trigger-change instant type once closed.</summary>
    public abstract void ClearTriggerChangeWhenClosed();

    /// <summary>Gets the element of a registered trigger.</summary>
    /// <param name="triggerId">The trigger's id.</param>
    /// <returns>The trigger's element, or <see langword="null"/> when no rendered trigger has that id.</returns>
    public abstract ElementReference? TriggerElement(string? triggerId);

    // MNU-DIS-04, MNU-DIS-05 (MenuPositioner.tsx:134-175): a submenu registers with its parent for the parent-close cascade
    // and the sibling close; Base UI's `menuopenchange` listeners as C# subscriptions.
    /// <summary>Registers a submenu of this menu.</summary>
    /// <param name="child">The submenu's context.</param>
    /// <returns>The registration; disposing it unregisters the submenu.</returns>
    public abstract IDisposable RegisterChild(MenuRootContext child);
}

/// <summary>A part's subscription to a menu's state, independent of its payload type.</summary>
internal abstract class MenuSubscription : IDisposable
{
    /// <inheritdoc />
    public abstract void Dispose();

    /// <summary>Adds a selection.</summary>
    /// <typeparam name="TValue">The type of the selected value.</typeparam>
    /// <param name="selector">The selector.</param>
    /// <returns>The selection, whose value follows the state.</returns>
    public abstract MenuSelection<TValue> Select<TValue>(Func<IMenuState, TValue> selector);

    /// <summary>Adds a selection with an argument.</summary>
    /// <typeparam name="TArg">The type of the argument.</typeparam>
    /// <typeparam name="TValue">The type of the selected value.</typeparam>
    /// <param name="selector">The selector.</param>
    /// <param name="arg">The argument.</param>
    /// <returns>The selection.</returns>
    public abstract MenuSelection<TArg, TValue> Select<TArg, TValue>(Func<IMenuState, TArg, TValue> selector, TArg arg);
}

/// <summary>A selected value of a menu's state.</summary>
/// <typeparam name="TValue">The type of the value.</typeparam>
internal abstract class MenuSelection<TValue>
{
    /// <summary>Gets the value for the current state.</summary>
    public abstract TValue Value { get; }
}

/// <summary>A selected value of a menu's state whose selector takes an argument.</summary>
/// <typeparam name="TArg">The type of the argument.</typeparam>
/// <typeparam name="TValue">The type of the value.</typeparam>
internal abstract class MenuSelection<TArg, TValue> : MenuSelection<TValue>
{
    /// <summary>Gets or sets the selector's argument.</summary>
    public abstract TArg Arg { get; set; }
}

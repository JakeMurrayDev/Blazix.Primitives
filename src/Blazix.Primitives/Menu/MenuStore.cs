using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// Base UI's MenuStore (menu/store/MenuStore.ts) over the shared PopupStore (ADR 05 Amendments 2026-10-27; plan §Settled),
// with MenuRoot's own open-change sequence (MenuRoot.tsx:341-445, MNU §4.1):
// - a close requested while closed is dropped (MNU-EVT-01; relayed tree closes and stale hover timers);
// - an identical request is dropped (MNU-STA-01): the same open state, from the active trigger, with the last reason. A
//   trigger-less request never matches a null active trigger (Base UI's `undefined === null` is false), for ContextMenu;
// - a close without a trigger reports the active one (MNU-STA-02), then the consumer's OnOpenChange and a cancel;
// - the 300 ms touch guard after a `trigger-focus` open (MNU-STA-05; design point 7 of M5 track 6, settled by the orchestrator 2026-11-04): a
//   touch `click` closing the menu for a reason other than `item-press` is dropped after the consumer's callback, as Base
//   UI drops it after `onOpenChange`; a controlled consumer's `OpenChanged` still runs, as its state change would in
//   Base UI. A timestamp under the store's TimeProvider stands for Base UI's timeout (the timer's only result is state);
// - one write with the reason, KeyboardOpen (isKeyboardOpen.ts) and the instant type (MNU-STA-04): `group` under a menubar
//   for its five reasons, `click` for a press whose event has no detail, `dismiss` for Escape. Base UI's `reason == null`
//   branch is unreachable: a C# reason is never null (the Popover precedent, a dated note on MNU-STA-04).
// The tree's `item-press` and `cancel-open` closes and the parent-close cascade are the root context's (plan §Settled,
// sheet B Q9 (c)); `itemhover`, the sibling-close timer, `hoverEnabled` and `allowMouseEnter` are the `interactions`
// module's. ADR 04 Amendments 2026-11-03 (5): the store is the Root's node in the family-agnostic cascade.
/// <summary>Holds the state of one menu and runs its open-change sequence.</summary>
/// <typeparam name="TPayload">The type of the payload the menu's triggers carry.</typeparam>
internal sealed class MenuStore<TPayload> : PopupStore<MenuStoreState<TPayload>, TPayload>, IFloatingNodeContext
{
    // MenuRoot.tsx:393-399.
    private static readonly TimeSpan s_touchGuard = TimeSpan.FromMilliseconds(300);

    private readonly TimeProvider timeProvider;
    private long touchGuardStart = -1;
    private MenuInstantType? seededInstantType;
    private MenuParent rootParent;
    private MenuParent? forwardedParent;

    // Track 8 (Menubar.tsx:120's `menuopenchange`, plan §Settled): the commit of each open change, after its write.
    /// <summary>Occurs after the store committed an open change, with the committed open state and the change's reason.</summary>
    public event Action<bool, EventReason>? OpenChangeCommitted;

    /// <summary>Initializes a new instance of the <see cref="MenuStore{TPayload}"/> class.</summary>
    /// <param name="floatingId">The generated id of the popup element.</param>
    /// <param name="sessionToken">The session token the root drew, or <see langword="null"/> for a fallback store.</param>
    /// <param name="inert">Whether this is a handle's fallback store.</param>
    /// <param name="logger">The root's logger, or <see langword="null"/>.</param>
    /// <param name="timeProvider">The clock of the touch guard, or <see langword="null"/> for the system's.</param>
    public MenuStore(string? floatingId, string? sessionToken, bool inert = false, ILogger? logger = null, TimeProvider? timeProvider = null)
        : base(MenuStoreState<TPayload>.Initial(floatingId), "Menu", sessionToken, inert, logger)
    {
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Gets the context the root created over this store, which a detached trigger reaches its list through.</summary>
    /// <value>The context, or <see langword="null"/> for a handle's fallback store.</value>
    public MenuRootContext? Context { get; private set; }

    // MNU-STA-04 (MenuRoot.tsx:404, 422-437; isKeyboardOpen.ts).
    /// <summary>Derives the instant type an applied open change writes.</summary>
    /// <param name="parent">The menu's parent kind, or <see langword="null"/> for a top-level menu.</param>
    /// <param name="open">The applied open state.</param>
    /// <param name="details">The change's details.</param>
    /// <returns>The instant type, or <see langword="null"/>.</returns>
    public static MenuInstantType? InstantTypeFor(MenuParentKind? parent, bool open, OpenChangeEventDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        EventReason reason = details.Reason;
        if (parent == MenuParentKind.Menubar && reason is EventReason.TriggerFocus or EventReason.FocusOut or EventReason.TriggerHover or EventReason.ListNavigation or EventReason.SiblingOpen)
        {
            return MenuInstantType.Group;
        }

        if (IsKeyboardClick(details))
        {
            return MenuInstantType.Click;
        }

        return !open && reason == EventReason.EscapeKey ? MenuInstantType.Dismiss : null;
    }

    // isKeyboardOpen.ts:8-21.
    /// <summary>Determines whether an open change came from the keyboard or an assistive-technology press.</summary>
    /// <param name="open">The applied open state.</param>
    /// <param name="details">The change's details.</param>
    /// <returns><see langword="true"/> for an open by a navigation key or by a press whose event has no detail.</returns>
    public static bool IsKeyboardOpen(bool open, OpenChangeEventDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        return open && (details.Reason == EventReason.ListNavigation || IsKeyboardClick(details));
    }

    /// <summary>Creates the context the menu's root cascades to its parts.</summary>
    /// <param name="familyName">The root's family, such as <c>Menu</c>.</param>
    /// <returns>The context.</returns>
    public MenuRootContext CreateContext(string familyName) => Context = new MenuRootContext<TPayload>(this, familyName);

    /// <inheritdoc />
    public override async Task<bool> RequestOpenAsync(bool open, OpenChangeEventDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        if (IsInert)
        {
            return false;
        }

        // MenuRoot.tsx:348-360: read the store, as relayed tree closes and stale hover timers can arrive late.
        MenuStoreState<TPayload> state = State;
        bool currentlyOpen = state.Popup.EffectiveOpen;
        if (!open && !currentlyOpen)
        {
            return false;
        }

        if (open == currentlyOpen && state.OpenChangeReason == details.Reason && IsActiveTrigger(state, details.Trigger))
        {
            return false;
        }

        bool applied = await base.RequestOpenAsync(open, details);
        if (applied)
        {
            OpenChangeCommitted?.Invoke(open, details.Reason);
        }

        return applied;
    }

    /// <summary>Writes the parent the root resolved from its position.</summary>
    /// <param name="parent">The parent.</param>
    public void SetParent(MenuParent parent)
    {
        rootParent = parent;
        WriteParent();
    }

    // MBR-STR-04 (MenuTrigger.tsx:95-108, popupStoreUtils.ts:339-390), design point 2 (i) of M5 track 8 (settled by the
    // orchestrator 2026-11-11): the trigger that mounts the popup forwards its own parent, a menubar or none, and it stays
    // the store's until another trigger mounts it; the root's position gives the parent before any trigger has.
    /// <summary>Writes the parent of the trigger that holds the popup.</summary>
    /// <param name="parent">The trigger's parent: a menubar or none.</param>
    public void ForwardTriggerParent(MenuParent parent)
    {
        forwardedParent = parent;
        WriteParent();
    }

    /// <summary>Writes the root's own fields from its parameters.</summary>
    /// <param name="disabled">The root's <c>Disabled</c>.</param>
    /// <param name="modal">The root's <c>Modal</c> as supplied.</param>
    /// <param name="rootId">The root's generated id.</param>
    /// <param name="highlightItemOnHover">The root's <c>HighlightItemOnHover</c>.</param>
    public void SetRootFields(bool disabled, bool? modal, string? rootId, bool highlightItemOnHover)
    {
        MenuStoreState<TPayload> state = State;

        // MenuRoot.tsx:166, 220: `modal` is stored for a top-level menu only.
        bool? stored = state.Parent.Kind is null ? modal : null;
        if (state.Disabled != disabled || state.Modal != stored || !string.Equals(state.RootId, rootId, StringComparison.Ordinal) || state.HighlightItemOnHover != highlightItemOnHover)
        {
            Write(state with { Disabled = disabled, Modal = stored, RootId = rootId, HighlightItemOnHover = highlightItemOnHover });
        }
    }

    /// <summary>Writes the id of a mounted list.</summary>
    /// <param name="id">The list's id, or <see langword="null"/> when it went away.</param>
    public void SetListElementId(string? id)
    {
        if (!string.Equals(State.ListElementId, id, StringComparison.Ordinal))
        {
            Write(State with { ListElementId = id });
        }
    }

    /// <summary>Writes whether a viewport is mounted.</summary>
    /// <param name="adaptiveOrigin">Whether a viewport is mounted.</param>
    public void SetAdaptiveOrigin(bool adaptiveOrigin)
    {
        if (State.AdaptiveOrigin != adaptiveOrigin)
        {
            Write(State with { AdaptiveOrigin = adaptiveOrigin });
        }
    }

    /// <summary>Writes the active trigger's close delay.</summary>
    /// <param name="closeDelay">The delay in milliseconds.</param>
    public void ForwardTriggerState(int closeDelay)
    {
        if (State.CloseDelay != closeDelay)
        {
            Write(State with { CloseDelay = closeDelay });
        }
    }

    // MNU-ANI-03 (MenuRoot.tsx:138-153, `animateInitialOpen`): a submenu that mounts open during its parent's enter plays its
    // own, as Base UI's store starts it in `'starting'`; the seed of the root's parameters mounted it settled.
    /// <summary>Starts the enter of a submenu that mounts open during its parent's enter.</summary>
    public void BeginInitialEnter()
    {
        PopupLifecycleState lifecycle = State.Popup.Lifecycle;
        if (lifecycle.Mounted && lifecycle.TransitionStatus is null)
        {
            WriteLifecycle(lifecycle with { TransitionStatus = TransitionStatus.Starting });
        }
    }

    // MNU-ANI-04 (MenuRoot.tsx:139-160): an initially open submenu mirrors its parent's instant type.
    /// <summary>Seeds the instant type an initially open submenu takes from its parent.</summary>
    /// <param name="inherited">The parent's instant type, or <see langword="null"/>.</param>
    public void SeedInstantType(MenuInstantType? inherited)
    {
        seededInstantType = inherited;
        if (inherited is not null && State.InstantType != inherited)
        {
            Write(State with { InstantType = inherited });
        }
    }

    // MenuRoot.tsx:189-224: the seed is cleared once the enter settles, unless an interactive change replaced it.
    /// <summary>Clears the seeded instant type once the initial enter has settled.</summary>
    public void ClearSeededInstantType()
    {
        if (seededInstantType is { } seeded)
        {
            seededInstantType = null;
            if (State.InstantType == seeded)
            {
                Write(State with { InstantType = null });
            }
        }
    }

    // TRN-ANI-28 (useTriggerSwitchTransition.ts): a move between triggers clears the instant type while it animates.
    /// <summary>Clears the instant type when the menu starts moving to another trigger while open.</summary>
    public void BeginTriggerChange()
    {
        if (State.InstantType is not null)
        {
            Write(State with { InstantType = null });
        }
    }

    /// <summary>Writes <see cref="MenuInstantType.TriggerChange"/> once a move between triggers has finished, if still open.</summary>
    public void CompleteTriggerChange()
    {
        if (State.Popup.EffectiveOpen && State.InstantType != MenuInstantType.TriggerChange)
        {
            Write(State with { InstantType = MenuInstantType.TriggerChange });
        }
    }

    /// <summary>Drops a stored <see cref="MenuInstantType.TriggerChange"/> once the menu is closed.</summary>
    public void ClearTriggerChangeWhenClosed()
    {
        if (!State.Popup.EffectiveOpen && State.InstantType == MenuInstantType.TriggerChange)
        {
            Write(State with { InstantType = null });
        }
    }

    // MNU-STA-08 (MenuStore.ts:190-218): the parent's values the selectors read changed.
    /// <summary>Notifies the store's subscribers that a value read through the parent changed.</summary>
    public void NotifyParentChanged() => NotifyAll();

    /// <inheritdoc />
    protected override async Task<bool> RunConsumerStepAsync(bool open, OpenChangeEventDetails details)
    {
        if (!await OpenState.NotifyAsync(open, details, OnOpenChange))
        {
            return false;
        }

        // MenuRoot.tsx:381-402 (MNU-STA-05).
        if (!open && details.Reason != EventReason.ItemPress && touchGuardStart >= 0 && timeProvider.GetElapsedTime(touchGuardStart) < s_touchGuard
            && details.Event is { Type: "click", PointerType: "touch" })
        {
            if (OpenState.IsControlled)
            {
                await OpenState.CommitAsync(open, OpenChanged);
            }

            return false;
        }

        touchGuardStart = open && details.Reason == EventReason.TriggerFocus ? timeProvider.GetTimestamp() : -1;
        await OpenState.CommitAsync(open, OpenChanged);
        return true;
    }

    /// <inheritdoc />
    protected override void PrepareOpenChange(bool open, OpenChangeEventDetails details)
    {
        // MNU-STA-02 (MenuRoot.tsx:366-370): a close without a trigger reports the active one.
        PopupStoreState<TPayload> popup = State.Popup;
        if (open || details.Trigger is not null || popup.EffectiveActiveTriggerId is not { } activeId)
        {
            return;
        }

        if (Triggers.TryGetById(activeId, out PopupTriggerElement registered))
        {
            details.Trigger = new EventTrigger(activeId, registered.Element);
        }
        else if (popup.DerivedActiveTriggerElement is { } element)
        {
            details.Trigger = new EventTrigger(activeId, element.Element);
        }
    }

    /// <inheritdoc />
    protected override MenuStoreState<TPayload> ApplyOpenChange(MenuStoreState<TPayload> state, bool open, OpenChangeEventDetails details)
    {
        // TRN-ANI-28 (useTriggerSwitchTransition.ts): a request that moves the open menu to another trigger clears the
        // instant type in the write that applies the move, as Popover's does.
        PopupStoreState<TPayload> popup = state.Popup;
        bool switches = open && popup.EffectiveOpen && details.Trigger?.Id is { } next
            && popup.EffectiveActiveTriggerId is { } previous && !string.Equals(previous, next, StringComparison.Ordinal);
        MenuInstantType? instantType = switches ? null : InstantTypeFor(state.Parent.Kind, open, details);
        bool keyboardOpen = IsKeyboardOpen(open, details);

        // An interactive change replaces a seeded instant type (MenuRoot.tsx:189-224).
        seededInstantType = null;

        // FLT-FOC-10: the focus manager classifies the close type from the event that caused the close.
        EventSnapshot? closeEvent = open || !popup.EffectiveOpen ? state.CloseEvent : details.Event;
        return state.OpenChangeReason == details.Reason && state.InstantType == instantType && state.KeyboardOpen == keyboardOpen && ReferenceEquals(state.CloseEvent, closeEvent)
            ? state
            : state with { OpenChangeReason = details.Reason, InstantType = instantType, KeyboardOpen = keyboardOpen, CloseEvent = closeEvent };
    }

    // useOpenStateTransitions' unmount callback: the reason resets with the menu.
    /// <inheritdoc />
    protected override void OnUnmount()
    {
        if (State.OpenChangeReason is not null)
        {
            Write(State with { OpenChangeReason = null });
        }
    }

    // isKeyboardOpen.ts:8-13: `(event as MouseEvent)?.detail === 0`, which a UI event without a pointer press meets; Base UI's
    // stand-in Event has no `detail`.
    private void WriteParent()
    {
        MenuParent parent = forwardedParent ?? rootParent;
        if (State.Parent != parent)
        {
            Write(State with { Parent = parent });
        }
    }

    private static bool IsKeyboardClick(OpenChangeEventDetails details) =>
        details.Reason is EventReason.TriggerPress or EventReason.ItemPress && details.Event.IsInstanceOf(EventKind.UIEvent) && details.Event.Detail == 0;

    // MenuRoot.tsx:354-360: `eventDetails.trigger === activeTriggerElement`; a request without a trigger never matches.
    private static bool IsActiveTrigger(MenuStoreState<TPayload> state, EventTrigger? trigger) =>
        trigger?.Id is { } id && string.Equals(state.Popup.EffectiveActiveTriggerId, id, StringComparison.Ordinal);
}

using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// Base UI's PopoverStore (popover/store/PopoverStore.ts), over the shared PopupStore (ADR 05 Amendments 2026-10-27). Its
// own steps of the open-change sequence (PopoverStore.setOpen, POP §16 Q11):
// - the `close-press` trigger back-filled before the consumer's callback (POP-EVT-06): the registered element of the active
//   trigger id, else the active trigger element;
// - the shared sequence: OnOpenChange, a cancel honoured (POP-EVT-03), the relay;
// - one write with the reason (POP-STA-06) and the instant type (POP-STA-07). A C# reason is never null, so §16 Q2's
//   null-reason branch is unreachable (a dated resolution in popover.md).
// The patient-click flag and its timer (POP-STA-11) are the `interactions` module's (ADR 04 Amendments 2026-10-27).
/// <summary>Holds the state of one popover and runs its open-change sequence.</summary>
/// <typeparam name="TPayload">The type of the payload the popover's triggers carry.</typeparam>
internal sealed class PopoverStore<TPayload> : PopupStore<PopoverStoreState<TPayload>, TPayload>
{
    /// <summary>Initializes a new instance of the <see cref="PopoverStore{TPayload}"/> class.</summary>
    /// <param name="floatingId">The generated id of the popup element.</param>
    /// <param name="sessionToken">The session token the root drew, or <see langword="null"/> for a fallback store.</param>
    /// <param name="inert">Whether this is a handle's fallback store.</param>
    /// <param name="logger">The root's logger, or <see langword="null"/>.</param>
    public PopoverStore(string? floatingId, string? sessionToken, bool inert = false, ILogger? logger = null)
        : base(PopoverStoreState<TPayload>.Initial(floatingId), "Popover", sessionToken, inert, logger)
    {
    }

    // POP-STA-07 (PopoverStore.ts:114-119, 174-182).
    /// <summary>Derives the instant type an applied open change writes.</summary>
    /// <param name="open">The applied open state.</param>
    /// <param name="details">The change's details.</param>
    /// <returns><see cref="PopoverInstantType.Click"/> for a press without a pointer, <see cref="PopoverInstantType.Dismiss"/> for an Escape close, <see cref="PopoverInstantType.Focus"/> for a focus-out change, otherwise <see langword="null"/>.</returns>
    public static PopoverInstantType? InstantTypeFor(bool open, OpenChangeEventDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);
        if (details.Reason == EventReason.TriggerPress && details.Event.IsInstanceOf(EventKind.MouseEvent) && details.Event.Detail == 0)
        {
            return PopoverInstantType.Click;
        }

        if (!open && details.Reason == EventReason.EscapeKey)
        {
            return PopoverInstantType.Dismiss;
        }

        return details.Reason == EventReason.FocusOut ? PopoverInstantType.Focus : null;
    }

    /// <summary>Writes the root's <c>Modal</c>.</summary>
    /// <param name="modal">The modal mode.</param>
    public void SetModal(PopoverModalKind modal)
    {
        if (State.Modal != modal)
        {
            Write(State with { Modal = modal });
        }
    }

    // POP-STA-10: the Popup writes it every render.
    /// <summary>Writes whether focus is trapped in the popup.</summary>
    /// <param name="focusManagerModal">Whether focus is trapped.</param>
    public void SetFocusManagerModal(bool focusManagerModal)
    {
        if (State.FocusManagerModal != focusManagerModal)
        {
            Write(State with { FocusManagerModal = focusManagerModal });
        }
    }

    // POP-STA-05: the active trigger's forwarded values.
    /// <summary>Writes the active trigger's <c>Disabled</c>, <c>OpenOnHover</c> and close delay.</summary>
    /// <param name="disabled">The trigger's <c>Disabled</c>.</param>
    /// <param name="openOnHover">The trigger's <c>OpenOnHover</c>.</param>
    /// <param name="closeDelay">The trigger's close delay in milliseconds.</param>
    public void ForwardTriggerState(bool disabled, bool openOnHover, int closeDelay)
    {
        PopoverStoreState<TPayload> state = State;
        if (state.Disabled != disabled || state.OpenOnHover != openOnHover || state.CloseDelay != closeDelay)
        {
            Write(state with { Disabled = disabled, OpenOnHover = openOnHover, CloseDelay = closeDelay });
        }
    }

    // POP-STA-09: the latest mount wins; an unmount clears only its own id.
    /// <summary>Writes the title's id.</summary>
    /// <param name="id">The id, or <see langword="null"/> to clear it.</param>
    public void SetTitleElementId(string? id)
    {
        if (!string.Equals(State.TitleElementId, id, StringComparison.Ordinal))
        {
            Write(State with { TitleElementId = id });
        }
    }

    /// <summary>Writes the description's id.</summary>
    /// <param name="id">The id, or <see langword="null"/> to clear it.</param>
    public void SetDescriptionElementId(string? id)
    {
        if (!string.Equals(State.DescriptionElementId, id, StringComparison.Ordinal))
        {
            Write(State with { DescriptionElementId = id });
        }
    }

    // PSH-POS-02: a mounted Viewport positions the popover by its edges.
    /// <summary>Writes whether a viewport is mounted.</summary>
    /// <param name="adaptiveOrigin">Whether a viewport is mounted.</param>
    public void SetAdaptiveOrigin(bool adaptiveOrigin)
    {
        if (State.AdaptiveOrigin != adaptiveOrigin)
        {
            Write(State with { AdaptiveOrigin = adaptiveOrigin });
        }
    }

    // POP-ANI-07 (useTriggerSwitchTransition.ts): a move between triggers clears the instant type while it animates (a
    // controlled `TriggerId` move, from the Root's parameter set; a request's move in ApplyOpenChange), and
    // `trigger-change` is written once it settles, only while still open (TRN-ANI-31's live check).
    /// <summary>Clears the instant type when the popover starts moving to another trigger while open.</summary>
    public void BeginTriggerChange()
    {
        if (State.InstantType is not null)
        {
            Write(State with { InstantType = null });
        }
    }

    /// <summary>Writes <see cref="PopoverInstantType.TriggerChange"/> once a move between triggers has finished, if still open.</summary>
    public void CompleteTriggerChange()
    {
        if (State.Popup.EffectiveOpen && State.InstantType != PopoverInstantType.TriggerChange)
        {
            Write(State with { InstantType = PopoverInstantType.TriggerChange });
        }
    }

    // useTriggerSwitchTransition.ts:63-78: `trigger-change` belongs to the open cycle that wrote it; a controlled reopen
    // would otherwise expose it again.
    /// <summary>Drops a stored <see cref="PopoverInstantType.TriggerChange"/> once the popover is closed.</summary>
    public void ClearTriggerChangeWhenClosed()
    {
        if (!State.Popup.EffectiveOpen && State.InstantType == PopoverInstantType.TriggerChange)
        {
            Write(State with { InstantType = null });
        }
    }

    /// <inheritdoc />
    protected override void PrepareOpenChange(bool open, OpenChangeEventDetails details)
    {
        // POP-EVT-06 (PopoverStore.ts:125-136): a close press without a trigger reports the active one.
        PopupStoreState<TPayload> popup = State.Popup;
        if (open || details.Reason != EventReason.ClosePress || details.Trigger is not null || popup.EffectiveActiveTriggerId is not { } activeId)
        {
            return;
        }

        if (Triggers.TryGetById(activeId, out PopupTriggerElement registered))
        {
            details.Trigger = new EventTrigger(activeId, registered.Element);
        }
        else if (popup.DerivedActiveTriggerElement is { } element)
        {
            details.Trigger = new EventTrigger((element.Owner as IPopoverTriggerPart)?.TriggerId, element.Element);
        }
    }

    /// <inheritdoc />
    protected override PopoverStoreState<TPayload> ApplyOpenChange(PopoverStoreState<TPayload> state, bool open, OpenChangeEventDetails details)
    {
        // POP-ANI-07 (useTriggerSwitchTransition.ts): a request that moves the open popover to another trigger clears the
        // instant type in the write that applies the move, as Base UI's layout effect does at commit.
        PopupStoreState<TPayload> popup = state.Popup;
        bool switches = open && popup.EffectiveOpen && details.Trigger?.Id is { } next
            && popup.EffectiveActiveTriggerId is { } previous && !string.Equals(previous, next, StringComparison.Ordinal);
        PopoverInstantType? instantType = switches ? null : InstantTypeFor(open, details);
        return state.OpenChangeReason == details.Reason && state.InstantType == instantType
            ? state
            : state with { OpenChangeReason = details.Reason, InstantType = instantType };
    }

    // useOpenStateTransitions' unmount callback (PopoverRoot.tsx:59-62): the reason resets with the popover.
    /// <inheritdoc />
    protected override void OnUnmount()
    {
        if (State.OpenChangeReason is not null)
        {
            Write(State with { OpenChangeReason = null });
        }
    }
}

/// <summary>Defines a popover trigger as the store sees its registration.</summary>
internal interface IPopoverTriggerPart
{
    /// <summary>Gets the id the trigger renders and registers under.</summary>
    string? TriggerId { get; }
}

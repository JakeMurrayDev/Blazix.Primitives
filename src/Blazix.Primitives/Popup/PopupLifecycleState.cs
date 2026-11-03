using Blazix.Primitives.Interop;

namespace Blazix.Primitives;

// FLT-STR-11 (useFloatingRootContext.ts:24-77, FloatingRootStore.ts:106-135), the store-independent open lifecycle extracted
// from PopupStoreState<TPayload> (ADR 05 Amendments 2026-11-03, chosen by the user 2026-11-03; M5 track 2 design point 1,
// shape (a2), settled by the orchestrator 2026-11-04): the fields of every popup's open lifecycle, whatever store holds them. A struct with
// a member-by-member ==, so a family record holds it as one member, the generated ShallowEquals compares it member by
// member with no generic or EqualityComparer<T> call (nothing boxes on the interpreter, ADR 16 rule 7), and a forwarded
// `init` copies it on the stack instead of allocating. Disabled sits here because the effective open and every transition
// evaluation read it (ADR 05 Amendments 2026-10-28); only Tooltip writes it.
/// <summary>Represents the open lifecycle of one popup: its open state, mounting, transition and last committed change.</summary>
internal readonly struct PopupLifecycleState : IEquatable<PopupLifecycleState>
{
    /// <summary>Gets the internal open state, written by every committed open change.</summary>
    public bool Open { get; init; }

    /// <summary>Gets the controlled open state, or <see langword="null"/> while uncontrolled.</summary>
    public bool? OpenProp { get; init; }

    /// <summary>Gets a value indicating whether the popup's subtree is mounted; it lags <see cref="Open"/> through the exit.</summary>
    public bool Mounted { get; init; }

    /// <summary>Gets the stored transition phase.</summary>
    public TransitionStatus? TransitionStatus { get; init; }

    /// <summary>Gets the generated id of the popup element.</summary>
    public string? FloatingId { get; init; }

    /// <summary>Gets a value indicating whether the current close cycle stays mounted until the unmount action.</summary>
    public bool PreventUnmountingOnClose { get; init; }

    /// <summary>Gets how many times the unmount action ended a close.</summary>
    public int UnmountRequests { get; init; }

    /// <summary>Gets the relay of the last committed open change, or <see langword="null"/> before the first.</summary>
    public OpenChangeRelay? OpenChange { get; init; }

    /// <summary>Gets how the user opened the popup, or <see langword="null"/> while closed.</summary>
    public InteractionType? OpenMethod { get; init; }

    /// <summary>Gets a value indicating whether the popup's root is disabled, which keeps it effectively closed.</summary>
    public bool Disabled { get; init; }

    // PSH-STA-02, TIP-STA-02.
    /// <summary>Gets the effective open state: the controlled value when controlled, else the internal one, while the root is not disabled.</summary>
    public bool EffectiveOpen => IsEffectivelyOpen(Disabled, OpenProp, Open);

    // PSH-STA-05, TRN-STA-14.
    /// <summary>Gets the derived transition status: starting while effectively open and not yet mounted, else the stored one.</summary>
    public TransitionStatus? DerivedTransitionStatus => EffectiveOpen && !Mounted ? Primitives.TransitionStatus.Starting : TransitionStatus;

    /// <summary>Determines whether two lifecycle states are equal member by member.</summary>
    /// <param name="left">The first state.</param>
    /// <param name="right">The second state.</param>
    /// <returns><see langword="true"/> if every member is equal; the relay compares by reference.</returns>
    public static bool operator ==(PopupLifecycleState left, PopupLifecycleState right) =>
        left.Open == right.Open
        && left.OpenProp == right.OpenProp
        && left.Mounted == right.Mounted
        && left.TransitionStatus == right.TransitionStatus
        && string.Equals(left.FloatingId, right.FloatingId, StringComparison.Ordinal)
        && left.PreventUnmountingOnClose == right.PreventUnmountingOnClose
        && left.UnmountRequests == right.UnmountRequests
        && ReferenceEquals(left.OpenChange, right.OpenChange)
        && left.OpenMethod == right.OpenMethod
        && left.Disabled == right.Disabled;

    /// <summary>Determines whether two lifecycle states differ in any member.</summary>
    /// <param name="left">The first state.</param>
    /// <param name="right">The second state.</param>
    /// <returns><see langword="true"/> if any member differs.</returns>
    public static bool operator !=(PopupLifecycleState left, PopupLifecycleState right) => !(left == right);

    /// <summary>Creates the initial state of a popup.</summary>
    /// <param name="floatingId">The generated id of the popup element.</param>
    /// <returns>A closed, unmounted state with no committed change.</returns>
    public static PopupLifecycleState Initial(string? floatingId) => new() { FloatingId = floatingId };

    // PSH-STA-02, TIP-STA-02: the one definition, also read for a state a write is about to commit.
    /// <summary>Determines the effective open state of a set of inputs.</summary>
    /// <param name="disabled">Whether the root is disabled.</param>
    /// <param name="openProp">The controlled open state, or <see langword="null"/> while uncontrolled.</param>
    /// <param name="open">The internal open state.</param>
    /// <returns><see langword="true"/> if the root is not disabled and the controlled value, else the internal one, is open.</returns>
    public static bool IsEffectivelyOpen(bool disabled, bool? openProp, bool open) => !disabled && (openProp ?? open);

    /// <inheritdoc />
    public bool Equals(PopupLifecycleState other) => this == other;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PopupLifecycleState other && this == other;

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(HashCode.Combine(Open, OpenProp, Mounted, TransitionStatus, FloatingId), PreventUnmountingOnClose, UnmountRequests, OpenChange, OpenMethod, Disabled);
}

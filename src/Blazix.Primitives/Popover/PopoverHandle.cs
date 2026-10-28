namespace Blazix.Primitives;

// POP-API-26, POP-EDGE-05/06 (popover/store/PopoverHandle.ts:13-55); ADR 08 Amendments 2026-10-27 (chosen by the user
// 2026-10-27): Popover.createHandle() is `new PopoverHandle<TPayload>()`. An anchored popover throws when opened by an id
// no trigger registered (track 1's message, component name Popover).
/// <summary>
/// Connects a <see cref="PopoverRoot{TPayload}"/> with <see cref="PopoverTrigger{TPayload}"/> parts rendered anywhere on the
/// page, and opens and closes the popover from code.
/// </summary>
/// <typeparam name="TPayload">
/// The type of the payload the popover's triggers carry; <see cref="NoPayload"/> for a popover whose triggers carry none.
/// </typeparam>
/// <remarks>
/// Pass the handle to the root's <c>Handle</c> and to each trigger's <c>Handle</c>. <see cref="PopupHandle{TPayload}.OpenAsync"/>
/// needs the id of a trigger that uses this handle and throws for any other id; calls made while no root uses the handle
/// are ignored.
/// </remarks>
public sealed class PopoverHandle<TPayload> : PopupHandle<TPayload>
{
    /// <summary>Initializes a new instance of the <see cref="PopoverHandle{TPayload}"/> class.</summary>
    public PopoverHandle()
        : base("Popover", throwOnMissingTrigger: true)
    {
    }

    /// <inheritdoc />
    private protected override IPopupStore<TPayload> CreateFallbackStore() => new PopoverStore<TPayload>(floatingId: null, sessionToken: null, inert: true);
}

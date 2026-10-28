namespace Blazix.Primitives;

// TIP §3.9, TIP-API-23 (tooltip/store/TooltipHandle.ts:13-55); ADR 08 Amendments 2026-10-27 (chosen by the user
// 2026-10-27: a family handle created with `new`). The fallback store is the inert tooltip store (createNullTooltipStore,
// PSH-STA-37); an anchored popup throws on a missing trigger (TIP-EDGE-05).
/// <summary>
/// Connects a <see cref="TooltipRoot{TPayload}"/> with <see cref="TooltipTrigger{TPayload}"/> parts rendered anywhere on the
/// page, and opens and closes the tooltip from code.
/// </summary>
/// <typeparam name="TPayload">The type of the payload the tooltip's triggers carry; <see cref="NoPayload"/> for none.</typeparam>
/// <remarks>
/// Create one with <see langword="new"/> and pass it to the root and to each detached trigger through their <c>Handle</c>
/// parameters. <see cref="PopupHandle{TPayload}.OpenAsync(string?)"/> throws <see cref="InvalidOperationException"/> when
/// no trigger with the given id is registered, because a tooltip is anchored to its trigger.
/// </remarks>
public sealed class TooltipHandle<TPayload> : PopupHandle<TPayload>
{
    /// <summary>Initializes a new instance of the <see cref="TooltipHandle{TPayload}"/> class.</summary>
    public TooltipHandle()
        : base("Tooltip", throwOnMissingTrigger: true)
    {
    }

    /// <inheritdoc />
    private protected override IPopupStore<TPayload> CreateFallbackStore() => new TooltipStore<TPayload>(floatingId: null, sessionToken: null, inert: true);
}

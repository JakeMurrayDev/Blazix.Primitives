namespace Blazix.Primitives;

// PRV §3.9, PRV-API-21..23 (preview-card/store/PreviewCardHandle.ts); ADR 08 Amendments 2026-10-27 (chosen by the user
// 2026-10-27: a family handle created with `new`). The fallback store is the inert preview card store (PSH-STA-37); an
// anchored popup throws on a missing trigger (PRV-EDGE-05).
/// <summary>
/// Connects a <see cref="PreviewCardRoot{TPayload}"/> with <see cref="PreviewCardTrigger{TPayload}"/> parts rendered anywhere
/// on the page, and opens and closes the card from code.
/// </summary>
/// <typeparam name="TPayload">The type of the payload the card's triggers carry; <see cref="NoPayload"/> for none.</typeparam>
/// <remarks>
/// Create one with <see langword="new"/> and pass it to the root and to each detached trigger through their <c>Handle</c>
/// parameters. <see cref="PopupHandle{TPayload}.OpenAsync(string?)"/> throws <see cref="InvalidOperationException"/> when
/// no trigger with the given id is registered, because a preview card is anchored to its trigger.
/// </remarks>
public sealed class PreviewCardHandle<TPayload> : PopupHandle<TPayload>
{
    /// <summary>Initializes a new instance of the <see cref="PreviewCardHandle{TPayload}"/> class.</summary>
    public PreviewCardHandle()
        : base("PreviewCard", throwOnMissingTrigger: true)
    {
    }

    /// <inheritdoc />
    private protected override IPopupStore<TPayload> CreateFallbackStore() => new PreviewCardStore<TPayload>(floatingId: null, sessionToken: null, inert: true);
}

namespace Blazix.Primitives;

// MNU-API-63..66, MNU-EDGE-07 (menu/store/MenuHandle.ts); ADR 08 §3 and Amendments 2026-10-27 (chosen by the user
// 2026-10-27): Menu.createHandle() is `new MenuHandle<TPayload>()`. An anchored menu throws when opened by an id no trigger
// registered (PSH-API-09, component name Menu); the fallback store is inert (PopoverHandle's shape).
/// <summary>
/// Connects a <c>MenuRoot</c> with <c>MenuTrigger</c> parts rendered anywhere on the page,
/// and opens and closes the menu from code.
/// </summary>
/// <typeparam name="TPayload">
/// The type of the payload the menu's triggers carry; <see cref="NoPayload"/> for a menu whose triggers carry none.
/// </typeparam>
/// <remarks>
/// Pass the handle to the root's <c>Handle</c> and to each trigger's <c>Handle</c>. <see cref="PopupHandle{TPayload}.OpenAsync"/>
/// needs the id of a trigger that uses this handle and throws for any other id; calls made while no root uses the handle
/// are ignored.
/// </remarks>
public sealed class MenuHandle<TPayload> : PopupHandle<TPayload>
{
    /// <summary>Initializes a new instance of the <see cref="MenuHandle{TPayload}"/> class.</summary>
    public MenuHandle()
        : base("Menu", throwOnMissingTrigger: true)
    {
    }

    /// <inheritdoc />
    private protected override IPopupStore<TPayload> CreateFallbackStore() => new MenuStore<TPayload>(floatingId: null, sessionToken: null, inert: true);
}

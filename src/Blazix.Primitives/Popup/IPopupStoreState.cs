using Blazix.Primitives.Store;

namespace Blazix.Primitives;

// ADR 05 Amendments 2026-10-27 (chosen by the user 2026-10-27): a family's store record holds the shared state as one
// member, and the shared algorithms are generic over this interface, so BLZX1010 (no derived records), ADR 05 §1's one
// subscription per part and the generated ShallowEquals stand.
/// <summary>Defines a popup family's store record: the shared popup state it holds and how to replace it.</summary>
/// <typeparam name="TSelf">The type of the family's store record.</typeparam>
/// <typeparam name="TPayload">The type of the payload the popup's triggers carry.</typeparam>
internal interface IPopupStoreState<TSelf, TPayload>
    where TSelf : class, IStoreState<TSelf>, IPopupStoreState<TSelf, TPayload>
{
    /// <summary>Gets the shared popup state.</summary>
    PopupStoreState<TPayload> Popup { get; }

    /// <summary>Returns a copy of <paramref name="state"/> that holds <paramref name="popup"/>.</summary>
    /// <param name="state">The family's record.</param>
    /// <param name="popup">The shared popup state to hold.</param>
    /// <returns>The copy.</returns>
    static abstract TSelf WithPopup(TSelf state, PopupStoreState<TPayload> popup);
}

using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// PSH-ALG-06 (popupHandle.ts:43-63): what a handle needs of the stores it points at, whatever their family record: the
// effective open state, the trigger registry, the open-change request and the payload write of openWithPayload.
/// <summary>Defines the part of a popup store that its handle reads and drives.</summary>
/// <typeparam name="TPayload">The type of the payload the popup's triggers carry.</typeparam>
internal interface IPopupStore<TPayload>
{
    /// <summary>Gets a value indicating whether the popup is effectively open.</summary>
    bool IsOpen { get; }

    /// <summary>Gets the trigger registry.</summary>
    PopupTriggerRegistry Triggers { get; }

    /// <summary>Gets the logger of the root that owns the store, or <see langword="null"/> for none.</summary>
    ILogger? Logger { get; }

    /// <summary>Runs the open-change sequence for a requested open state.</summary>
    /// <param name="open">The requested open state.</param>
    /// <param name="details">The details of the request.</param>
    /// <returns><see langword="true"/> if the change was applied; <see langword="false"/> if it was canceled or the store is inert.</returns>
    Task<bool> RequestOpenAsync(bool open, OpenChangeEventDetails details);

    /// <summary>Writes the payload the popup renders, as a trigger activation or an imperative open does.</summary>
    /// <param name="payload">The payload.</param>
    void SetPayload(TPayload? payload);
}

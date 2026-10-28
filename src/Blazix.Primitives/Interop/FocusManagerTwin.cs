using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

// ADR 04 §1 (the `focus-manager` row: initial focus, modal trap, focus-out close, focus restoration, return focus,
// outside marking, tabbable queries) and Amendments 2026-10-27 (chosen by the user 2026-10-27): one handle per focus-
// managed popup, attached to the Popup part's element (Interop/CONTEXT.md, a part owns its twin). The module's prevents
// are unconditional and its closes are requests, so it has no decision point (the DomTwin precedent); `press` is the
// interactions twin's and `guard` the popup twin's (ADR 04 Amendments 2026-10-27). ADR 08 §3's own example name.
/// <summary>
/// The C# half of the <c>focus-manager</c> module: a popup's initial focus, its modal focus trap, the close of a non-modal
/// popup when focus leaves it, the restoration of focus inside it, return focus when it closes and the marking of the rest
/// of the page while it is open.
/// </summary>
/// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly application.</param>
internal sealed class FocusManagerTwin(IJSRuntime jsRuntime)
    : TwinBase<FocusManagerTwin, FocusManagerOptions>(jsRuntime, LibraryModule("focus-manager"), BlazixJsonContext.Default.FocusManagerOptions)
{
    /// <summary>Represents the restoration literal that restores no focus.</summary>
    public const string RestoreNone = "none";

    /// <summary>Represents the restoration literal that restores focus to the nearest tabbable element inside the popup.</summary>
    public const string RestoreTabbable = "tabbable";

    /// <summary>Represents the restoration literal that restores focus to the popup itself.</summary>
    public const string RestorePopup = "popup";

    /// <summary>Occurs when focus left a non-modal popup and the module asks to close it, once per open.</summary>
    /// <remarks>The argument carries the <c>focusout</c> snapshot, the recorded interaction type and the reason <see cref="EventReason.FocusOut"/>.</remarks>
    public event Action<FocusOutCloseRequest>? FocusOutCloseRequested;

    /// <summary>Occurs when the interaction type the module records changed, once per change.</summary>
    /// <remarks>The argument is <c>mouse</c>, <c>touch</c>, <c>pen</c>, <c>keyboard</c> or the empty string after an open reset it.</remarks>
    public event Action<string>? InteractionTypeChanged;

    /// <summary>Receives a focus-out close request.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="FocusOutCloseRequest"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnFocusOutClose(string payloadJson)
    {
        FocusOutCloseRequest request = Deserialize(payloadJson, BlazixJsonContext.Default.FocusOutCloseRequest);
        FocusOutCloseRequested?.Invoke(request);
    }

    /// <summary>Receives a change of the recorded interaction type.</summary>
    /// <param name="payloadJson">The JSON of an <see cref="InteractionTypeChange"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnInteractionTypeChange(string payloadJson)
    {
        InteractionTypeChange change = Deserialize(payloadJson, BlazixJsonContext.Default.InteractionTypeChange);
        InteractionTypeChanged?.Invoke(change.Type);
    }

    /// <inheritdoc />
    /// <remarks>The module has no decision point; every point answers <see cref="DecisionPolicy.Run"/>.</remarks>
    protected override DecisionPolicy OnDecide(string point, EventSnapshot snapshot) => DecisionPolicy.Run;
}

using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

// ADR 04 §1 (the `combobox` component module, written by track 10 under Amendments 2026-11-03 (4)), §3 (the `clear` and
// `remove` points of Amendments 2026-11-03 (3): the closed Escape's clear, fallback "stop when there is something to clear
// and the root is not inline"; the chip removal, fallback Stop; Amendments 2026-10-20). One twin per part element (ADR 04
// §1's one-twin-per-part rule).
/// <summary>
/// The C# half of the <c>combobox</c> module: a combobox-family part's composition, typed-input classification, caret,
/// Enter activation, chip keys, clear and removal, and the press defaults the other modules do not own.
/// </summary>
/// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly application.</param>
internal sealed class ComboboxTwin(IJSRuntime jsRuntime)
    : TwinBase<ComboboxTwin, ComboboxOptions>(jsRuntime, LibraryModule("combobox"), BlazixJsonContext.Default.ComboboxOptions)
{
    /// <summary>Represents the decision point the module asks before the closed Escape clears.</summary>
    public const string ClearPoint = "clear";

    /// <summary>Represents the decision point the module asks before a chip's removal.</summary>
    public const string RemovePoint = "remove";

    /// <summary>Represents the decision point the input's handle asks before its own key handling, the consumer's <c>onKeyDown</c> veto.</summary>
    public const string NavigatePoint = "navigate";

    /// <summary>Represents the decision point the chips' and the input group's handles ask before a press, the consumer's <c>onMouseDown</c> veto.</summary>
    public const string PressPoint = "press";

    /// <summary>Represents the part literal of an input's handle.</summary>
    public const string InputPart = "input";

    /// <summary>Represents the part literal of a trigger's handle.</summary>
    public const string TriggerPart = "trigger";

    /// <summary>Represents the part literal of a clear button's handle.</summary>
    public const string ClearPart = "clear";

    /// <summary>Represents the part literal of an input group's handle.</summary>
    public const string InputGroupPart = "input-group";

    /// <summary>Represents the part literal of a chips container's handle.</summary>
    public const string ChipsPart = "chips";

    /// <summary>Represents the part literal of a chip remove button's handle.</summary>
    public const string ChipRemovePart = "chip-remove";

    /// <summary>Represents the part literal of the handle of a chip with a consumer <c>onkeydown</c> or <c>onmousedown</c>.</summary>
    public const string ChipPart = "chip";

    /// <summary>Represents the part literal of a list's handle.</summary>
    public const string ListPart = "list";

    /// <summary>Represents the part literal of a popup's handle.</summary>
    public const string PopupPart = "popup";

    /// <summary>Occurs when the module classified a change of the input text.</summary>
    public event Action<ComboboxInputChange>? InputChanged;

    /// <summary>Occurs when the closed Escape asks for a clear that no C# chain ran inside the dispatch.</summary>
    public event Action<ComboboxRequest>? ClearRequested;

    /// <summary>Occurs when a chip's removal, or Backspace into an empty input, asks for a removal no C# chain ran.</summary>
    public event Action<ComboboxRequest>? RemoveRequested;

    /// <summary>Occurs when a press that opened from the trigger ended outside it, Base UI's <c>cancel-open</c>.</summary>
    public event Action<EventSnapshot>? OpenCanceled;

    /// <summary>Occurs when an internal dismiss button was pressed, Base UI's <c>close-press</c>.</summary>
    public event Action<EventSnapshot>? DismissPressed;

    /// <summary>Occurs before Enter's synthetic click on the highlighted item, with the keydown the selection is attributed to, and after it, with <see langword="null"/>.</summary>
    public event Action<EventSnapshot?>? SelectionEventReported;

    /// <summary>Occurs before the click of a press inside a link of an item, with its <c>href</c>, and after it, with <see langword="null"/>.</summary>
    public event Action<string?>? ItemLinkPressed;

    /// <summary>Occurs when the input's handle handled and stopped a key without a synchronous decision (Server), so the consumer's <c>onKeyDown</c> runs.</summary>
    public event Action<EventSnapshot>? KeyHandled;

    /// <summary>Occurs when Enter on an open list without a highlight closes the popup, with the reason <c>none</c>.</summary>
    public event Action<EventSnapshot>? EnterCloseRequested;

    /// <summary>Occurs when a press on the input group or the chips focused the input, Base UI's <c>input-press</c>.</summary>
    public event Action<EventSnapshot>? InputPressed;

    /// <summary>Occurs when ArrowDown or ArrowUp on a chip or on the trigger asks for the open, Base UI's <c>list-navigation</c>.</summary>
    public event Action<EventSnapshot>? OpenRequested;

    /// <summary>Occurs when the trigger lost the focus to an element outside the popup.</summary>
    public event Action<EventSnapshot>? Blurred;

    /// <summary>Occurs when a chip's keydown or mousedown is reported, on Server, for its declined consumer handler.</summary>
    public event Action<EventSnapshot>? ChipEventReported;

    /// <summary>Gets or sets the C# chain of the <c>clear</c> and <c>remove</c> points on WebAssembly.</summary>
    /// <value>A function from the point and the event to the verdict, or <see langword="null"/> for each point's fallback.</value>
    public Func<string, EventSnapshot, DecisionPolicy>? Decider { get; set; }

    /// <summary>Receives a classified change of the input text.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="ComboboxInputChange"/>.</param>
    [JSInvokable]
    public void OnInputChange(string payloadJson) => InputChanged?.Invoke(Deserialize(payloadJson, BlazixJsonContext.Default.ComboboxInputChange));

    /// <summary>Receives a clear the module started.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="ComboboxRequest"/>.</param>
    [JSInvokable]
    public void OnClear(string payloadJson) => ClearRequested?.Invoke(Deserialize(payloadJson, BlazixJsonContext.Default.ComboboxRequest));

    /// <summary>Receives a removal the module started.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="ComboboxRequest"/>.</param>
    [JSInvokable]
    public void OnRemove(string payloadJson) => RemoveRequested?.Invoke(Deserialize(payloadJson, BlazixJsonContext.Default.ComboboxRequest));

    /// <summary>Receives the end of a trigger press outside its bounds.</summary>
    /// <param name="payloadJson">The JSON of an <see cref="EventSnapshot"/>.</param>
    [JSInvokable]
    public void OnCancelOpen(string payloadJson) => OpenCanceled?.Invoke(Deserialize(payloadJson, BlazixJsonContext.Default.EventSnapshot));

    /// <summary>Receives a press of the internal dismiss button beside the handle's element.</summary>
    /// <param name="payloadJson">The JSON of an <see cref="EventSnapshot"/>.</param>
    [JSInvokable]
    public void OnDismiss(string payloadJson) => DismissPressed?.Invoke(Deserialize(payloadJson, BlazixJsonContext.Default.EventSnapshot));

    /// <summary>Receives the keydown Enter's synthetic click on the highlighted item is attributed to.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="ComboboxKeyReport"/>.</param>
    [JSInvokable]
    public void OnSelectionEvent(string payloadJson) => SelectionEventReported?.Invoke(Deserialize(payloadJson, BlazixJsonContext.Default.ComboboxKeyReport).Event);

    /// <summary>Receives a press inside a link of an item before its click is dispatched, and its end after it.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="ComboboxLinkPress"/>.</param>
    [JSInvokable]
    public void OnItemLinkPress(string payloadJson) => ItemLinkPressed?.Invoke(Deserialize(payloadJson, BlazixJsonContext.Default.ComboboxLinkPress).Href);

    /// <summary>Receives Enter on an open list without a highlight.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="ComboboxKeyReport"/>.</param>
    [JSInvokable]
    public void OnEnterClose(string payloadJson) => EnterCloseRequested?.Invoke(Deserialize(payloadJson, BlazixJsonContext.Default.ComboboxKeyReport).Event ?? EventSnapshot.CreatePlaceholder());

    /// <summary>Receives a press on the input group or the chips that focused the input.</summary>
    /// <param name="payloadJson">The JSON of an <see cref="EventSnapshot"/>.</param>
    [JSInvokable]
    public void OnInputPress(string payloadJson) => InputPressed?.Invoke(Deserialize(payloadJson, BlazixJsonContext.Default.EventSnapshot));

    /// <summary>Receives ArrowDown or ArrowUp on a chip or on the trigger.</summary>
    /// <param name="payloadJson">The JSON of an <see cref="EventSnapshot"/>.</param>
    [JSInvokable]
    public void OnOpenRequest(string payloadJson) => OpenRequested?.Invoke(Deserialize(payloadJson, BlazixJsonContext.Default.EventSnapshot));

    /// <summary>Receives a key the input's handle handled and stopped without a synchronous decision, for the consumer's <c>onKeyDown</c>.</summary>
    /// <param name="payloadJson">The JSON of an <see cref="EventSnapshot"/>.</param>
    [JSInvokable]
    public void OnKeyHandled(string payloadJson) => KeyHandled?.Invoke(Deserialize(payloadJson, BlazixJsonContext.Default.EventSnapshot));

    /// <summary>Receives the trigger's loss of focus to an element outside the popup.</summary>
    /// <param name="payloadJson">The JSON of an <see cref="EventSnapshot"/>.</param>
    [JSInvokable]
    public void OnBlur(string payloadJson) => Blurred?.Invoke(Deserialize(payloadJson, BlazixJsonContext.Default.EventSnapshot));

    /// <summary>Receives a chip's keydown or mousedown, which C# did not decide inside the dispatch.</summary>
    /// <param name="payloadJson">The JSON of an <see cref="EventSnapshot"/>.</param>
    [JSInvokable]
    public void OnChipEvent(string payloadJson) => ChipEventReported?.Invoke(Deserialize(payloadJson, BlazixJsonContext.Default.EventSnapshot));

    /// <inheritdoc />
    /// <remarks>
    /// The <see cref="ClearPoint"/>, <see cref="RemovePoint"/>, <see cref="NavigatePoint"/> and <see cref="PressPoint"/> points
    /// are answered by <see cref="Decider"/> on WebAssembly;
    /// without one, or for any other point, the verdict is <see cref="DecisionPolicy.Run"/>, and the module applies the
    /// point's fallback on Server.
    /// </remarks>
    protected override DecisionPolicy OnDecide(string point, EventSnapshot snapshot) => Decider is { } decider ? decider(point, snapshot) : DecisionPolicy.Run;
}

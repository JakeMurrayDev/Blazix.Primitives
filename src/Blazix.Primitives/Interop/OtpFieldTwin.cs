using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

// ADR 04 §1 (the `otp-field` component module) and §3 (decision points; Amendments 2026-11-18, `edit` with fallback run;
// Amendments 2026-11-19, design points 1 (a) and 2 (a), chosen by the user 2026-11-19: `key`, `press`, `focus` and `blur`,
// fallback run, and the edit's synchronous handover before `edit`); issue 278. One twin per part: OtpFieldRoot's, on the
// Root element (design point 6 (a)).
/// <summary>
/// The C# half of the <c>otp-field</c> module: the slots' values, the tab stop and the focus moves C# asks for, and the
/// reports of each edit, focus and blur, attached to the element an <see cref="OtpFieldRoot"/> renders.
/// </summary>
/// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly application.</param>
internal sealed class OtpFieldTwin(IJSRuntime jsRuntime)
    : TwinBase<OtpFieldTwin, OtpFieldOptions>(jsRuntime, LibraryModule("otp-field"), BlazixJsonContext.Default.OtpFieldOptions)
{
    /// <summary>Represents the name of the decision point the module asks about before an edit runs.</summary>
    public const string EditPoint = "edit";

    /// <summary>Represents the name of the decision point the module asks about before a navigation key moves focus.</summary>
    public const string KeyPoint = "key";

    /// <summary>Represents the name of the decision point the module asks about before a slot's mousedown focuses it.</summary>
    public const string PressPoint = "press";

    /// <summary>Represents the name of the decision point the module asks about before it reports a focus.</summary>
    public const string FocusPoint = "focus";

    /// <summary>Represents the name of the decision point the module asks about before it reports a blur.</summary>
    public const string BlurPoint = "blur";

    /// <summary>Occurs when an edit reached C# after the dispatch (on Server, or on WebAssembly without a decision).</summary>
    public event Action<OtpFieldEditReport>? Edited;

    /// <summary>Occurs when a slot gains focus and neither a listener nor a decision vetoed it.</summary>
    public event Action<OtpFieldFocusReport>? SlotFocused;

    /// <summary>Occurs when a slot loses focus and neither a listener nor a decision vetoed it.</summary>
    public event Action<OtpFieldBlurReport>? SlotBlurred;

    /// <summary>Occurs when reading a paste's clipboard text threw.</summary>
    public event Action<OtpFieldPasteUnreadableReport>? PasteUnreadable;

    /// <summary>Gets the edit the module handed over for the <c>edit</c> decision in flight, or <see langword="null"/>.</summary>
    public OtpFieldEditReport? PendingEdit { get; private set; }

    /// <summary>Gets or sets the C# chain of the <c>edit</c> point: the consumer's handler, then the request.</summary>
    /// <value>A function that returns <see cref="DecisionPolicy.Stop"/> when a consumer vetoed the edit; <see langword="null"/> runs.</value>
    public Func<EventSnapshot, DecisionPolicy>? EditDecider { get; set; }

    /// <summary>Gets or sets the C# handler chain of the <c>key</c> point.</summary>
    /// <value>A function that returns <see cref="DecisionPolicy.Stop"/> when a consumer vetoed the key; <see langword="null"/> runs.</value>
    public Func<EventSnapshot, DecisionPolicy>? KeyDecider { get; set; }

    /// <summary>Gets or sets the C# handler chain of the <c>press</c> point.</summary>
    /// <value>A function that returns <see cref="DecisionPolicy.Stop"/> when a consumer vetoed the press; <see langword="null"/> runs.</value>
    public Func<EventSnapshot, DecisionPolicy>? PressDecider { get; set; }

    /// <summary>Gets or sets the C# handler chain of the <c>focus</c> point.</summary>
    /// <value>A function that returns <see cref="DecisionPolicy.Stop"/> when a consumer vetoed the focus; <see langword="null"/> runs.</value>
    public Func<EventSnapshot, DecisionPolicy>? FocusDecider { get; set; }

    /// <summary>Gets or sets the C# handler chain of the <c>blur</c> point.</summary>
    /// <value>A function that returns <see cref="DecisionPolicy.Stop"/> when a consumer vetoed the blur; <see langword="null"/> runs.</value>
    public Func<EventSnapshot, DecisionPolicy>? BlurDecider { get; set; }

    /// <summary>Receives an edit after the dispatch.</summary>
    /// <param name="payloadJson">The JSON of an <see cref="OtpFieldEditReport"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnEdit(string payloadJson) => Edited?.Invoke(Deserialize(payloadJson, BlazixJsonContext.Default.OtpFieldEditReport));

    // Design point 2 (a): on WebAssembly the module hands the edit over synchronously, then asks `edit`, whose chain runs it.
    /// <summary>Receives the edit the module is about to ask the <c>edit</c> point about.</summary>
    /// <param name="payloadJson">The JSON of an <see cref="OtpFieldEditReport"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnEditHandover(string payloadJson) => PendingEdit = Deserialize(payloadJson, BlazixJsonContext.Default.OtpFieldEditReport);

    /// <summary>Receives a focus of a slot.</summary>
    /// <param name="payloadJson">The JSON of an <see cref="OtpFieldFocusReport"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnFocus(string payloadJson) => SlotFocused?.Invoke(Deserialize(payloadJson, BlazixJsonContext.Default.OtpFieldFocusReport));

    /// <summary>Receives a blur of a slot.</summary>
    /// <param name="payloadJson">The JSON of an <see cref="OtpFieldBlurReport"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnBlur(string payloadJson) => SlotBlurred?.Invoke(Deserialize(payloadJson, BlazixJsonContext.Default.OtpFieldBlurReport));

    /// <summary>Receives a paste whose clipboard text could not be read.</summary>
    /// <param name="payloadJson">The JSON of an <see cref="OtpFieldPasteUnreadableReport"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnPasteUnreadable(string payloadJson) => PasteUnreadable?.Invoke(Deserialize(payloadJson, BlazixJsonContext.Default.OtpFieldPasteUnreadableReport));

    /// <inheritdoc />
    protected override DecisionPolicy OnDecide(string point, EventSnapshot snapshot)
    {
        if (point == EditPoint)
        {
            try
            {
                return EditDecider is null ? DecisionPolicy.Run : EditDecider(snapshot);
            }
            finally
            {
                PendingEdit = null;
            }
        }

        Func<EventSnapshot, DecisionPolicy>? decider = point switch
        {
            KeyPoint => KeyDecider,
            PressPoint => PressDecider,
            FocusPoint => FocusDecider,
            BlurPoint => BlurDecider,
            _ => null,
        };

        return decider is null ? DecisionPolicy.Run : decider(snapshot);
    }
}

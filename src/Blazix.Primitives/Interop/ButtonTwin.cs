using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

// ADR 04 §1 (button row), §3 (decision points: button activation, fallback "run"; the hidden-input toggle, fallback
// "run", ADR 04 Amendments 2026-10-22, pending the user); ADR 01 relaxation table (UBT-KEY-03/04/06; the hidden input's
// checked after a cancelled toggle); button-behavior.md (UBT). One twin per part: the callbacks carry no element key.
/// <summary>
/// The C# half of the <c>button</c> module: keyboard activation, press suppression, pointer-type classification, the
/// press-and-hold repeater and the bridge to a hidden input, attached to the element a button-like part renders.
/// </summary>
/// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly application.</param>
internal sealed class ButtonTwin(IJSRuntime jsRuntime)
    : TwinBase<ButtonTwin, ButtonOptions>(jsRuntime, LibraryModule("button"), BlazixJsonContext.Default.ButtonOptions)
{
    /// <summary>Represents the name of the decision point the module asks about before a keyboard activation.</summary>
    public const string ActivatePoint = "activate";

    /// <summary>Represents the name of the decision point the module asks about when the bridged hidden input is clicked.</summary>
    public const string TogglePoint = "toggle";

    /// <summary>Occurs when the bridged hidden input was toggled and the toggle was not decided synchronously.</summary>
    /// <remarks>The argument carries the input's new <c>checked</c> property and the click it received.</remarks>
    public event Action<ButtonToggle>? Toggled;

    /// <summary>Occurs when the module classified a <c>pointerdown</c> or a <c>click</c>, if the options ask for it.</summary>
    public event Action<ButtonInteraction>? InteractionReported;

    /// <summary>Occurs on every tick of a press-and-hold sequence, with the event that started it.</summary>
    public event Action<EventSnapshot>? HoldTicked;

    /// <summary>Occurs once when a started press-and-hold sequence is released, with the <c>pointerup</c>.</summary>
    public event Action<EventSnapshot>? HoldStopped;

    /// <summary>Occurs when a click on a press-and-hold button performs a single action (a keyboard click or a tap).</summary>
    public event Action<EventSnapshot>? HoldClicked;

    /// <summary>Occurs when the development check finds the element not matching the declared mode.</summary>
    /// <remarks>The argument is the declared mode: <see langword="true"/> for native.</remarks>
    public event Action<bool>? NativeButtonMismatched;

    /// <summary>Gets or sets the C# handler chain of the activation decision point.</summary>
    /// <value>
    /// A function that receives the <c>keydown</c> or <c>keyup</c> about to activate the element and returns
    /// <see cref="DecisionPolicy.Run"/> to let it, or <see cref="DecisionPolicy.Stop"/> when a consumer called
    /// <c>PreventPartHandler()</c>; <see langword="null"/> always lets it. It runs only on WebAssembly, synchronously
    /// inside the dispatch, and must not await.
    /// </value>
    public Func<EventSnapshot, DecisionPolicy>? ActivationDecider { get; set; }

    /// <summary>Gets or sets the C# request of the toggle decision point.</summary>
    /// <value>
    /// A function that receives the click the bridged hidden input received and returns <see cref="DecisionPolicy.Run"/>
    /// to keep the browser's toggle, or <see cref="DecisionPolicy.Stop"/> to revert it; <see langword="null"/> always
    /// keeps it. It runs only on WebAssembly, synchronously inside the dispatch, and must not await; elsewhere the toggle
    /// arrives through <see cref="Toggled"/>.
    /// </value>
    public Func<EventSnapshot, DecisionPolicy>? ToggleDecider { get; set; }

    /// <summary>Receives the module's classification of a press.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="ButtonInteraction"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnInteraction(string payloadJson)
    {
        ButtonInteraction interaction = Deserialize(payloadJson, BlazixJsonContext.Default.ButtonInteraction);
        InteractionReported?.Invoke(interaction);
    }

    /// <summary>Receives one tick of a press-and-hold sequence.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="ButtonHoldEvent"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnHoldTick(string payloadJson)
    {
        ButtonHoldEvent tick = Deserialize(payloadJson, BlazixJsonContext.Default.ButtonHoldEvent);
        HoldTicked?.Invoke(tick.Event);
    }

    /// <summary>Receives the release of a press-and-hold sequence.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="ButtonHoldEvent"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnHoldStop(string payloadJson)
    {
        ButtonHoldEvent release = Deserialize(payloadJson, BlazixJsonContext.Default.ButtonHoldEvent);
        HoldStopped?.Invoke(release.Event);
    }

    /// <summary>Receives a click that performs a single action on a press-and-hold button.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="ButtonHoldEvent"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnHoldClick(string payloadJson)
    {
        ButtonHoldEvent click = Deserialize(payloadJson, BlazixJsonContext.Default.ButtonHoldEvent);
        HoldClicked?.Invoke(click.Event);
    }

    /// <summary>Receives a toggle of the bridged hidden input.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="ButtonToggle"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnToggle(string payloadJson)
    {
        ButtonToggle toggle = Deserialize(payloadJson, BlazixJsonContext.Default.ButtonToggle);
        Toggled?.Invoke(toggle);
    }

    /// <summary>Receives the result of the development check of the declared mode.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="NativeButtonMismatch"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnNativeButtonMismatch(string payloadJson)
    {
        NativeButtonMismatch mismatch = Deserialize(payloadJson, BlazixJsonContext.Default.NativeButtonMismatch);
        NativeButtonMismatched?.Invoke(mismatch.ExpectedNative);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The module has two decision points, <see cref="ActivatePoint"/> and <see cref="TogglePoint"/>; a point without a
    /// decider, and any other point, answers <see cref="DecisionPolicy.Run"/>.
    /// </remarks>
    protected override DecisionPolicy OnDecide(string point, EventSnapshot snapshot)
    {
        Func<EventSnapshot, DecisionPolicy>? decider = point switch
        {
            ActivatePoint => ActivationDecider,
            TogglePoint => ToggleDecider,
            _ => null,
        };
        return decider is null ? DecisionPolicy.Run : decider(snapshot);
    }
}

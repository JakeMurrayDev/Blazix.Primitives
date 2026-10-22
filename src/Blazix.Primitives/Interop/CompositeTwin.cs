using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

// ADR 04 §1 (composite row), §3 (decision points: the consumer's onKeyDown veto against composite key handling, EVD-EVT-26,
// and the focus-driven selection, ADR 04 Amendments 2026-10-20, both with fallback run; their names navigate and select were
// chosen by the user 2026-10-22, ADR 04 Amendments 2026-10-22) and Amendments 2026-10-22 (the module owns the items' tabindex); ADR 01
// relaxation table (EVD-EVT-26 against composite key handling, CMP-STA-10); composite-and-list-navigation.md (CMP). One twin
// per part: the callbacks carry no element key.
/// <summary>
/// The C# half of the <c>composite</c> module: the roving group of one root element. The module keeps the items in
/// document order, writes their <c>tabindex</c>, moves the highlight and the focus with the arrow keys, and reports the
/// highlighted index and focus leaving the root.
/// </summary>
/// <param name="jsRuntime">The runtime of the part's circuit or WebAssembly application.</param>
internal sealed class CompositeTwin(IJSRuntime jsRuntime)
    : TwinBase<CompositeTwin, CompositeOptions>(jsRuntime, LibraryModule("composite"), BlazixJsonContext.Default.CompositeOptions)
{
    /// <summary>Represents the name of the decision point the module asks about before it handles a navigation key.</summary>
    public const string NavigatePoint = "navigate";

    /// <summary>Represents the name of the decision point the module asks about before it selects a focused item.</summary>
    public const string SelectPoint = "select";

    /// <summary>Occurs when the highlighted index changed, if the options ask for it, once per change.</summary>
    public event Action<int>? HighlightChanged;

    /// <summary>Occurs when focus left the root, if the options ask for it, with the <c>focusout</c> event.</summary>
    public event Action<EventSnapshot>? FocusLeft;

    /// <summary>
    /// Occurs when the module handled a key and stopped its propagation without running the navigation chain inside the
    /// dispatch, if the options ask for it, with the <c>keydown</c> event.
    /// </summary>
    public event Action<EventSnapshot>? KeyHandled;

    /// <summary>Gets or sets the C# handler chain of the navigation decision point.</summary>
    /// <value>
    /// A function that receives the <c>keydown</c> the module is about to handle and returns
    /// <see cref="DecisionPolicy.Run"/> to let it, or <see cref="DecisionPolicy.Stop"/> when a consumer called
    /// <c>PreventPartHandler()</c>; <see langword="null"/> always lets it. It runs only on WebAssembly, synchronously inside
    /// the dispatch, and must not await.
    /// </value>
    public Func<EventSnapshot, DecisionPolicy>? NavigationDecider { get; set; }

    /// <summary>Gets or sets the C# handler chain of the selection decision point.</summary>
    /// <value>
    /// A function that receives the <c>focusin</c> of the item the module is about to select and returns
    /// <see cref="DecisionPolicy.Run"/> to let it, or <see cref="DecisionPolicy.Stop"/> when a consumer called
    /// <c>PreventPartHandler()</c>; <see langword="null"/> always lets it. It runs only on WebAssembly, synchronously inside
    /// the dispatch, and must not await.
    /// </value>
    public Func<EventSnapshot, DecisionPolicy>? SelectionDecider { get; set; }

    /// <summary>Receives a change of the highlighted index.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="CompositeHighlightChange"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnHighlightChange(string payloadJson)
    {
        CompositeHighlightChange change = Deserialize(payloadJson, BlazixJsonContext.Default.CompositeHighlightChange);
        HighlightChanged?.Invoke(change.Index);
    }

    /// <summary>Receives focus leaving the root.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="CompositeFocusLeave"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnFocusLeave(string payloadJson)
    {
        CompositeFocusLeave leave = Deserialize(payloadJson, BlazixJsonContext.Default.CompositeFocusLeave);
        FocusLeft?.Invoke(leave.Event);
    }

    /// <summary>Receives a key the module handled and stopped without running the navigation chain inside the dispatch.</summary>
    /// <param name="payloadJson">The JSON of a <see cref="CompositeKeyHandled"/>.</param>
    /// <exception cref="System.Text.Json.JsonException">The payload is not valid.</exception>
    [JSInvokable]
    public void OnKeyHandled(string payloadJson)
    {
        CompositeKeyHandled handled = Deserialize(payloadJson, BlazixJsonContext.Default.CompositeKeyHandled);
        KeyHandled?.Invoke(handled.Event);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The module has two decision points, <see cref="NavigatePoint"/> and <see cref="SelectPoint"/>; any other point, or a
    /// point without a decider, answers <see cref="DecisionPolicy.Run"/>.
    /// </remarks>
    protected override DecisionPolicy OnDecide(string point, EventSnapshot snapshot)
    {
        Func<EventSnapshot, DecisionPolicy>? decider = point switch
        {
            NavigatePoint => NavigationDecider,
            SelectPoint => SelectionDecider,
            _ => null,
        };
        return decider is null ? DecisionPolicy.Run : decider(snapshot);
    }
}

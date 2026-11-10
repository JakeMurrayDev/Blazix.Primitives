using Microsoft.JSInterop;

namespace Blazix.Primitives.Interop;

// M5 track 12's design point 1 (a), settled by the orchestrator 2026-11-11: the `menu-filter` component module (a dated ADR 04 §1 row on
// the `combobox` precedent). Its handles: the Input (composition, value writes, the key router over Base UI's
// getMenuFilterKeyAction table, the consumer veto, data-highlighted, the WebKit aria-selected of design point 5 (c)), the
// Trigger (the relay, MNU-KEY-28), the List (the mousedown default, the key replay onto the Input, the item texts) and the
// Popup (the background mousedown, the pointer refocus, the cross-axis close key). Design point 6 (a), settled by the orchestrator 2026-11-11: `navigate`
// is the consumer's onKeyDown veto on the Input and the Trigger's relay, the shape the user chose for the `combobox` twin
// (ADR 04 Amendments 2026-11-10). The reports are forwarded as events; the parts' handlers land with their red-first commits.
/// <summary>Represents the C# half of the <c>menu-filter</c> module.</summary>
internal sealed class MenuFilterTwin(IJSRuntime jsRuntime)
    : TwinBase<MenuFilterTwin, MenuFilterOptions>(jsRuntime, LibraryModule("menu-filter"), BlazixJsonContext.Default.MenuFilterOptions)
{
    /// <summary>The decision point asked before the module handles a key: the consumer's key veto.</summary>
    public const string NavigatePoint = "navigate";

    /// <summary>The part name of the Input's handle.</summary>
    public const string InputPart = "input";

    /// <summary>The part name of the Trigger's relay handle.</summary>
    public const string TriggerPart = "trigger";

    /// <summary>The part name of the List's handle.</summary>
    public const string ListPart = "list";

    /// <summary>The part name of the Popup's handle.</summary>
    public const string PopupPart = "popup";

    /// <summary>Occurs when the input reported a change of the query.</summary>
    public event Action<MenuFilterValueChange>? ValueChanged;

    /// <summary>Occurs when the list reported the rendered texts of items.</summary>
    public event Action<MenuFilterTexts>? TextsChanged;

    /// <summary>Occurs when the module changed the input's focus ring.</summary>
    public event Action<bool>? HighlightedChanged;

    /// <summary>Occurs when the input reported a focus-out close.</summary>
    public event Action<MenuFilterFocusOut>? FocusedOut;

    /// <summary>Gets or sets the chain the <c>navigate</c> decision runs on WebAssembly: the consumer's key handler.</summary>
    public Func<EventSnapshot, DecisionPolicy>? NavigationDecider { get; set; }

    /// <summary>Receives a change of the query.</summary>
    /// <param name="payloadJson">The <see cref="MenuFilterValueChange"/> JSON.</param>
    [JSInvokable]
    public void OnValueChange(string payloadJson) => ValueChanged?.Invoke(Deserialize(payloadJson, BlazixJsonContext.Default.MenuFilterValueChange));

    /// <summary>Receives the rendered texts of items.</summary>
    /// <param name="payloadJson">The <see cref="MenuFilterTexts"/> JSON.</param>
    [JSInvokable]
    public void OnTexts(string payloadJson) => TextsChanged?.Invoke(Deserialize(payloadJson, BlazixJsonContext.Default.MenuFilterTexts));

    /// <summary>Receives a change of the input's focus ring.</summary>
    /// <param name="payloadJson">The <see cref="MenuFilterHighlightedChange"/> JSON.</param>
    [JSInvokable]
    public void OnHighlightedChange(string payloadJson) => HighlightedChanged?.Invoke(Deserialize(payloadJson, BlazixJsonContext.Default.MenuFilterHighlightedChange).Highlighted);

    /// <summary>Receives a focus-out close.</summary>
    /// <param name="payloadJson">The <see cref="MenuFilterFocusOut"/> JSON.</param>
    [JSInvokable]
    public void OnFocusOut(string payloadJson) => FocusedOut?.Invoke(Deserialize(payloadJson, BlazixJsonContext.Default.MenuFilterFocusOut));

    /// <inheritdoc />
    protected override DecisionPolicy OnDecide(string point, EventSnapshot snapshot) =>
        string.Equals(point, NavigatePoint, StringComparison.Ordinal) && NavigationDecider is { } decider ? decider(snapshot) : DecisionPolicy.Run;
}

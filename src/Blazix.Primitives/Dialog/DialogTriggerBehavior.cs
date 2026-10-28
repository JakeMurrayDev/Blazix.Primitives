using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// The trigger half every dialog-family trigger composes (dialog/trigger/DialogTrigger.tsx; ADR 08 Amendments 2026-10-27, the
// Input/FieldControl precedent). Three behaviours on the trigger's own element (issue 115: the bag's id is the trigger id):
// - PopupTriggerBehavior: the store (the handle's, else the root's), registration by id at parameter set, the payload,
//   aria-expanded, aria-haspopup="dialog", aria-controls (DLG-ARIA-01..03, PSH-STR-02);
// - ButtonBehavior in the CollapsibleTrigger configuration (DLG §11: native, no focusable-when-disabled, not composite);
// - the `interactions` trigger handle, which decides the click through `press` (ADR 04 Amendments 2026-10-27, chosen by
//   the user 2026-10-27; FLT-ALG-16 with useClick's defaults: click, toggle, stick-if-open, trigger-press). The press's
//   interaction type is the open type (DLG-STA-08, PSH-PTR-01/02), written before the request while closed.
// A disabled trigger asks no press (UBT-PTR-01 blocks the click; DLG-API-16).
/// <summary>The behavior of a dialog-family trigger: its registration, its button and its press.</summary>
/// <typeparam name="TPayload">The type of the payload the trigger carries.</typeparam>
internal sealed class DialogTriggerBehavior<TPayload> : IAsyncDisposable
{
    private static readonly InteractionsClickOptions s_click = new();

    private readonly PopupTriggerBehavior<DialogStore<TPayload>, DialogStoreState<TPayload>, TPayload> trigger;
    private readonly ButtonBehavior button;
    private readonly InteractionsTriggerBehavior interactions;
    private readonly string missingRootMessage;
    private InteractionsTriggerOptions? options;
    private string? optionsId;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="DialogTriggerBehavior{TPayload}"/> class.</summary>
    /// <param name="owner">The trigger part, whose identity registers in the store.</param>
    /// <param name="partName">The part's name as a consumer writes it, such as <c>DialogTrigger</c>.</param>
    /// <param name="rootName">The family root's name, such as <c>DialogRoot</c>.</param>
    /// <param name="jsRuntime">The runtime the twins import their modules through.</param>
    /// <param name="logger">The part's logger.</param>
    /// <param name="developmentLog">The button mechanism's development log.</param>
    /// <param name="idGenerator">The scoped id generator.</param>
    /// <param name="requestRender">Asks the part to render, its <c>StateHasChanged</c>.</param>
    /// <param name="dispatchException">Routes an exception to the part.</param>
    /// <param name="onKeyDown">The part's own <c>onkeydown</c> handler.</param>
    /// <param name="onKeyUp">The part's own <c>onkeyup</c> handler.</param>
    /// <param name="onClick">The part's own <c>onclick</c> handler, which calls <see cref="HandleClickAsync"/>.</param>
    public DialogTriggerBehavior(
        object owner,
        string partName,
        string rootName,
        IJSRuntime jsRuntime,
        ILogger logger,
        ButtonDevelopmentLog developmentLog,
        PartIdGenerator idGenerator,
        Action requestRender,
        Func<Exception, Task> dispatchException,
        EventCallback onKeyDown,
        EventCallback onKeyUp,
        EventCallback onClick)
    {
        trigger = new(owner, partName, requestRender);
        button = new ButtonBehavior(jsRuntime, logger, developmentLog, idGenerator, requestRender, dispatchException, onKeyDown, onKeyUp);
        interactions = new InteractionsTriggerBehavior(jsRuntime, RequestAsync, dispatchException, onClick);

        // DLG-EDGE-01, thrown in every build.
        missingRootMessage = $"Blazix: `<{partName}>` must be used within `<{rootName}>` or provided with a handle.";
    }

    /// <summary>Gets a value indicating whether the dialog is open and this trigger owns it.</summary>
    public bool IsOpenedByTrigger => trigger.IsOpenedByTrigger;

    /// <summary>Gets a task that completes once the button and the press handles have attached.</summary>
    public Task TwinAttached => Task.WhenAll(button.TwinAttached, interactions.TwinAttached);

    /// <summary>Attaches the button and press handles and re-registers the trigger after the render.</summary>
    /// <param name="enabled">Whether the part rendered its element.</param>
    /// <param name="element">The trigger's element, or <see langword="null"/> under a substitute callback.</param>
    /// <param name="id">The id the trigger rendered.</param>
    /// <param name="disabled">The trigger's <c>Disabled</c>.</param>
    /// <returns>A task that completes when the handles have their options and the batch boundary has run.</returns>
    /// <remarks>Call it from the part's <c>OnAfterRenderAsync</c>.</remarks>
    public async Task OnAfterRenderAsync(bool enabled, ElementReference? element, string? id, bool disabled)
    {
        button.Disabled = disabled;
        Task pressed = interactions.AfterRenderAsync(enabled, element, id);
        await button.AfterRenderAsync(enabled, element, id, composite: false);
        await pressed;
        await trigger.OnAfterRenderAsync(element);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        trigger.Dispose();
        button.Dispose();
        await interactions.DisposeAsync();
    }

    /// <summary>Resolves the store, registers the trigger and configures its button and press, at each parameter set.</summary>
    /// <param name="cascadedStore">The store of the enclosing root, or <see langword="null"/>.</param>
    /// <param name="handle">The trigger's <c>Handle</c>, or <see langword="null"/>.</param>
    /// <param name="id">The id the trigger renders.</param>
    /// <param name="element">The trigger's element from its last render.</param>
    /// <param name="payloadSupplied">Whether the trigger's <c>Payload</c> was supplied.</param>
    /// <param name="payload">The trigger's <c>Payload</c>.</param>
    /// <param name="nativeButton">The trigger's <c>NativeButton</c>.</param>
    /// <param name="disabled">The trigger's <c>Disabled</c>.</param>
    /// <param name="additionalAttributes">The part's unmatched attributes.</param>
    /// <exception cref="InvalidOperationException">There is neither an enclosing root nor a handle.</exception>
    public void Apply(DialogStore<TPayload>? cascadedStore, DialogHandle<TPayload>? handle, string? id, ElementReference? element, bool payloadSupplied, TPayload? payload, bool nativeButton, bool disabled, IReadOnlyDictionary<string, object>? additionalAttributes)
    {
        trigger.Apply(cascadedStore, handle, id, element, payloadSupplied, payload, missingRootMessage);
        button.Native = nativeButton;
        button.FocusableWhenDisabled = null;
        button.Disabled = disabled;
        button.SetConsumerAttributes(additionalAttributes);
        interactions.SetConsumerAttributes(additionalAttributes);
        interactions.SessionToken = trigger.Store.SessionToken;
        interactions.Options = Options(id, disabled);
    }

    /// <summary>Adds the trigger's attributes: the button's, the marker, the ARIA attributes and the part's <c>onclick</c>.</summary>
    /// <param name="attributes">The part's attributes.</param>
    /// <param name="consumerId">The consumer's <c>id</c>, or <see langword="null"/>.</param>
    /// <param name="disabled">The trigger's <c>Disabled</c> in this render.</param>
    public void AddAttributes(AttributeList attributes, string? consumerId, bool disabled)
    {
        button.Disabled = disabled;
        button.AddAttributes(attributes, consumerId, composite: false);

        // DLG-DOM-02, PSH-DOM-16: always, the focus manager's marker of a press that lands on a trigger.
        attributes.Set(PopupMarkerAttributes.ClickTrigger, string.Empty);
        trigger.AddAttributes(attributes, ariaExpanded: true, ariaHasPopup: DialogStoreState<TPayload>.DialogRole);
        interactions.AddAttributes(attributes);
    }

    /// <summary>Determines whether the consumer's handler for an event is rendered on the element.</summary>
    /// <param name="eventName">The event's attribute name in lowercase.</param>
    /// <returns><see langword="false"/> for a handler the button or the press runs; otherwise <see langword="true"/>.</returns>
    public bool ForwardsConsumerHandler(string eventName) => button.ForwardsConsumerHandler(eventName) && interactions.ForwardsConsumerHandler(eventName);

    /// <summary>Runs the consumer's <c>onclick</c> from the part's own, unless the press already ran it.</summary>
    /// <param name="e">The event arguments.</param>
    /// <returns>The consumer handler's task.</returns>
    public Task HandleClickAsync(MouseEventArgs e) => interactions.HandleClickAsync(e);

    /// <summary>Runs the consumer's <c>onkeydown</c> through the button.</summary>
    /// <param name="e">The event arguments.</param>
    /// <returns>The consumer handler's task.</returns>
    public Task HandleKeyDownAsync(KeyboardEventArgs e) => button.HandleKeyDownAsync(e);

    /// <summary>Runs the consumer's <c>onkeyup</c> through the button.</summary>
    /// <param name="e">The event arguments.</param>
    /// <returns>The consumer handler's task.</returns>
    public Task HandleKeyUpAsync(KeyboardEventArgs e) => button.HandleKeyUpAsync(e);

    // The record is rebuilt only when the id or the disabled state changed (ADR 16 rule 7).
    private InteractionsTriggerOptions Options(string? id, bool disabled)
    {
        bool click = !disabled;
        if (options is not null && string.Equals(optionsId, id, StringComparison.Ordinal) && (options.Click is not null) == click)
        {
            return options;
        }

        optionsId = id;
        options = new InteractionsTriggerOptions { TriggerId = id, Click = click ? s_click : null };
        return options;
    }

    // PSH-PTR-01/02: the press's interaction type is recorded while closed, then the press runs the store's sequence.
    private Task RequestAsync(OpenChangeRequest request)
    {
        DialogStore<TPayload> store = trigger.Store;
        if (request.InteractionType is { } type)
        {
            store.SetOpenInteractionType(type);
        }

        return store.RequestOpenAsync(request.Open, request.Details);
    }
}

using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives;

// combobox/label/ComboboxLabel.tsx (CMB §3.2, STR-10, ARIA-16, FFI-PTR-01): a div whose id is always {rootId}-label (Base
// UI drops a consumer `id`), published to the store for the Trigger's aria-labelledby, with the `form` module's label role,
// Native = false, ControlId the Trigger's (or the root's while the input is inside the popup). The consumer's mousedown, click
// and pointerdown handlers run first: inside the decision point `label` on WebAssembly, after the module on Server (the
// FieldLabel precedent, ADR 01 "C#-origin consumer vetoes"). The development warning while an Input outside the popup is
// registered (STR-10 restored without the owner stack, ADR 12 Amendments 2026-11-03 item (5)). Combobox only.
/// <summary>The behavior of a combobox label: its id, its label role and its development warning.</summary>
internal sealed class ComboboxLabelBehavior : IAsyncDisposable
{
    private const string MouseDownEvent = "onmousedown";

    private const string ClickEvent = "onclick";

    private const string PointerDownEvent = "onpointerdown";

    private readonly ComboboxPartServices services;

    private readonly FieldConsumerHandlers consumerHandlers = new(MouseDownEvent, ClickEvent, PointerDownEvent);

    private readonly Action fieldChanged;

    private ComboboxStore? store;

    private FieldRootContext? fieldScope;

    private FormTwin? twin;

    private TwinHandle<FormOptions>? handle;

    private FormOptions? sentOptions;

    private string? id;

    private string? warnedInputId;

    private DecidedPointer? decided;

    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="ComboboxLabelBehavior"/> class.</summary>
    /// <param name="services">The part's services.</param>
    public ComboboxLabelBehavior(ComboboxPartServices services)
    {
        this.services = services;
        fieldChanged = services.RequestRender;
    }

    /// <summary>Gets the root's store.</summary>
    public ComboboxStore Store => store ?? throw new InvalidOperationException("Blazix: the combobox label is used before its first render.");

    /// <summary>Gets the field's state the label projects.</summary>
    public FieldStateSnapshot Field => fieldScope?.State ?? FieldStateSnapshot.Default;

    /// <summary>Gets the id the label renders: <c>{rootId}-label</c>.</summary>
    public string? Id => id;

    /// <summary>Gets the id of the control the label focuses: the trigger's, else the root's while the input is inside the popup.</summary>
    public string? ControlId
    {
        get
        {
            ComboboxStoreState state = Store.State;
            return state.ElementIds.Trigger ?? (state.InputInsidePopup ? state.Id : null);
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        fieldScope?.RemoveListener(fieldChanged);
        if (store is not null && id is not null)
        {
            string own = id;
            store.SetElementIds(ids => ids.Label == own ? ids with { Label = null } : ids);
        }

        if (twin is not null)
        {
            twin.LabelDecider = null;
            await twin.DisposeAsync();
        }
    }

    /// <summary>Reads the root and the field and subscribes the part.</summary>
    /// <param name="cascaded">The cascaded store, or <see langword="null"/> outside a root.</param>
    /// <param name="field">The cascaded field, or <see langword="null"/>.</param>
    /// <param name="subscribe">The part's subscription factory over the store.</param>
    public void Subscribe(ComboboxStore? cascaded, FieldRootContext? field, Func<ComboboxStore, SelectorSubscription<ComboboxStoreState>> subscribe)
    {
        store = ComboboxParts.RequireRoot(cascaded, services.Family);
        fieldScope = field;
        field?.AddListener(fieldChanged);
        SelectorSubscription<ComboboxStoreState> subscription = subscribe(store);
        subscription.Select(static state => state.Id);
        subscription.Select(ComboboxStoreState.InsidePopup);
        subscription.Select(static state => state.ElementIds.Trigger);
        subscription.Select(static state => state.ElementIds.Input);
    }

    /// <summary>Applies the consumer's handlers and publishes the label's id.</summary>
    /// <param name="consumerAttributes">The consumer's attributes.</param>
    public void Apply(IReadOnlyDictionary<string, object>? consumerAttributes)
    {
        consumerHandlers.Set(consumerAttributes);
        Publish();
    }

    /// <summary>Publishes the label's id when the root's changed, and adds the part's own handlers for the consumer's declined ones.</summary>
    /// <param name="attributes">The part's attributes.</param>
    /// <param name="onMouseDown">The part's own <c>onmousedown</c> handler.</param>
    /// <param name="onClick">The part's own <c>onclick</c> handler.</param>
    /// <param name="onPointerDown">The part's own <c>onpointerdown</c> handler.</param>
    public void AddPartAttributes(AttributeList attributes, EventCallback onMouseDown, EventCallback onClick, EventCallback onPointerDown)
    {
        // A root id change renders the label through its subscription, with no parameter set: the id follows here.
        Publish();
        if (consumerHandlers.Has(MouseDownEvent))
        {
            attributes.Set(MouseDownEvent, onMouseDown);
        }

        if (consumerHandlers.Has(ClickEvent))
        {
            attributes.Set(ClickEvent, onClick);
        }

        if (consumerHandlers.Has(PointerDownEvent))
        {
            attributes.Set(PointerDownEvent, onPointerDown);
        }
    }

    /// <summary>Sets the label's id over the consumer's.</summary>
    /// <param name="attributes">The merged attributes.</param>
    public void FinishAttributes(AttributeList attributes) => attributes.Set("id", id);

    /// <summary>Determines whether the consumer's handler for an event renders on the element.</summary>
    /// <param name="eventName">The event's attribute name.</param>
    /// <returns><see langword="false"/> for the pointer handlers the part runs itself.</returns>
    public bool ForwardsConsumerHandler(string eventName) => !consumerHandlers.Declines(eventName);

    /// <summary>Runs a declined consumer handler, unless the label decision already ran it for this event.</summary>
    /// <param name="eventName">The event's attribute name.</param>
    /// <param name="args">The event.</param>
    /// <returns>A task that completes when the handler has run.</returns>
    public Task RunConsumerAsync(string eventName, MouseEventArgs args)
    {
        DecidedPointer? pending = decided;
        decided = null;
        if (pending is { } previous && previous == DecidedPointer.From(args))
        {
            return Task.CompletedTask;
        }

        return consumerHandlers.RunAsync(eventName, args);
    }

    /// <summary>Attaches the label role, sends its changed options and warns once per outside input.</summary>
    /// <param name="element">The label's element, or <see langword="null"/> under a substitute callback.</param>
    /// <returns>A task that completes when the role has its options.</returns>
    public async Task AfterRenderAsync(ElementReference? element)
    {
        if (disposed)
        {
            return;
        }

        // CMB-STR-10 (ComboboxLabel.tsx:40-53): once per input element change, while the input is outside the popup.
        ComboboxStoreState state = Store.State;
        if (state.ElementIds.Input is { } inputId && !state.InputInsidePopup && !string.Equals(inputId, warnedInputId, StringComparison.Ordinal))
        {
            warnedInputId = inputId;
            DevelopmentWarnings.Write(
                services.Logger,
                $"Blazix: `<{services.Family}Label>` labels `<{services.Family}Trigger>` only. When `<{services.Family}Input>` is the form control, use a native `<label>` or `<FieldLabel>` instead.");
        }

        FormOptions options = new()
        {
            Label = new FormLabelOptions { Native = false, ControlId = ControlId, Decides = consumerHandlers.Any, DevelopmentChecks = false },
        };
        if (handle is not null)
        {
            if (options != sentOptions)
            {
                sentOptions = options;
                await handle.UpdateAsync(options);
            }

            return;
        }

        twin ??= new FormTwin(services.JSRuntime) { LabelDecider = Decide };
        sentOptions = options;
        if (element is { } target)
        {
            handle = await twin.AttachAsync(target, options);
        }
        else if (id is not null)
        {
            handle = await twin.AttachAsync(id, options);
        }
    }

    private void Publish()
    {
        ComboboxStore root = Store;
        string next = root.State.Id + "-label";
        if (string.Equals(next, id, StringComparison.Ordinal) && string.Equals(root.State.ElementIds.Label, next, StringComparison.Ordinal))
        {
            return;
        }

        string? previous = id;
        id = next;
        root.SetElementIds(ids => ids.Label is null || ids.Label == previous ? ids with { Label = next } : ids);
    }

    // The label decision point, on WebAssembly only: the consumer's handler runs inside the dispatch, so its
    // PreventPartHandler() stops the module's handling of the event.
    private DecisionPolicy Decide(EventSnapshot snapshot)
    {
        string eventName = "on" + snapshot.Type;
        if (!consumerHandlers.Has(eventName))
        {
            return DecisionPolicy.Run;
        }

        MouseEventArgs args = FieldConsumerHandlers.ToMouseEventArgs(snapshot);
        decided = DecidedPointer.From(args);
        return consumerHandlers.RunSynchronously(eventName, args, services.DispatchException) ? DecisionPolicy.Stop : DecisionPolicy.Run;
    }

    private readonly record struct DecidedPointer(string Type, long Detail, long Buttons, bool AltKey, bool CtrlKey, bool MetaKey, bool ShiftKey)
    {
        public static DecidedPointer From(MouseEventArgs e) => new(e.Type, e.Detail, e.Buttons, e.AltKey, e.CtrlKey, e.MetaKey, e.ShiftKey);
    }
}

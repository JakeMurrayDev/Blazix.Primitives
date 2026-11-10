using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives;

// combobox/input/ComboboxInput.tsx (CMB §3.7, §5.1, §6.1): the text control. Its handles on its own element:
// - `interactions` trigger role, Click { Event = "mousedown-only", Toggle = false, TouchOpenDelay = inside ? 0 : 100,
//   Reason = "input-press" } while !Disabled && OpenOnInputClick (CMB-PTR-01, AriaCombobox.tsx:1384-1393);
// - `composite` list handle, Reference outside the popup (Trigger inside), the virtual focus's aria-activedescendant;
// - `combobox` `input`: composition, the inputType classification, Home/End, Enter's synthetic click, the closed Escape's
//   `clear`, the chip keys, and the `value` writer (ADR 04 Amendments 2026-11-03 (6): C# renders `value` once under the
//   handover, then sends committed values through `update`).
// The start dismiss span renders before the element while open and modal (CMB-STR-07, BuildBeforeElement).
/// <summary>The behavior of a combobox-family input: its ARIA, its handles and its start dismiss button.</summary>
internal sealed class ComboboxInputBehavior : IAsyncDisposable
{
    private const string InputPress = "input-press";

    private const string MouseDownOnly = "mousedown-only";

    private static readonly InteractionsClickOptions s_clickOutside = new() { Event = MouseDownOnly, Toggle = false, TouchOpenDelay = 100, Reason = InputPress };

    private static readonly InteractionsClickOptions s_clickInside = new() { Event = MouseDownOnly, Toggle = false, Reason = InputPress };

    private readonly ComboboxPartServices services;

    private readonly InteractionsTriggerBehavior interactions;

    private readonly CompositeRootBehavior reference;

    private ComboboxStore? store;

    private FieldRootContext? fieldScope;

    private ComboboxTwin? twin;

    private TwinHandle<ComboboxOptions>? handle;

    private ComboboxOptions? sentOptions;

    private string? id;

    private string? renderedValue;

    private string? dismissId;

    private ElementReference? lastElement;

    private bool disabledParameter;

    private bool insidePopup;

    private bool hasPositionerParent;

    private string? consumerId;

    private string? generatedId;

    private bool listRoleSet;

    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="ComboboxInputBehavior"/> class.</summary>
    /// <param name="services">The part's services.</param>
    /// <param name="onKeyDown">The part's own <c>onkeydown</c> handler, which calls <see cref="HandleKeyDownAsync"/>.</param>
    /// <param name="onClick">The part's own <c>onclick</c> handler, which calls <see cref="HandleClickAsync"/>.</param>
    /// <param name="onMouseDown">The part's own <c>onmousedown</c> handler, which calls <see cref="HandleMouseDownAsync"/>.</param>
    public ComboboxInputBehavior(ComboboxPartServices services, EventCallback onKeyDown, EventCallback onClick, EventCallback onMouseDown)
    {
        this.services = services;
        interactions = new InteractionsTriggerBehavior(services.JSRuntime, RouteAsync, services.DispatchException, onClick, onMouseDown);
        reference = new CompositeRootBehavior(services.JSRuntime, services.Ids, services.RequestRender, services.DispatchException, onKeyDown);
    }

    /// <summary>Gets the root's store.</summary>
    public ComboboxStore Store => store ?? throw new InvalidOperationException($"Blazix: `{services.Family}Input` is used before its first render.");

    /// <summary>Gets a value indicating whether the input renders inside the popup.</summary>
    public bool InsidePopup => insidePopup;

    /// <summary>Gets a value indicating whether the input is disabled, by the part, the root or the field.</summary>
    public bool Disabled => disabledParameter || Store.State.Disabled || (fieldScope?.Disabled ?? false);

    /// <summary>Gets the field's state the input projects (the default state inside the popup).</summary>
    public FieldStateSnapshot Field => hasPositionerParent || fieldScope is null ? FieldStateSnapshot.Default with { Disabled = Disabled } : fieldScope.State with { Disabled = Disabled };

    /// <summary>Gets or sets a value indicating whether the reading direction is right to left.</summary>
    public bool Rtl { get; set; }

    /// <summary>Gets the id the input renders.</summary>
    public string? Id => id;

    /// <summary>Gets a task that completes once the input's handles have attached.</summary>
    public Task TwinAttached => Task.WhenAll(interactions.TwinAttached, reference.TwinAttached);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (store is not null && id is not null)
        {
            string own = id;
            store.SetElementIds(ids => ids.Input == own ? ids with { Input = null } : ids);
        }

        if (store is not null)
        {
            store.InputValueCommitted -= OnInputValueCommitted;
        }

        fieldScope?.RemoveListener(services.RequestRender);

        reference.Dispose();
        Task twinDisposal = twin?.DisposeAsync().AsTask() ?? Task.CompletedTask;
        await Task.WhenAll(interactions.DisposeAsync().AsTask(), twinDisposal);
    }

    /// <summary>Reads the root and the optional positioner and field contexts, and subscribes the part.</summary>
    /// <param name="cascaded">The cascaded store, or <see langword="null"/> outside a root.</param>
    /// <param name="positioner">The cascaded combobox positioner context, or <see langword="null"/>.</param>
    /// <param name="fieldContext">The cascaded field, or <see langword="null"/>.</param>
    /// <param name="subscribe">The part's subscription factory over the store.</param>
    public void Subscribe(ComboboxStore? cascaded, ComboboxPositionerContext? positioner, FieldRootContext? fieldContext, Func<ComboboxStore, SelectorSubscription<ComboboxStoreState>> subscribe)
    {
        store = ComboboxParts.RequireRoot(cascaded, services.Family);
        fieldScope = fieldContext;
        fieldScope?.AddListener(services.RequestRender);
        // ComboboxInput.tsx:64,95,98,111: the combobox positioner's context, never another family's; inline counts as inside
        // for the layout, not for the field state or the form value.
        hasPositionerParent = positioner is not null;
        insidePopup = hasPositionerParent || store.State.Inline;
        SelectorSubscription<ComboboxStoreState> subscription = subscribe(store);
        subscription.Select(ComboboxStoreState.Open);
        subscription.Select(ComboboxStoreState.Mounted);
        subscription.Select(static state => state.Expanded);
        subscription.Select(static state => state.Disabled);
        subscription.Select(static state => state.ReadOnly);
        subscription.Select(static state => state.Required);
        subscription.Select(static state => state.Grid);
        subscription.Select(static state => state.Modal);
        subscription.Select(static state => state.Id);
        subscription.Select(static state => state.ElementIds.List);
        subscription.Select(ComboboxStoreState.ListEmpty);
        subscription.Select(static state => state.Lifecycle.Mounted ? state.PopupSide : null);
        store.SetInputInsidePopup(insidePopup);
        twin = new ComboboxTwin(services.JSRuntime);
        twin.InputChanged += change => _ = Observe(services.InvokeAsync(() => Store.HandleInputChangeAsync(change.Text, change.Typed, change.Composing, change.Event)));
        twin.ClearRequested += request => _ = Observe(services.InvokeAsync(() => Store.ClearAsync(EventReason.EscapeKey, request.Event)));
        twin.RemoveRequested += request => _ = Observe(services.InvokeAsync(() => Store.RemoveLastAsync(request.Index, request.Event)));
        twin.SelectionEventReported += snapshot => Store.SetSelectionEvent(snapshot);
        twin.Decider = Decide;

        // ADR 04 Amendments 2026-11-03 (6): a committed text reaches the module's writer without a render of the input.
        store.InputValueCommitted += OnInputValueCommitted;
        twin.EnterCloseRequested += snapshot => _ = Observe(services.InvokeAsync(() => Store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.None, snapshot))));
        twin.DismissPressed += snapshot => _ = Observe(services.InvokeAsync(
            () => Store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.ClosePress, snapshot, new EventTrigger(Store.State.ElementIds.StartDismiss, null)))));
    }

    /// <summary>Applies the part's parameters.</summary>
    /// <param name="disabled">The part's <c>Disabled</c>.</param>
    /// <param name="consumerId">The consumer's id, or <see langword="null"/>.</param>
    /// <param name="generatedId">The part's generated id, used inside the popup.</param>
    /// <param name="consumerAttributes">The consumer's attributes, for the declined handlers.</param>
    public void Apply(bool disabled, string? consumerId, string? generatedId, IReadOnlyDictionary<string, object>? consumerAttributes)
    {
        disabledParameter = disabled;
        this.consumerId = consumerId;
        this.generatedId = generatedId;
        reference.SetConsumerAttributes(consumerAttributes);
        interactions.SetConsumerAttributes(consumerAttributes);
        interactions.SessionToken = Store.SessionToken;
        Resolve();
    }

    /// <summary>Adds the input's attributes: the fixed ones, the combobox ARIA outside the popup and the rendered value.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddPartAttributes(AttributeList attributes)
    {
        // The root's id can change with no parameter set of the input.
        Resolve();
        ComboboxStore root = Store;
        ComboboxStoreState state = root.State;

        // The list handle's part handler for the consumer's declined onkeydown, and its render mark; the press handle's part
        // handlers for the consumer's declined onclick and onmousedown.
        reference.AddAttributes(attributes, id);
        interactions.AddAttributes(attributes);

        // AriaCombobox.tsx:1348-1376: an input-like control (always, on the server and before hydration) takes the four
        // fixed attributes and the combobox ARIA.
        attributes.Set("autocomplete", "off");
        attributes.Set("spellcheck", "false");
        attributes.Set("autocorrect", "off");
        attributes.Set("autocapitalize", "none");
        if (!insidePopup || state.Inline)
        {
            bool expanded = state.Expanded;
            attributes.Set("role", "combobox");
            attributes.Set("aria-expanded", expanded ? "true" : "false");
            attributes.Set("aria-haspopup", state.Grid ? "grid" : "listbox");
            if (expanded && state.ElementIds.List is { } listId)
            {
                attributes.Set("aria-controls", listId);
            }

            attributes.Set("aria-autocomplete", state.ReadOnly ? "none" : AriaAutoCompleteLiteral(root.AriaAutoComplete));
        }

        if (state.ReadOnly)
        {
            attributes.Set("aria-readonly", "true");
        }

        if (state.Required)
        {
            attributes.Set("aria-required", "true");
        }

        if (!insidePopup && fieldScope?.Labelable.Label.Id is { } labelId)
        {
            attributes.Set("aria-labelledby", labelId);
        }

        // CMB-DOM-01: `required` only in no-selection mode (Autocomplete), `name` while the input owns the form value.
        if (state.SelectionMode == ComboboxSelectionMode.None)
        {
            if (state.Required)
            {
                attributes.Set("required", true);
            }

            if (!hasPositionerParent && root.Name is { } name)
            {
                attributes.Set("name", name);
            }
        }

        if (root.Form is { } form)
        {
            attributes.Set("form", form);
        }

        if (Disabled)
        {
            attributes.Set("disabled", true);
        }

        if (state.ReadOnly)
        {
            attributes.Set("readonly", true);
        }

        attributes.Set("id", id);

        // ADR 04 Amendments 2026-11-03 (6): the committed text once, under the handover; the module writes later commits.
        renderedValue ??= state.InputValue;
        attributes.Set("value", renderedValue);
    }

    /// <summary>Determines whether the consumer's handler for an event renders on the element.</summary>
    /// <param name="eventName">The event's attribute name.</param>
    /// <returns><see langword="false"/> for a handler the part declines and runs itself.</returns>
    public bool ForwardsConsumerHandler(string eventName) => reference.ForwardsConsumerHandler(eventName) && interactions.ForwardsConsumerHandler(eventName);

    /// <summary>Runs the declined consumer <c>onkeydown</c> unless a decision already ran it.</summary>
    /// <param name="args">The keydown.</param>
    /// <returns>A task that completes when the handler has run.</returns>
    public Task HandleKeyDownAsync(KeyboardEventArgs args) => reference.HandleKeyDownAsync(args);

    /// <summary>Runs the declined consumer <c>onclick</c> unless the press already ran it.</summary>
    /// <param name="args">The click.</param>
    /// <returns>A task that completes when the handler has run.</returns>
    public Task HandleClickAsync(MouseEventArgs args) => interactions.HandleClickAsync(args);

    /// <summary>Runs the declined consumer <c>onmousedown</c> unless the press already ran it.</summary>
    /// <param name="args">The mousedown.</param>
    /// <returns>A task that completes when the handler has run.</returns>
    public Task HandleMouseDownAsync(MouseEventArgs args) => interactions.HandleMouseDownAsync(args);

    /// <summary>Renders the start dismiss button while the popup is open and the focus manager is modal.</summary>
    /// <param name="builder">The part's render tree builder.</param>
    public void BuildBeforeElement(RenderTreeBuilder builder)
    {
        // CMB-STR-07 (ComboboxInput.tsx:436-443): `open && focusManagerModal`, the modal manager being !inside || modal.
        ComboboxStoreState state = Store.State;
        bool modal = !insidePopup || state.Modal;
        if (state.Lifecycle.EffectiveOpen && modal)
        {
            dismissId ??= services.Ids.Next();
            string own = dismissId;
            Store.SetElementIds(ids => ids with { StartDismiss = own });
            ComboboxDismissButton.Build(builder, own);
        }
    }

    /// <summary>Records the input's focus for the field.</summary>
    /// <param name="focused">Whether the input received or lost the focus.</param>
    public void OnFocusChange(bool focused)
    {
        if (fieldScope is null || insidePopup)
        {
            return;
        }

        // ComboboxInput.tsx onBlur: touched, and a commit in onBlur mode, through the root's field behaviour.
        if (!focused)
        {
            Store.ControlBlurred?.Invoke();
        }

        fieldScope.SetFocused(this, focused, Disabled);
    }

    /// <summary>Attaches the handles and sends their changed options.</summary>
    /// <param name="element">The input's element, or <see langword="null"/> under a substitute callback.</param>
    /// <returns>A task that completes when the handles have their options.</returns>
    public async Task AfterRenderAsync(ElementReference? element)
    {
        if (disposed)
        {
            return;
        }

        Task interactionsSync = interactions.AfterRenderAsync(true, element, id);
        Task referenceSync = reference.AfterRenderAsync(true, element, id);
        lastElement = element;
        Task twinSync = SyncTwinAsync(element);
        await Task.WhenAll(interactionsSync, referenceSync, twinSync);
    }

    private static string AriaAutoCompleteLiteral(ComboboxAriaAutoComplete mode) => mode switch
    {
        ComboboxAriaAutoComplete.Both => "both",
        ComboboxAriaAutoComplete.Inline => "inline",
        ComboboxAriaAutoComplete.None => "none",
        _ => "list",
    };

    private async Task SyncTwinAsync(ElementReference? element)
    {
        if (twin is null || element is not { } target)
        {
            return;
        }

        ComboboxStore root = Store;
        ComboboxStoreState state = root.State;
        ComboboxOptions options = new()
        {
            Part = ComboboxTwin.InputPart,
            InsidePopup = insidePopup,
            Inline = state.Inline,
            Disabled = Disabled,
            ReadOnly = state.ReadOnly,
            Multiple = state.SelectionMode == ComboboxSelectionMode.Multiple,
            HasValue = state.HasSelectedValue,
            Virtualized = state.Virtualized,
            ChipsId = state.ElementIds.Chips,
            ListId = state.ElementIds.List,
            PopupId = state.ElementIds.Popup,
            Value = state.InputValue,
            ValueRevision = state.InputRevision,
            Open = state.Lifecycle.EffectiveOpen,
            Mounted = state.Lifecycle.Mounted,
            Rtl = Rtl,
            InputId = id,
            DismissId = state.ElementIds.StartDismiss,
        };
        if (handle is null)
        {
            sentOptions = options;
            handle = await twin.AttachAsync(target, options);
            return;
        }

        if (options != sentOptions)
        {
            sentOptions = options;
            await handle.UpdateAsync(options);
        }
    }

    // CMB-KEY-05, EVT-09 (ComboboxInput.tsx:347-362): on WebAssembly the closed Escape's clear runs inside the dispatch, and
    // the keydown stops when a value was selected, the root is not inline and the callbacks did not allow propagation.
    // CMB-API-27 (ComboboxInput.tsx:97, derived at every render): the root's id outside the popup; inside, the consumer's or
    // a generated one.
    private void Resolve()
    {
        ComboboxStore root = Store;
        string? next = consumerId ?? (insidePopup ? generatedId : root.State.Id);
        if (!string.Equals(next, id, StringComparison.Ordinal))
        {
            string? previous = id;
            id = next;
            root.SetElementIds(ids => ids.Input is null || ids.Input == previous ? ids with { Input = next } : ids);
        }

        if (!listRoleSet)
        {
            listRoleSet = true;
            reference.SetList(root.ListNavigation, insidePopup ? CompositeListRole.Trigger : CompositeListRole.Reference);
        }

        bool clickEnabled = !Disabled && root.OpenOnInputClick;
        InteractionsClickOptions? click = clickEnabled ? insidePopup ? s_clickInside : s_clickOutside : null;
        if (!string.Equals(interactions.Options.TriggerId, id, StringComparison.Ordinal) || !ReferenceEquals(interactions.Options.Click, click))
        {
            interactions.Options = new InteractionsTriggerOptions { TriggerId = id, Click = click };
        }
    }

    private DecisionPolicy Decide(string point, EventSnapshot snapshot)
    {
        if (!string.Equals(point, ComboboxTwin.ClearPoint, StringComparison.Ordinal))
        {
            return DecisionPolicy.Run;
        }

        ComboboxStore root = Store;
        bool stops = root.State.HasSelectedValue && !root.State.Inline;
        Task<ChangeEventDetails> clearing = root.ClearAsync(EventReason.EscapeKey, snapshot);
        if (!clearing.IsCompleted)
        {
            _ = Observe(clearing);
            return stops ? DecisionPolicy.Stop : DecisionPolicy.Run;
        }

        return stops && !clearing.Result.IsPropagationAllowed ? DecisionPolicy.Stop : DecisionPolicy.Run;
    }

    private void OnInputValueCommitted()
    {
        if (!disposed && handle is not null)
        {
            _ = Observe(SyncTwinAsync(lastElement));
        }
    }

    private Task RouteAsync(OpenChangeRequest request)
    {
        ComboboxStore root = Store;
        if (request.InteractionType is { } type)
        {
            root.SetOpenInteractionType(type);
        }

        return root.RequestOpenAsync(request.Open, request.Details);
    }

    private async Task Observe(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception) when (!task.IsCanceled)
        {
            await services.DispatchException(exception);
        }
    }
}

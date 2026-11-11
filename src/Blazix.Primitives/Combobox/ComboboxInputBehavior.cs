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

    private const string KeyDownEvent = "onkeydown";

    private static readonly InteractionsClickOptions s_clickOutside = new() { Event = MouseDownOnly, Toggle = false, TouchOpenDelay = 100, Reason = InputPress };

    private static readonly InteractionsClickOptions s_clickInside = new() { Event = MouseDownOnly, Toggle = false, Reason = InputPress };

    private readonly ComboboxPartServices services;

    private readonly InteractionsTriggerBehavior interactions;

    private readonly CompositeRootBehavior reference;

    // The wrapper the `composite` reference handle holds as the consumer's onkeydown: it runs the handler at most once per
    // event, sharing the verdict with the `combobox` handle's `navigate` (ADR 04 Amendments 2026-11-10).
    private readonly IReadOnlyDictionary<string, object> keyDownWrapper;

    private AttributeValue consumerKeyDown;

    private KeyEvent? decidedKey;

    private bool decidedVetoed;

    private DispatchedKey? dispatchedKey;

    private bool wrapperSkipsNext;

    private ComboboxStore? store;

    private FieldRootContext? fieldScope;

    private ComboboxTwin? twin;


    private readonly ComboboxHandleSync<ComboboxTwin, ComboboxOptions> sync = new();

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

    // AriaCombobox.tsx:1347-1354: whether the part's substitute is an element other than an <input>, and whether the element
    // has mounted; until it has, the control is taken for an input, as on the server.
    private bool nonInputControl;

    private bool elementMounted;

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
        reference = new CompositeRootBehavior(services.JSRuntime, services.Ids, services.RequestRender, services.DispatchException, onKeyDown)
        {
            NavigationDecider = DecideReferenceKey,
        };
        keyDownWrapper = new Dictionary<string, object>(1, StringComparer.Ordinal)
        {
            [KeyDownEvent] = new EventCallback<KeyboardEventArgs>(null, (Func<KeyboardEventArgs, Task>)RunConsumerKeyDownAsync),
        };
    }

    /// <summary>Gets the root's store.</summary>
    public ComboboxStore Store => store ?? throw new InvalidOperationException($"Blazix: `{services.Family}Input` is used before its first render.");

    /// <summary>Gets a value indicating whether the input renders inside the popup.</summary>
    public bool InsidePopup => insidePopup;

    /// <summary>Gets a value indicating whether the input is disabled, by the part, the root or the field.</summary>
    public bool Disabled => disabledParameter || Store.State.Disabled || (fieldScope?.Disabled ?? false);

    /// <summary>Gets the field's state the input projects (the default state inside the popup).</summary>
    public FieldStateSnapshot Field => hasPositionerParent || fieldScope is null ? FieldStateSnapshot.Default with { Disabled = Disabled } : fieldScope.State with { Disabled = Disabled };

    /// <summary>Gets a value indicating whether the reading direction is right to left.</summary>
    public bool Rtl { get; private set; }

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

        // A focused input that unmounts takes the field's focus with it (no blur reaches it).
        fieldScope?.SetFocused(this, false, Disabled);
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
        subscription.Select(static state => state.SubmitRevision);
        store.SetInputInsidePopup(insidePopup);
        twin = new ComboboxTwin(services.JSRuntime);
        twin.InputChanged += change => _ = Observe(services.InvokeAsync(() => Store.HandleInputChangeAsync(change.Text, change.Typed, change.Composing, change.Event)));
        twin.ClearRequested += request => _ = Observe(services.InvokeAsync(() => Store.ClearAsync(EventReason.EscapeKey, request.Event)));
        twin.RemoveRequested += request => _ = Observe(services.InvokeAsync(() => Store.RemoveLastAsync(request.Index, request.Event)));
        twin.SelectionEventReported += snapshot => Store.SetSelectionEvent(snapshot);
        twin.Decider = Decide;
        twin.KeyHandled += snapshot => _ = Observe(services.InvokeAsync(() => RunHandledKeyAsync(snapshot)));

        // ADR 04 Amendments 2026-11-03 (6): a committed text reaches the module's writer without a render of the input.
        store.InputValueCommitted += OnInputValueCommitted;
        twin.EnterCloseRequested += snapshot => _ = Observe(services.InvokeAsync(() => Store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.None, snapshot))));
        twin.DismissPressed += snapshot => _ = Observe(services.InvokeAsync(
            () => Store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.ClosePress, snapshot, new EventTrigger(Store.State.ElementIds.StartDismiss, null)))));
    }

    /// <summary>Applies the part's parameters.</summary>
    /// <param name="disabled">The part's <c>Disabled</c>.</param>
    /// <param name="generatedId">The part's generated id, used inside the popup.</param>
    /// <param name="consumerAttributes">The consumer's attributes, for its <c>id</c> and the declined handlers.</param>
    /// <param name="direction">The cascaded direction provider, or <see langword="null"/>.</param>
    /// <param name="render">The part's <c>Render</c> fragment, whose root element is the control, or <see langword="null"/>.</param>
    public void Apply(bool disabled, string? generatedId, IReadOnlyDictionary<string, object>? consumerAttributes, DirectionProviderContext? direction, RenderFragment? render)
    {
        disabledParameter = disabled;
        nonInputControl = render is not null && ComboboxParts.SubstituteRoot(render).TagName is { } tag && !string.Equals(tag, "input", StringComparison.OrdinalIgnoreCase);
        Rtl = ComboboxParts.IsRtl(direction);
        consumerId = ComboboxParts.ConsumerId(consumerAttributes);
        this.generatedId = generatedId;
        consumerKeyDown = default;
        if (consumerAttributes is not null)
        {
            foreach (KeyValuePair<string, object> pair in consumerAttributes)
            {
                AttributeValue value;
                if (string.Equals(pair.Key, KeyDownEvent, StringComparison.OrdinalIgnoreCase) && MergeProps.IsHandler(pair.Key, value = AttributeValue.FromObject(pair.Value)))
                {
                    consumerKeyDown = value;
                }
            }
        }

        reference.SetConsumerAttributes(consumerKeyDown.IsHandler ? keyDownWrapper : null);
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

        // AriaCombobox.tsx:1347-1376: an input-like control (an <input>, and any control on the server and before its element
        // mounts) takes the four fixed attributes and the combobox ARIA; another control (a <textarea>) takes the combobox ARIA
        // only while expanded, so a closed one keeps its native semantics. In both layouts (review B7, settled by the
        // orchestrator 2026-11-10).
        bool inputLike = !nonInputControl || !elementMounted;
        bool expanded = state.Expanded;
        if (inputLike)
        {
            attributes.Set("autocomplete", "off");
            attributes.Set("spellcheck", "false");
            attributes.Set("autocorrect", "off");
            attributes.Set("autocapitalize", "none");
        }

        if (inputLike || expanded)
        {
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

    /// <summary>Sets the field's validation attributes over the merged ones, unless the input sits in the popup.</summary>
    /// <param name="attributes">The merged attributes.</param>
    public void FinishAttributes(AttributeList attributes)
    {
        if (!hasPositionerParent)
        {
            ComboboxParts.AddValidationAttributes(attributes, fieldScope, Disabled);
        }
    }

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

    /// <summary>Records the input's focus for the field, and an inline list's highlight.</summary>
    /// <param name="focused">Whether the input received or lost the focus.</param>
    /// <returns>A task that completes when the focus change was handled.</returns>
    public async Task OnFocusChangeAsync(bool focused)
    {
        await Store.OnInlineInputFocusAsync(focused);
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
        if (element is not null && !elementMounted)
        {
            elementMounted = true;
            if (nonInputControl)
            {
                services.RequestRender();
            }
        }

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
            SubmitRevision = state.SubmitRevision == 0 ? null : state.SubmitRevision,
            SubmitFormElement = state.SubmitRevision != 0 && root.HiddenInput is not null ? 0 : null,
            DecidesKeyDown = consumerKeyDown.IsHandler,
            FocusOnAttach = sync.Handle is null && root.TakeInputFocus(),
        };
        // The hidden input travels with the update that moves the submit revision (AriaCombobox.tsx:945-948).
        TwinReferences? references = null;
        if (options.SubmitFormElement is not null && root.HiddenInput is { } hidden)
        {
            references = new TwinReferences();
            references.Add(hidden);
        }

        await sync.SyncAsync(twin, target, options, references);
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
            // AriaCombobox.tsx:1452-1474: the input holds listNavigation.reference in both layouts (review B7).
            reference.SetList(root.ListNavigation, CompositeListRole.Reference);
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
        if (string.Equals(point, ComboboxTwin.NavigatePoint, StringComparison.Ordinal))
        {
            return DecideKeyDown(snapshot);
        }

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

    // ADR 04 Amendments 2026-11-10 (design point 6, chosen by the user): the consumer's onkeydown runs once per keydown for
    // both the `combobox` handle's `navigate` and the `composite` reference handle's, whichever asks first, and both read the
    // verdict; a PreventPartHandler() stops both, as Base UI's preventBaseUIHandler() skips the input's merged handlers.
    // Blazor's later dispatch of the key skips the handler (the one-shot marker of ButtonBehavior's model).
    private DecisionPolicy DecideKeyDown(EventSnapshot snapshot)
    {
        if (!consumerKeyDown.IsHandler)
        {
            return DecisionPolicy.Run;
        }

        KeyEvent key = KeyEvent.From(snapshot);
        if (decidedKey == key)
        {
            return decidedVetoed ? DecisionPolicy.Stop : DecisionPolicy.Run;
        }

        KeyboardEventArgs args = snapshot.ToKeyboardEventArgs();
        RunConsumerKeyDown(args);
        decidedKey = key;
        decidedVetoed = args.PartHandlerPrevented();
        dispatchedKey = DispatchedKey.From(args);
        return decidedVetoed ? DecisionPolicy.Stop : DecisionPolicy.Run;
    }

    // The `composite` reference handle's `navigate` chain: the shared verdict; when it lets the key run, the reference calls
    // the wrapper next, which must not run the handler again.
    private DecisionPolicy DecideReferenceKey(EventSnapshot snapshot)
    {
        DecisionPolicy verdict = DecideKeyDown(snapshot);
        wrapperSkipsNext = verdict == DecisionPolicy.Run && consumerKeyDown.IsHandler;
        return verdict;
    }

    // The consumer's onkeydown as the reference handle holds it: inside its `navigate` (already run), from Blazor's dispatch
    // of a decided key (skipped once), and otherwise run, as on Server.
    private Task RunConsumerKeyDownAsync(KeyboardEventArgs args)
    {
        if (wrapperSkipsNext)
        {
            wrapperSkipsNext = false;
            return Task.CompletedTask;
        }

        DispatchedKey? marker = dispatchedKey;
        dispatchedKey = null;
        if (marker is { } decided && decided == DispatchedKey.From(args))
        {
            return Task.CompletedTask;
        }

        return consumerKeyDown.InvokeAsync(args);
    }

    // On Server a key the module stopped never reaches Blazor's dispatcher: the module reports it, and the handler runs a round
    // trip late, once per event.
    private Task RunHandledKeyAsync(EventSnapshot snapshot)
    {
        KeyEvent key = KeyEvent.From(snapshot);
        if (!consumerKeyDown.IsHandler || decidedKey == key)
        {
            return Task.CompletedTask;
        }

        decidedKey = key;
        decidedVetoed = false;
        return consumerKeyDown.InvokeAsync(snapshot.ToKeyboardEventArgs());
    }

    private void RunConsumerKeyDown(KeyboardEventArgs args)
    {
        try
        {
            Task task = consumerKeyDown.InvokeAsync(args);
            if (!task.IsCompleted)
            {
                _ = Observe(task);
            }
            else if (task.IsFaulted)
            {
                _ = services.DispatchException(task.Exception!.GetBaseException());
            }
        }
        catch (Exception exception)
        {
            _ = services.DispatchException(exception);
        }
    }

    private void OnInputValueCommitted()
    {
        if (!disposed && sync.Handle is not null)
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

    // One keydown as the module reports it: its time stamp tells two presses of a key apart.
    private readonly record struct KeyEvent(string Type, string? Key, string? Code, bool AltKey, bool CtrlKey, bool MetaKey, bool ShiftKey, double TimeStamp)
    {
        public static KeyEvent From(EventSnapshot snapshot) =>
            new(snapshot.Type, snapshot.Key, snapshot.Code, snapshot.AltKey, snapshot.CtrlKey, snapshot.MetaKey, snapshot.ShiftKey, snapshot.TimeStamp);
    }

    // A decided keydown as Blazor dispatches it afterwards, which carries no time stamp.
    private readonly record struct DispatchedKey(string Key, string Code, bool AltKey, bool CtrlKey, bool MetaKey, bool ShiftKey)
    {
        public static DispatchedKey From(KeyboardEventArgs e) => new(e.Key, e.Code, e.AltKey, e.CtrlKey, e.MetaKey, e.ShiftKey);
    }
}

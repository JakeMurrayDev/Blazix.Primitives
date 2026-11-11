using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives;

// combobox/trigger/ComboboxTrigger.tsx (CMB §3.9, §5.3): a native button by default (ButtonBehavior), tabindex 0 inside /
// -1 outside, role="combobox" with the input inside, aria-haspopup dialog/listbox, aria-expanded. Its handles:
// - `interactions` trigger role, Click { Event = "mousedown" } (CMB-PTR-02), the consumer's onmousedown declined
//   (InteractionsTriggerBehavior's veto; ADR 01's "C#-origin consumer vetoes");
// - `composite` list handle: Reference with the input inside, else Trigger; the closed typeahead (CMB-KEY-12) waits for the
//   composite grant;
// - `combobox` `trigger`: the press's focus move and the one-shot mouseup (`cancel-open`, CMB-PTR-03).
// The id is the root's with the input inside (CMB-API-45); outside, the consumer's, else a generated one, which Base UI does
// not render: the interactions and composite handles register the trigger by its id.
/// <summary>The behavior of a combobox-family trigger: its button, its press, its list handle and its ARIA.</summary>
internal sealed class ComboboxTriggerBehavior : IAsyncDisposable
{
    private static readonly InteractionsClickOptions s_click = new() { Event = "mousedown" };

    private readonly ComboboxPartServices services;

    private readonly ButtonBehavior button;

    private readonly InteractionsTriggerBehavior interactions;

    // CMB-KEY-12 (ComboboxTrigger.tsx:107-122; option (a), settled by the orchestrator 2026-11-11): the list's typeahead only.
    private readonly CompositeRootBehavior typeahead;

    private ComboboxStore? store;

    private FieldRootContext? fieldScope;

    private ComboboxTwin? twin;

    private readonly ComboboxHandleSync<ComboboxTwin, ComboboxOptions> sync = new();

    private string? id;

    private string? labelableId;

    private bool disabledParameter;

    private string? consumerId;

    private string? generatedId;

    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="ComboboxTriggerBehavior"/> class.</summary>
    /// <param name="services">The part's services.</param>
    /// <param name="developmentLog">The button mechanism's development log.</param>
    /// <param name="onKeyDown">The part's own <c>onkeydown</c> handler, which calls <see cref="HandleKeyDownAsync"/>.</param>
    /// <param name="onKeyUp">The part's own <c>onkeyup</c> handler, which calls <see cref="HandleKeyUpAsync"/>.</param>
    /// <param name="onClick">The part's own <c>onclick</c> handler, which calls <see cref="HandleClickAsync"/>.</param>
    /// <param name="onMouseDown">The part's own <c>onmousedown</c> handler, which calls <see cref="HandleMouseDownAsync"/>.</param>
    public ComboboxTriggerBehavior(ComboboxPartServices services, ButtonDevelopmentLog developmentLog, EventCallback onKeyDown, EventCallback onKeyUp, EventCallback onClick, EventCallback onMouseDown)
    {
        this.services = services;
        button = new ButtonBehavior(services.JSRuntime, services.Logger, developmentLog, services.Ids, services.RequestRender, services.DispatchException, onKeyDown, onKeyUp);
        interactions = new InteractionsTriggerBehavior(services.JSRuntime, RouteAsync, services.DispatchException, onClick, onMouseDown);
        typeahead = new CompositeRootBehavior(services.JSRuntime, services.Ids, services.RequestRender, services.DispatchException, default) { TypeaheadOnly = true };
    }

    /// <summary>Gets the root's store.</summary>
    public ComboboxStore Store => store ?? throw new InvalidOperationException($"Blazix: `{services.Family}Trigger` is used before its first render.");

    /// <summary>Gets the field's state the trigger projects.</summary>
    public FieldStateSnapshot Field => (fieldScope?.State ?? FieldStateSnapshot.Default) with { Disabled = Disabled };

    /// <summary>Gets a value indicating whether the trigger is disabled, by the part, the root or the field.</summary>
    public bool Disabled => disabledParameter || Store.State.Disabled || (fieldScope?.Disabled ?? false);

    /// <summary>Gets the id the trigger renders.</summary>
    public string? Id => id;

    /// <summary>Gets a task that completes once the trigger's handles have attached.</summary>
    public Task TwinAttached => Task.WhenAll(button.TwinAttached, interactions.TwinAttached);

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
            store.SetElementIds(ids => ids.Trigger == own ? ids with { Trigger = null, TriggerLabelable = null } : ids);
        }

        // A focused trigger that unmounts takes the field's focus with it (no blur reaches it).
        fieldScope?.SetFocused(this, false, Disabled);
        if (store is not null)
        {
            store.ClosedFromInsidePopup -= OnClosedFromInsidePopup;
        }

        fieldScope?.RemoveListener(services.RequestRender);
        button.Dispose();
        typeahead.Dispose();
        Task twinDisposal = twin?.DisposeAsync().AsTask() ?? Task.CompletedTask;
        await Task.WhenAll(interactions.DisposeAsync().AsTask(), twinDisposal);
    }

    /// <summary>Reads the root and the field and subscribes the part.</summary>
    /// <param name="cascaded">The cascaded store, or <see langword="null"/> outside a root.</param>
    /// <param name="field">The cascaded field, or <see langword="null"/>.</param>
    /// <param name="subscribe">The part's subscription factory over the store.</param>
    public void Subscribe(ComboboxStore? cascaded, FieldRootContext? field, Func<ComboboxStore, SelectorSubscription<ComboboxStoreState>> subscribe)
    {
        store = ComboboxParts.RequireRoot(cascaded, services.Family);
        fieldScope = field;
        fieldScope?.AddListener(services.RequestRender);
        store.ClosedFromInsidePopup += OnClosedFromInsidePopup;
        SelectorSubscription<ComboboxStoreState> subscription = subscribe(store);
        subscription.Select(ComboboxStoreState.Open);
        subscription.Select(ComboboxStoreState.InsidePopup);
        subscription.Select(ComboboxStoreState.ListEmpty);
        subscription.Select(static state => state.Lifecycle.Mounted ? state.PopupSide : null);
        subscription.Select(static state => state.Disabled);
        subscription.Select(static state => state.ReadOnly);
        subscription.Select(static state => state.Required);
        subscription.Select(static state => state.Id);
        subscription.Select(static state => state.HasSelectedValue);
        subscription.Select(static state => state.ElementIds.Input);
        subscription.Select(static state => state.ElementIds.List);
        subscription.Select(static state => state.ElementIds.Popup);
        subscription.Select(static state => state.ElementIds.Label);
        typeahead.SetList(store.ListNavigation, CompositeListRole.Trigger);
        typeahead.TypeaheadSource = () => Disabled ? null : Store.TriggerTypeahead;
        twin = new ComboboxTwin(services.JSRuntime);
        twin.Blurred += snapshot => _ = Observe(services.InvokeAsync(() =>
        {
            OnFocusChange(false);
            return Task.CompletedTask;
        }));
        // A report sent before the disabled state reached the module (Server) opens nothing either (final review F3).
        twin.OpenRequested += snapshot => _ = Observe(services.InvokeAsync(() => Disabled ? Task.CompletedTask : Store.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.ListNavigation, snapshot))));
        twin.OpenCanceled += snapshot => _ = Observe(services.InvokeAsync(() => Store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.CancelOpen, snapshot))));
    }

    /// <summary>Applies the part's parameters.</summary>
    /// <param name="nativeButton">The part's <c>NativeButton</c>.</param>
    /// <param name="disabled">The part's <c>Disabled</c>.</param>
    /// <param name="consumerId">The consumer's id, or <see langword="null"/>.</param>
    /// <param name="generatedId">The part's generated id, used outside the popup when the consumer gave none.</param>
    /// <param name="consumerAttributes">The consumer's attributes.</param>
    public void Apply(bool nativeButton, bool disabled, string? consumerId, string? generatedId, IReadOnlyDictionary<string, object>? consumerAttributes)
    {
        disabledParameter = disabled;
        this.consumerId = consumerId;
        this.generatedId = generatedId;
        button.Native = nativeButton;
        button.FocusableWhenDisabled = null;
        button.Disabled = Disabled;
        button.SetConsumerAttributes(consumerAttributes);
        interactions.SetConsumerAttributes(consumerAttributes);
        interactions.SessionToken = Store.SessionToken;
        Resolve();
    }

    /// <summary>Adds the trigger's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    public void AddPartAttributes(AttributeList attributes)
    {
        // The layout can change with no parameter set (an outside input registering after the trigger rendered).
        Resolve();
        ComboboxStoreState state = Store.State;
        bool insidePopup = state.InputInsidePopup;
        bool open = state.Lifecycle.EffectiveOpen;
        button.Disabled = Disabled;
        button.AddAttributes(attributes, id, composite: false);
        interactions.AddAttributes(attributes);
        attributes.Set("id", id);

        // CMB-DOM-05, ARIA-07 (ComboboxTrigger.tsx:150-166).
        attributes.Set("tabindex", insidePopup ? "0" : "-1");
        if (insidePopup)
        {
            attributes.Set("role", "combobox");
        }

        attributes.Set("aria-expanded", open ? "true" : "false");
        attributes.Set("aria-haspopup", insidePopup ? "dialog" : "listbox");
        string? controls = !open ? null : insidePopup ? state.ElementIds.Popup ?? state.Id + "-popup" : state.ElementIds.List;
        if (controls is not null)
        {
            attributes.Set("aria-controls", controls);
        }

        if (insidePopup && state.Required)
        {
            attributes.Set("aria-required", "true");
        }

        if (insidePopup && state.ReadOnly)
        {
            attributes.Set("aria-readonly", "true");
        }

        // CMB-ARIA-16 (resolveAriaLabelledBy): the field's label, else the combobox's.
        if ((fieldScope?.Labelable.Label.Id ?? state.ElementIds.Label) is { } labelledBy)
        {
            attributes.Set("aria-labelledby", labelledBy);
        }
    }

    /// <summary>Determines whether the consumer's handler for an event renders on the element.</summary>
    /// <param name="eventName">The event's attribute name.</param>
    /// <returns><see langword="false"/> for a handler the part declines and runs itself.</returns>
    public bool ForwardsConsumerHandler(string eventName) =>
        button.ForwardsConsumerHandler(eventName) && interactions.ForwardsConsumerHandler(eventName);

    /// <summary>Sets the field's validation attributes over the merged ones.</summary>
    /// <param name="attributes">The merged attributes.</param>
    public void FinishAttributes(AttributeList attributes) => ComboboxParts.AddValidationAttributes(attributes, fieldScope, Disabled);

    /// <summary>Runs the declined consumer <c>onclick</c>.</summary>
    /// <param name="args">The click.</param>
    /// <returns>A task that completes when the handler has run.</returns>
    public Task HandleClickAsync(MouseEventArgs args) => interactions.HandleClickAsync(args);

    /// <summary>Forces the list to mount, then runs the declined consumer <c>onmousedown</c> unless the press already ran it.</summary>
    /// <param name="args">The mousedown.</param>
    /// <returns>A task that completes when the handler has run.</returns>
    public Task HandleMouseDownAsync(MouseEventArgs args)
    {
        // CMB-STA-32 (ComboboxTrigger.tsx:182-188): a press mounts the items so the selected one can be highlighted.
        if (!Disabled)
        {
            if (!Store.State.InputInsidePopup && id is not null)
            {
                Store.SetDomReference(id);
            }

            Store.ForceMount();
        }

        return interactions.HandleMouseDownAsync(args);
    }

    /// <summary>Runs the declined consumer <c>onkeydown</c> unless a decision already ran it.</summary>
    /// <param name="args">The keydown.</param>
    /// <returns>A task that completes when the handler has run.</returns>
    public Task HandleKeyDownAsync(KeyboardEventArgs args) => button.HandleKeyDownAsync(args);

    /// <summary>Runs the declined consumer <c>onkeyup</c>.</summary>
    /// <param name="args">The keyup.</param>
    /// <returns>A task that completes when the handler has run.</returns>
    public Task HandleKeyUpAsync(KeyboardEventArgs args) => button.HandleKeyUpAsync(args);

    /// <summary>Records the trigger's focus for the field and forces the list to mount.</summary>
    /// <param name="focused">Whether the trigger received or lost the focus.</param>
    public void OnFocusChange(bool focused)
    {
        // ComboboxTrigger.tsx:171-185: a focus mounts the items for typeahead; a blur into the popup is not a blur.
        if (focused && !Disabled)
        {
            Store.ForceMount();
        }

        if (fieldScope is null)
        {
            return;
        }

        // ComboboxTrigger.tsx:186-201: touched, and a commit in onBlur mode, through the root's field behaviour.
        if (!focused)
        {
            Store.ControlBlurred?.Invoke();
        }

        fieldScope.SetFocused(this, focused, Disabled);
    }

    /// <summary>Attaches the handles and sends their changed options.</summary>
    /// <param name="element">The trigger's element.</param>
    /// <returns>A task that completes when the handles have their options.</returns>
    public async Task AfterRenderAsync(ElementReference? element)
    {
        if (disposed)
        {
            return;
        }

        button.Disabled = Disabled;
        interactions.SessionToken = Store.SessionToken;
        Task pressed = interactions.AfterRenderAsync(true, element, id);
        Task twinSync = SyncTwinAsync(element);
        Task typeaheadSync = typeahead.AfterRenderAsync(true, element, id);
        await button.AfterRenderAsync(true, element, id, composite: false);
        await Task.WhenAll(pressed, twinSync, typeaheadSync);
    }

    // CMB-API-45 (ComboboxTrigger.tsx:89-90, derived at every render): the root's id with the input inside the popup, else
    // the consumer's or a generated one. The trigger joins no list (ComboboxTrigger.tsx:150-166): the input is the list's
    // reference in both layouts (review B7, settled by the orchestrator 2026-11-10).
    private void Resolve()
    {
        ComboboxStore root = Store;
        bool insidePopup = root.State.InputInsidePopup;
        string? next = consumerId ?? (insidePopup ? root.State.Id : generatedId);

        // ComboboxLabel.tsx:37 focuses the trigger's DOM id, which Base UI renders only inside or when the consumer gave one.
        string? labelable = insidePopup ? next : consumerId;
        if (!string.Equals(next, id, StringComparison.Ordinal) || !string.Equals(labelable, labelableId, StringComparison.Ordinal))
        {
            string? previous = id;
            id = next;
            labelableId = labelable;
            root.SetElementIds(ids => ids.Trigger is null || ids.Trigger == previous ? ids with { Trigger = next, TriggerLabelable = labelable } : ids);
        }


        InteractionsClickOptions? click = Disabled ? null : s_click;
        if (!string.Equals(interactions.Options.TriggerId, id, StringComparison.Ordinal) || !ReferenceEquals(interactions.Options.Click, click))
        {
            interactions.Options = new InteractionsTriggerOptions { TriggerId = id, Click = click };
        }
    }

    private async Task SyncTwinAsync(ElementReference? element)
    {
        if (twin is null || element is not { } target)
        {
            return;
        }

        ComboboxStoreState state = Store.State;
        ComboboxOptions options = new()
        {
            Part = ComboboxTwin.TriggerPart,
            InsidePopup = state.InputInsidePopup,
            Disabled = Disabled,
            Open = state.Lifecycle.EffectiveOpen,
            InputId = state.ElementIds.Input,
            ListId = state.ElementIds.List,
            PopupId = state.ElementIds.Popup,
        };
        await sync.SyncAsync(twin, target, options);
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

    // AriaCombobox.tsx:838-850: the trigger keeps the field's focus while the focus moves into the popup; the popup's close by
    // focus-out or an outside press ends it.
    private void OnClosedFromInsidePopup() => fieldScope?.SetFocused(this, false, Disabled);
}

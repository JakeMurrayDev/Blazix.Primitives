using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// SEL-STA-15 (select/trigger/SelectTrigger.tsx:218-226, 236-257); design point 4 (a), pending the user: the value is in
// the state, not projected (SEL-DOM-08). PopupSide is C#'s render of the positioning report (SEL-STA-28, never `none`).
/// <summary>Represents the state of a select trigger, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <typeparam name="TValue">The type of the select's values.</typeparam>
/// <param name="Disabled">A value indicating whether the trigger is disabled, its own flag, the root's or the field's; projected as <c>data-disabled</c>.</param>
/// <param name="Touched">A value indicating whether the field has been touched; projected as <c>data-touched</c>.</param>
/// <param name="Dirty">A value indicating whether the value differs from its initial value; projected as <c>data-dirty</c>.</param>
/// <param name="Valid">The field's verdict: projected as <c>data-valid</c> for <see langword="true"/>, <c>data-invalid</c> for <see langword="false"/>, and neither for <see langword="null"/>.</param>
/// <param name="Filled">A value indicating whether a value is selected, inside a field; projected as <c>data-filled</c>.</param>
/// <param name="Focused">A value indicating whether the trigger has focus, inside a field; projected as <c>data-focused</c>.</param>
/// <param name="Open">A value indicating whether the popup is open; projected as <c>data-popup-open</c> and <c>data-pressed</c>.</param>
/// <param name="ReadOnly">A value indicating whether the value is locked; projected as <c>data-readonly</c>.</param>
/// <param name="Value">The selected value of a single select; not projected.</param>
/// <param name="Values">The selected values of a multiple select, empty otherwise; not projected.</param>
/// <param name="PopupSide">The side the mounted popup is placed on, or <see langword="null"/>; projected as <c>data-popup-side</c>.</param>
/// <param name="Placeholder">A value indicating whether no value is selected; projected as <c>data-placeholder</c>.</param>
[PartState]
public sealed partial record SelectTriggerState<TValue>(
    bool Disabled,
    bool Touched,
    bool Dirty,
    bool? Valid,
    bool Filled,
    bool Focused,
    bool Open,
    bool ReadOnly,
    [property: NotProjected] TValue Value,
    [property: NotProjected] IReadOnlyList<TValue> Values,
    Side? PopupSide,
    bool Placeholder);

// select/trigger/SelectTriggerDataAttributes.ts (SEL-DOM-02..06); `data-required` is declared by Base UI and never
// rendered (SEL-DOM-03, §16 Q6), so the port declares it too and renders it never.
/// <summary>Provides the names of the data attributes a select trigger renders.</summary>
public static class SelectTriggerDataAttributes
{
    /// <summary>Represents the attribute present while the popup is open: <c>data-popup-open</c>.</summary>
    public const string PopupOpen = PopupTriggerDataAttributes.PopupOpen;

    /// <summary>Represents the attribute present while the popup is open: <c>data-pressed</c>.</summary>
    public const string Pressed = PopupTriggerDataAttributes.Pressed;

    /// <summary>Represents the attribute present while the trigger is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute present while the value is locked: <c>data-readonly</c>.</summary>
    public const string ReadOnly = "data-readonly";

    /// <summary>Represents an attribute Base UI declares for a required select and never renders: <c>data-required</c>.</summary>
    public const string Required = "data-required";

    /// <summary>Represents the attribute present while no value is selected: <c>data-placeholder</c>.</summary>
    public const string Placeholder = "data-placeholder";

    /// <summary>Represents the attribute whose value is the side the popup is placed on: <c>data-popup-side</c>.</summary>
    public const string PopupSide = "data-popup-side";

    /// <summary>Represents the attribute present while the field is valid: <c>data-valid</c>.</summary>
    public const string Valid = "data-valid";

    /// <summary>Represents the attribute present while the field is invalid: <c>data-invalid</c>.</summary>
    public const string Invalid = "data-invalid";

    /// <summary>Represents the attribute present once the field has been touched: <c>data-touched</c>.</summary>
    public const string Touched = "data-touched";

    /// <summary>Represents the attribute present while the value differs from its initial value: <c>data-dirty</c>.</summary>
    public const string Dirty = "data-dirty";

    /// <summary>Represents the attribute present while a value is selected: <c>data-filled</c>.</summary>
    public const string Filled = "data-filled";

    /// <summary>Represents the attribute present while the trigger has focus: <c>data-focused</c>.</summary>
    public const string Focused = "data-focused";
}

// Base UI's Select.Trigger (select/trigger/SelectTrigger.tsx); the contract is docs/components/select.md §3.3, §5.1, §6.
// Four handles on one element, in one render's attach (their order pinned by L4 stall titles):
// - `button` (ButtonBehavior { Native = NativeButton, Disabled }; SEL-API-24/25, SEL-DOM-01/07): `type`, `disabled` or
//   `aria-disabled`, Enter and Space as a click; tabindex 0/-1 rendered by the part (SelectTrigger.tsx:144).
// - `interactions` (the trigger role, Click { Event = mousedown, Toggle }, none while disabled; SEL-PTR-01, FLT-PTR-02):
//   the press opens on the next frame (at once for a virtual press), the consumer's onmousedown and onclick run inside
//   the press on WebAssembly (ADR 01 "C#-origin consumer vetoes": PTR-01, DIS-04).
// - `composite` in the list's Reference role (SEL-KEY-01..04, 17): the arrow-key open, the closed trigger's typeahead.
// - `select`'s trigger role (SEL-PTR-04, the release listener; added with the module).
// The part's own onfocus and onblur (C#: their results are state): the 0 ms force-mount timer (SEL-STA-09, FOC-11; a timer
// whose result is state, ADR 04 §1), the aligned focus close (SEL-FOC-07, reason `none`), the field's focused and touched
// flags (SEL-FOC-10, SEL-FRM-03/07).
/// <summary>
/// Represents the button that opens a select's popup and shows its value, Base UI's <c>Select.Trigger</c>, rendered as a
/// <c>button</c> with <c>role="combobox"</c>.
/// </summary>
/// <typeparam name="TValue">The type of the select's values.</typeparam>
/// <remarks>
/// <typeparamref name="TValue"/> is inferred from the enclosing <see cref="SelectRoot{TValue}"/> when the part is written
/// inside the root's markup. A part written elsewhere, such as in a <see cref="Microsoft.AspNetCore.Components.RenderFragment"/>
/// field or in another component, names it: <c>TValue="string"</c>.
/// </remarks>
public sealed class SelectTrigger<TValue> : PartBase<SelectTriggerState<TValue>>
{
    private static readonly IReadOnlyList<TValue> s_empty = [];
    private static readonly InteractionsClickOptions s_click = new() { Event = "mousedown" };
    private static readonly StateAttributesMapping<SelectTriggerState<TValue>> s_mapping = FieldValidityMapping.Create<SelectTriggerState<TValue>>()
        .Map("Open", PopupStateMapping.PressableTriggerOpen)
        .Map("PopupSide", SelectSideAttributes.PopupSide);

    private readonly EventCallback onClick;
    private readonly EventCallback onMouseDown;
    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private readonly EventCallback onFocus;
    private readonly Action fieldChanged;
    private SelectStore<TValue>? store;
    private ButtonBehavior? button;
    private InteractionsTriggerBehavior? interactions;
    private CompositeRootBehavior? reference;
    private SelectModuleAttachment? module;
    private StoreSelection<SelectStoreState, bool>? open;
    private StoreSelection<SelectStoreState, bool>? disabledRoot;
    private StoreSelection<SelectStoreState, bool>? readOnly;
    private StoreSelection<SelectStoreState, bool>? required;
    private StoreSelection<SelectStoreState, int>? valueVersion;
    private StoreSelection<SelectStoreState, bool>? hasSelectedValue;
    private StoreSelection<SelectStoreState, Side?>? popupSide;
    private StoreSelection<SelectStoreState, string?>? rootId;
    private StoreSelection<SelectStoreState, string?>? listboxId;
    private StoreSelection<SelectStoreState, string?>? labelId;
    private StoreSelection<SelectStoreState, bool>? hasList;
    private StoreSelection<SelectStoreState, bool>? multiple;
    private SelectTriggerState<TValue>? state;
    private int stateVersion = -1;
    private FieldStateSnapshot renderedSnapshot;
    private ITimer? forceMountTimer;

    /// <summary>Initializes a new instance of the <see cref="SelectTrigger{TValue}"/> class.</summary>
    public SelectTrigger()
    {
        onClick = CreateHandler<MouseEventArgs>("onclick", e => interactions!.HandleClickAsync(e));
        onMouseDown = CreateHandler<MouseEventArgs>("onmousedown", e => interactions!.HandleMouseDownAsync(e));
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", OnKeyDownAsync);
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => button!.HandleKeyUpAsync(e));
        onFocus = CreateHandler<FocusEventArgs>("onfocus", OnFocusAsync);
        fieldChanged = () => _ = InvokeAsync(OnFieldChanged);
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<SelectTrigger<TValue>> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    [Inject]
    private IServiceProvider Services { get; set; } = null!;

    private bool EffectiveDisabled => Disabled || disabledRoot!.Value;

    private string? TriggerId => Id ?? rootId!.Value;

    [CascadingParameter]
    private SelectStore<TValue>? RootStore { get; set; }

    [CascadingParameter]
    private ListNavigationContext? ListContext { get; set; }

    [CascadingParameter]
    private CompositeRootContext? CompositeContext { get; set; }

    // SEL-API-24
    /// <summary>Gets or sets a value indicating whether the trigger ignores user interaction.</summary>
    /// <value><see langword="false"/> by default; a disabled root or field also disables it.</value>
    [Parameter]
    public bool Disabled { get; set; }

    // SEL-API-25
    /// <summary>Gets or sets a value indicating whether the rendered element is a native <c>button</c>.</summary>
    /// <value><see langword="true"/> by default; set it to <see langword="false"/> when <see cref="PartBase{TState}.Render"/> supplies another element.</value>
    [Parameter]
    public bool NativeButton { get; set; } = true;

    /// <inheritdoc />
    protected override string DefaultTagName => "button";

    /// <inheritdoc />
    protected override SelectTriggerState<TValue> State
    {
        get
        {
            SelectStore<TValue> current = store!;
            FieldStateSnapshot snapshot = current.Field?.Snapshot ?? FieldStateSnapshot.Default;
            bool disabled = EffectiveDisabled;
            int version = valueVersion!.Value;
            if (state is null
                || stateVersion != version
                || snapshot != renderedSnapshot
                || state.Disabled != disabled
                || state.Open != open!.Value
                || state.ReadOnly != readOnly!.Value
                || state.PopupSide != popupSide!.Value
                || state.Placeholder == hasSelectedValue!.Value)
            {
                stateVersion = version;
                renderedSnapshot = snapshot;
                state = new SelectTriggerState<TValue>(
                    disabled,
                    snapshot.Touched,
                    snapshot.Dirty,
                    snapshot.Valid,
                    snapshot.Filled,
                    snapshot.Focused,
                    open!.Value,
                    readOnly!.Value,
                    current.HasValue ? current.Value : default!,
                    multiple!.Value ? current.Values : s_empty,
                    popupSide!.Value,
                    !hasSelectedValue!.Value);
            }

            return state;
        }
    }

    /// <inheritdoc />
    protected override StateAttributesMapping<SelectTriggerState<TValue>>? StateAttributesMapping => s_mapping;

    // SEL-API-10/26, SEL-DOM-09: the trigger's own id, else the root's; on the trigger's own element (issue 115).
    /// <inheritdoc />
    protected override bool UsesGeneratedId => false;

    /// <summary>Gets a task that completes when the trigger's behaviors are attached.</summary>
    internal Task TwinsAttached => Task.WhenAll(button!.TwinAttached, interactions!.TwinAttached, reference!.TwinAttached);

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The trigger is not inside a <see cref="SelectRoot{TValue}"/> of the same value type.</exception>
    protected override void OnInitialized()
    {
        store = (SelectStore<TValue>)SelectStore.Require(RootStore);
        button = new ButtonBehavior(JSRuntime, Logger, DevelopmentLog, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown, onKeyUp);
        interactions = new InteractionsTriggerBehavior(JSRuntime, RouteAsync, DispatchExceptionAsync, onClick, onMouseDown);
        reference = new CompositeRootBehavior(JSRuntime, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown);
        module = new SelectModuleAttachment(JSRuntime);
        SelectStore<TValue> current = store;
        module.Twin.OpenCanceled += snapshot => _ = InvokeAsync(() => current.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.CancelOpen, snapshot)));
        module.Twin.TriggerBlurred += intoPopup => _ = InvokeAsync(() => current.Field?.HandleBlurAsync(intoPopup, current.BoxedValue?.Invoke()) ?? Task.CompletedTask);
        store.FieldStateChanged += fieldChanged;
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        bool disabled = EffectiveDisabled;
        button!.Native = NativeButton;
        button.Disabled = disabled;
        button.SetConsumerAttributes(AdditionalAttributes);
        interactions!.SetConsumerAttributes(AdditionalAttributes);
        reference!.SetList(ListContext, CompositeListRole.Reference);
        reference.SetConsumerAttributes(AdditionalAttributes);
        SyncInteractions(disabled);
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        SelectStore<TValue> current = store!;
        current.SetTriggerId(Enabled ? TriggerId : null);
        current.TriggerElement = Enabled ? Element : null;
        bool disabled = EffectiveDisabled;
        SyncInteractions(disabled);

        // The handles start attaching in the same render; their order is pinned by the stall titles.
        Task interactionsSync = interactions!.AfterRenderAsync(Enabled, Element, TriggerId);
        Task referenceSync = reference!.AfterRenderAsync(Enabled, Element, TriggerId);
        Task moduleSync = Element is { } element
            ? module!.SyncAsync(element, new SelectOptions { Part = SelectTwin.TriggerPart, Open = open!.Value }, positioner: current.PositionerElement)
            : Task.CompletedTask;
        await button!.AfterRenderAsync(Enabled, Element, TriggerId, composite: CompositeContext is not null);
        await interactionsSync;
        await referenceSync;
        await moduleSync;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            forceMountTimer?.Dispose();
            forceMountTimer = null;
            if (store is not null)
            {
                store.FieldStateChanged -= fieldChanged;
                if (string.Equals(store.State.TriggerId, TriggerId, StringComparison.Ordinal))
                {
                    store.SetTriggerId(null);
                }

                // SEL-FRM-20: the focused flag goes with the trigger.
                store.Field?.HandleBlurAsync(intoPopup: true, value: null);
            }

            button?.Dispose();
            reference?.Dispose();
            if (module is not null)
            {
                _ = module.DisposeAsync().AsTask();
            }

            if (interactions is not null)
            {
                _ = interactions.DisposeAsync().AsTask();
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void SubscribeSelectors()
    {
        SelectorSubscription<SelectStoreState> subscription = SubscribeTo<SelectStoreState>(store!);
        open = subscription.Select(SelectStoreState.Open);
        disabledRoot = subscription.Select(SelectStoreState.DisabledSelector);
        readOnly = subscription.Select(SelectStoreState.ReadOnlySelector);
        required = subscription.Select(SelectStoreState.RequiredSelector);
        valueVersion = subscription.Select(SelectStoreState.ValueVersionSelector);
        hasSelectedValue = subscription.Select(SelectStoreState.HasSelectedValueSelector);
        popupSide = subscription.Select(SelectStoreState.TriggerPopupSide);
        rootId = subscription.Select(SelectStoreState.RootIdSelector);
        listboxId = subscription.Select(SelectStoreState.ListboxConsumerIdSelector);
        labelId = subscription.Select(SelectStoreState.LabelIdSelector);
        hasList = subscription.Select(SelectStoreState.HasListSelector);
        multiple = subscription.Select(SelectStoreState.MultipleSelector);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        SelectStore<TValue> current = store!;
        SelectField? field = current.Field;
        bool disabled = EffectiveDisabled;
        bool isOpen = open!.Value;
        button!.Disabled = disabled;
        attributes.Set("id", TriggerId);
        attributes.Set(PopupMarkerAttributes.ClickTrigger, string.Empty);
        attributes.Set("aria-expanded", isOpen ? "true" : "false");
        attributes.Set("aria-haspopup", "listbox");

        // SEL-ARIA-04: the listbox's rendered id while open, the consumer's or the default.
        if (isOpen && (listboxId!.Value ?? (rootId!.Value is { } id ? id + "-list" : null)) is { } controls)
        {
            attributes.Set("aria-controls", controls);
        }

        // SEL-ARIA-05 (resolveAriaLabelledBy): the field's label, else the select label.
        if ((field?.FieldLabelId ?? labelId!.Value) is { } labelledBy)
        {
            attributes.Set("aria-labelledby", labelledBy);
        }

        if (readOnly!.Value)
        {
            attributes.Set("aria-readonly", "true");
        }

        if (required!.Value)
        {
            attributes.Set("aria-required", "true");
        }

        if (field?.AriaInvalid == true)
        {
            attributes.Set("aria-invalid", "true");
        }

        attributes.Set("tabindex", disabled ? "-1" : "0");
        attributes.Set("onclick", onClick);
        attributes.Set("onmousedown", onMouseDown);
        attributes.Set("onfocus", onFocus);
        button.AddAttributes(attributes, TriggerId, composite: CompositeContext is not null);
        interactions!.AddAttributes(attributes);
        reference!.AddAttributes(attributes, TriggerId);
    }

    // SEL-ARIA-01: `role="combobox"` after every other source, the consumer's and a wrapping part's included.
    // SEL-ARIA-07, SEL-FRM-16/17: the field's description and error ids after the consumer's own (the CheckboxRoot
    // precedent), which the merge would otherwise keep alone.
    /// <inheritdoc />
    protected override void FinishAttributes(AttributeList attributes)
    {
        attributes.Set("role", "combobox");
        string? consumer = attributes.TryGetValue("aria-describedby", out AttributeValue current) && current.TryGetValue(out string text) ? text : null;
        if (store!.Field?.DescribedBy(consumer) is { } describedBy)
        {
            attributes.Set("aria-describedby", describedBy);
        }
    }

    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName) =>
        button!.ForwardsConsumerHandler(eventName) && interactions!.ForwardsConsumerHandler(eventName) && reference!.ForwardsConsumerHandler(eventName);

    // SEL-PTR-01, SEL-PTR-21: the press toggles from the mousedown; no press while disabled.
    private void SyncInteractions(bool disabled)
    {
        interactions!.SessionToken = store!.SessionToken;
        InteractionsClickOptions? click = disabled ? null : s_click;
        InteractionsTriggerOptions current = interactions.Options;
        if (!string.Equals(current.TriggerId, TriggerId, StringComparison.Ordinal) || !ReferenceEquals(current.Click, click))
        {
            interactions.Options = new InteractionsTriggerOptions { TriggerId = TriggerId, Click = click };
        }
    }

    private Task OnKeyDownAsync(KeyboardEventArgs e) => reference!.HandleKeyDownAsync(e);

    // SEL-STA-09, SEL-FOC-11: force-mount 0 ms after the focus; SEL-FOC-07: an aligned open popup closes so it does not
    // cover the focused trigger; SEL-FOC-10: the field's focused flag.
    private async Task OnFocusAsync(FocusEventArgs e)
    {
        SelectStore<TValue> current = store!;
        current.Field?.HandleFocus();
        if (!current.State.ForceMount && forceMountTimer is null)
        {
            TimeProvider time = Services.GetService<TimeProvider>() ?? TimeProvider.System;
            forceMountTimer = time.CreateTimer(static state => ((SelectTrigger<TValue>)state!).ForceMountElapsed(), this, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }

        if (current.IsOpen && current.State.AlignedActive)
        {
            await current.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.None, FocusSnapshot("focus")));
        }
    }


    private void ForceMountElapsed() => _ = InvokeAsync(() =>
    {
        forceMountTimer?.Dispose();
        forceMountTimer = null;
        store?.ForceMount();
    });

    private Task RouteAsync(OpenChangeRequest request)
    {
        SelectStore<TValue> current = store!;
        if (request.InteractionType is { } type)
        {
            current.SetOpenInteractionType(type);
        }

        return current.RequestOpenAsync(request.Open, request.Details);
    }

    private void OnFieldChanged()
    {
        SelectTriggerState<TValue>? previous = state;
        if (previous is null || previous != State)
        {
            StateHasChanged();
        }
    }

    private static EventSnapshot FocusSnapshot(string type) => new() { Kind = EventKind.FocusEvent, Type = type };
}

using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Blazix.Primitives.Utilities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// The root half every combobox-family root composes (combobox/root/AriaCombobox.tsx, configured by ComboboxRoot.tsx and
// AutocompleteRoot.tsx; ADR 08 Amendments 2026-11-03 (3), the AlertDialog shape: ComboboxRoot<TValue> and track 11's
// AutocompleteRoot<TValue> are thin parts over it). It owns:
// - the lifecycle root (PopupLifecycleRootBehavior: the store at the first parameter set, the fixed cascade, the boundary);
// - the `interactions` session on the store's token: Dismiss with the outside-press events (mouse sloppy, touch
//   intentional), the exemption list (Trigger, Clear, Chips, InputGroup; CMB-DIS-01) and the inline bubbling, enabled iff
//   !Disabled && !Inline (AriaCombobox.tsx:1395-1411); its ParentToken from the family-agnostic cascade;
// - list navigation (ListNavigationBehavior: virtual focus), its handles attaching after the session (sessionReady);
// - the hidden input(s) after the children (CMB-DOM-18/19), the `form` role's committed value (C# → JS from update);
// - the commit path: committed changes reach the session outside a render (ADR 01 Amendments 2026-10-27).
/// <summary>The behavior of a combobox-family root: its store, session, list navigation and hidden inputs.</summary>
/// <typeparam name="TValue">The type of the items' values.</typeparam>
internal sealed class ComboboxRootBehavior<TValue> : IAsyncDisposable
{
    private const string Sloppy = "sloppy";
    private const string Intentional = "intentional";

    private static readonly InteractionsPressEvents s_outsidePressEvent = new(Sloppy, Intentional);

    private readonly IJSRuntime jsRuntime;
    private readonly Func<Exception, Task> dispatchException;

    private readonly Func<Func<Task>, Task> invokeAsync;
    private readonly PopupLifecycleRootBehavior<ComboboxStore<TValue>, ComboboxStoreState> root;
    private readonly RenderFragment content;
    private readonly Action onCommitted;
    private InteractionsSessionBehavior? session;
    private ListNavigationBehavior? list;
    private SelectorSubscription<ComboboxStoreState>? committed;

    private ComboboxFieldBehavior<TValue>? fieldBehavior;
    private ComboboxRootInputs<TValue> inputs = new();
    private IFloatingNodeContext? floatingParent;
    private PartIdGenerator? ids;
    private FormTwin? formTwin;
    private TwinHandle<FormOptions>? formHandle;
    private ElementReference hiddenInput;
    private FormOptions? formOptions;
    private string? attachedSerialized;
    private int formRevision;

    // Revisions sent only to ask for a fresh report of the hidden input's native state.
    private int formRefreshes;

    private int refreshRevision;

    private TaskCompletionSource? refreshSource;

    private (bool Required, bool Disabled, bool ReadOnly) sentConstraints;
    private InteractionsDismissOptions? dismiss;
    private DismissKey dismissKey;
    private bool rendered;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="ComboboxRootBehavior{TValue}"/> class.</summary>
    /// <param name="family">The family whose parts the diagnostics name: <c>Combobox</c> or <c>Autocomplete</c>.</param>
    /// <param name="jsRuntime">The runtime the twins import their modules through.</param>
    /// <param name="requestRender">Asks the root to render, its <c>StateHasChanged</c>.</param>
    /// <param name="invokeAsync">Runs a report's work on the root's dispatcher, its <c>InvokeAsync</c>.</param>
    /// <param name="dispatchException">Routes an exception of a request to the root.</param>
    public ComboboxRootBehavior(string family, IJSRuntime jsRuntime, Action requestRender, Func<Func<Task>, Task> invokeAsync, Func<Exception, Task> dispatchException)
    {
        this.jsRuntime = jsRuntime;
        this.invokeAsync = invokeAsync;
        this.dispatchException = dispatchException;
        root = new((popupId, token, logger) => new ComboboxStore<TValue>(ComboboxStoreState.Initial(popupId, ids!.Next(), ComboboxSelectionMode.Single), family, token, logger), requestRender);
        content = BuildContent;
        onCommitted = OnCommitted;
    }

    /// <summary>Gets the root's store; available from the first parameter set.</summary>
    public ComboboxStore<TValue> Store => root.Store;

    /// <summary>Gets a task that completes when the session handle is attached.</summary>
    public Task SessionAttached => session?.TwinAttached ?? Task.CompletedTask;

    /// <summary>Gets the list navigation the list's handles join.</summary>
    public ListNavigationBehavior List => list ?? throw new InvalidOperationException($"Blazix: the list navigation of `<{root.Store.Family}Root>` is created at its first parameter set.");

    /// <summary>Renders the store's fixed cascade around the child content, then the hidden input or inputs.</summary>
    /// <param name="builder">The root's render tree builder.</param>
    public void BuildRenderTree(RenderTreeBuilder builder) => root.BuildRenderTree(builder);

    /// <summary>Attaches the session, flushes list navigation, updates the hidden input's role and runs the batch boundary.</summary>
    /// <returns>A task that completes when that work has run.</returns>
    /// <remarks>Call it from the root's <c>OnAfterRenderAsync</c>, on every render.</remarks>
    public async Task OnAfterRenderAsync()
    {
        rendered = true;
        fieldBehavior?.AfterRender(inputs.Id, inputs.Name, HiddenInputId(root.Store));
        CopySessionInputs();
        Task sync = session!.AfterRenderAsync();
        Task flush = list!.FlushAsync();
        Task form = SyncFormAsync();
        await root.OnAfterRenderAsync();
        await sync;
        await flush;
        await form;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        committed?.Dispose();
        refreshSource?.TrySetResult();
        fieldBehavior?.Dispose();
        root.Dispose();
        list?.Dispose();
        if (formTwin is not null)
        {
            await formTwin.DisposeAsync();
        }

        if (session is not null)
        {
            await session.DisposeAsync();
        }
    }

    /// <summary>Creates the store at the first parameter set and reads the three controlled values.</summary>
    /// <param name="parameters">The root's parameters.</param>
    /// <param name="names">The names of the parameters the controlled values read.</param>
    /// <param name="idGenerator">The scoped id generator.</param>
    /// <param name="logger">The root's logger.</param>
    /// <remarks>Call it from the root's <c>SetParametersAsync</c>, before the parameters are assigned.</remarks>
    public void SetParameters(ParameterView parameters, ComboboxValueNames names, PartIdGenerator idGenerator, ILogger? logger)
    {
        ids = idGenerator;
        root.EnsureStore(idGenerator, logger);
        ComboboxStore<TValue> store = root.Store;
        if (session is null)
        {
            session = new InteractionsSessionBehavior(jsRuntime, store.SessionToken!, RouteAsync, dispatchException);
            list = new ListNavigationBehavior(store.SessionToken!, session.TwinAttached, RouteAsync, dispatchException)
            {
                Virtual = true,
                DisabledIndices = [],
                ResetOnPointerLeave = true,
            };
            list.HighlightChanged += report => _ = ObserveAsync(store.OnHighlightReportedAsync(report.Index, report.Event));
            list.Matched += match => _ = ObserveAsync(store.SelectTypeaheadMatchAsync(match.Index, match.Event));
            store.AttachListNavigation(list, list.RequestHighlightAsync);
            store.SessionInputsChanged += CopyAndSyncSession;
            store.SelectionCommitted += OnSelectionCommitted;
            committed = new SelectorSubscription<ComboboxStoreState>(store, onCommitted);
            committed.Select(ComboboxStoreState.Open);
            committed.Select(ComboboxStoreState.Status);
            committed.Select(static state => state.Lifecycle.OpenChange);
            committed.Select(static state => state.Inline);
            committed.Select(static state => state.Disabled);
            committed.Select(static state => state.SelectedIndex);
            committed.Select(static state => state.FilteredCount);
            committed.Select(static state => state.ElementIds);
            fieldBehavior = new ComboboxFieldBehavior<TValue>(store, () => Serialized(store), () => _ = ObserveAsync(SyncFormAsync()), RefreshFormAsync);
            store.NameResolver = fieldBehavior.NameOf;
            store.ControlBlurred = fieldBehavior.Blur;

            // AriaCombobox.tsx:838-850: a close by focus-out or an outside press from inside the popup touches the field and
            // commits its validation in onBlur mode, as a blur of the control does.
            store.ClosedFromInsidePopup += fieldBehavior.Blur;
            store.DispatchException = dispatchException;
        }

        store.ReadControlled(parameters, names);
        store.OpenState.SetParameters(parameters, "Open", "DefaultOpen");
    }

    /// <summary>Applies the root's inputs, derives the filtered items and sends the changed session and list options.</summary>
    /// <param name="next">The root's inputs.</param>
    /// <param name="parent">The enclosing popup's floating node, or <see langword="null"/>.</param>
    /// <param name="field">The enclosing field, or <see langword="null"/>.</param>
    /// <param name="form">The enclosing form, or <see langword="null"/>.</param>
    /// <param name="editContext">The enclosing edit context, or <see langword="null"/>.</param>
    /// <param name="rtl">Whether the reading direction is right to left.</param>
    /// <remarks>Call it from the root's <c>OnParametersSet</c>.</remarks>
    public void ApplyParameters(ComboboxRootInputs<TValue> next, IFloatingNodeContext? parent, FieldRootContext? field, IFormScope? form, EditContext? editContext, bool rtl)
    {
        ArgumentNullException.ThrowIfNull(next);
        fieldBehavior!.Apply(field, form, editContext, next);
        inputs = next;
        floatingParent = parent;
        ComboboxStore<TValue> store = root.Store;
        store.OnOpenChange = next.OnOpenChange;
        store.OpenChanged = next.OpenChanged;
        store.OnOpenChangeComplete = next.OnOpenChangeComplete;
        bool wasSelected = store.State.HasSelectedValue;
        int selection = store.State.SelectionRevision;
        store.ApplyInputs(next, field?.Disabled ?? false, field?.Labelable.ControlId);
        store.ApplyOpenParameters(store.OpenState.IsControlled ? store.OpenState.Value : null, next.DefaultOpen);
        if ((store.ValueState.IsControlled || store.ValuesState.IsControlled) && store.State.SelectionRevision == selection && wasSelected != store.State.HasSelectedValue)
        {
            // A controlled value the consumer changed: the selection's follow-ups (CMB-STA-20) run once.
            store.OnControlledSelectionChanged();
        }

        root.ChildContent = content;
        ApplyListOptions(store, rtl);
        CopySessionInputs();
    }

    /// <summary>Closes the popup with the reason <see cref="EventReason.ImperativeAction"/>.</summary>
    /// <returns>A task that completes when the close request has run.</returns>
    public Task CloseAsync() => root.Store.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.ImperativeAction));

    /// <summary>Removes the popup at once, ending its close animation or a held close.</summary>
    /// <returns>A task that completes when the unmount and its cleanup have run.</returns>
    public Task UnmountAsync() => root.Store.UnmountAsync();

    private static string Serialized(ComboboxStore<TValue> store)
    {
        // AriaCombobox.tsx:1676-1681: an array serializes as the empty string; none mode submits the input text.
        return store.State.SelectionMode switch
        {
            ComboboxSelectionMode.Single => store.State.HasSelectedValue ? store.ValueTextOf(store.SelectedValue) : string.Empty,
            ComboboxSelectionMode.None => store.State.InputValue,
            _ => string.Empty,
        };
    }

    private static void AddId(List<string> ids, string? id)
    {
        if (id is not null)
        {
            ids.Add(id);
        }
    }

    private void ApplyListOptions(ComboboxStore<TValue> store, bool rtl)
    {
        // CMB-KEY-01 (AriaCombobox.tsx:1413-1432): virtual focus, the open's focus, the hover and the grid.
        ComboboxStoreState state = store.State;
        ListNavigationBehavior navigation = list!;
        navigation.Enabled = !state.Disabled;
        navigation.LoopFocus = inputs.LoopFocus;
        navigation.AllowEscape = inputs.LoopFocus && inputs.AutoHighlight == ComboboxAutoHighlightMode.Off;
        navigation.FocusItemOnOpen = FocusItemOnOpen(store);
        navigation.FocusItemOnHover = inputs.HighlightItemOnHover;
        navigation.ResetOnPointerLeave = !inputs.KeepHighlight;
        navigation.Orientation = inputs.Grid ? "horizontal" : "vertical";
        navigation.Grid = inputs.Grid;
        navigation.Rtl = rtl;
        navigation.SelectedIndex = state.SelectedIndex;
        navigation.ItemCount = inputs.Virtualized && state.HasItems ? state.FilteredCount : null;
    }

    private void BuildContent(RenderTreeBuilder builder)
    {
        builder.AddContent(0, inputs.ChildContent);
        BuildHiddenInputs(builder);
    }

    // CMB-DOM-18/19 (AriaCombobox.tsx:1584-1721): the hidden input after the children, then one per value in multiple mode
    // with a name. The serialized value is rendered once under the handover and then written by the `form` module from
    // update (ADR 04 Amendments 2026-11-04, the form list additions; the Root's Writer row of combobox.md).
    private void BuildHiddenInputs(RenderTreeBuilder builder)
    {
        ComboboxStore<TValue> store = root.Store;
        ComboboxStoreState state = store.State;
        bool multiple = state.SelectionMode == ComboboxSelectionMode.Multiple;
        string? name = store.Name;
        string? hiddenName = HiddenName(store);
        bool hasMultipleSelection = multiple && store.SelectedValues.Count > 0;
        attachedSerialized ??= Serialized(store);

        builder.OpenRegion(1);
        builder.OpenElement(0, "input");
        builder.AddAttribute(1, "id", HiddenInputId(store));

        builder.AddAttribute(2, "form", store.Form);
        builder.AddAttribute(3, "name", hiddenName);
        builder.AddAttribute(4, "autocomplete", inputs.FormAutoComplete);
        builder.AddAttribute(5, "disabled", state.Disabled);
        builder.AddAttribute(6, "required", state.Required && !hasMultipleSelection);
        builder.AddAttribute(7, "readonly", state.ReadOnly);
        builder.AddAttribute(8, "value", attachedSerialized);
        builder.AddAttribute(9, "style", hiddenName is not null ? VisuallyHidden.InputStyle : VisuallyHidden.Style);
        builder.AddAttribute(10, "tabindex", "-1");
        builder.AddAttribute(11, "aria-hidden", "true");
        builder.AddElementReferenceCapture(12, element => store.HiddenInput = hiddenInput = element);
        builder.CloseElement();

        if (multiple && name is not null)
        {
            foreach (TValue value in store.SelectedValues)
            {
                // Unkeyed: two values may serialize alike (React only warns about the key), and an input holds no state.
                string serialized = store.ValueTextOf(value);
                builder.OpenElement(13, "input");
                builder.AddAttribute(14, "type", "hidden");
                builder.AddAttribute(15, "form", store.Form);
                builder.AddAttribute(16, "name", name);
                builder.AddAttribute(17, "value", serialized);
                builder.AddAttribute(18, "disabled", state.Disabled);
                builder.CloseElement();
            }
        }

        builder.CloseRegion();
    }

    // AriaCombobox.tsx:1589-1593, 1691: the name the hidden input submits, and its id when it has none.
    private static string? HiddenName(ComboboxStore<TValue> store)
    {
        ComboboxStoreState state = store.State;
        bool inputOwnsFormValue = state.SelectionMode == ComboboxSelectionMode.None && (state.Inline || !state.InputInsidePopup);
        return state.SelectionMode == ComboboxSelectionMode.Multiple || (state.SelectionMode == ComboboxSelectionMode.None && inputOwnsFormValue) ? null : store.Name;
    }

    private static string? HiddenInputId(ComboboxStore<TValue> store) => HiddenName(store) is null ? store.State.Id + "-hidden-input" : null;

    private async Task SyncFormAsync()
    {
        // The `form` module's Control role (ADR 04 Amendments 2026-11-04): the committed value through update, and the element the
        // hidden input hands its focus to (AriaCombobox.tsx:1614-1622): the trigger while the input is inside the popup, else the
        // input, else the trigger.
        ComboboxStore<TValue> store = root.Store;
        string serialized = Serialized(store);
        string? focusTarget = FocusTarget(store.State);
        if (formTwin is null)
        {
            formTwin = new FormTwin(jsRuntime);
            formTwin.Autofilled += report =>
            {
                if (!report.DefaultPrevented)
                {
                    _ = ObserveAsync(invokeAsync(() => root.Store.HandleAutofillAsync(report.Text, report.Event)));
                }
            };
            formTwin.ControlReported += report => _ = ObserveAsync(invokeAsync(() =>
            {
                OnControlReport(report);
                return Task.CompletedTask;
            }));
            formRevision = 1;
            sentConstraints = Constraints(store.State);
            formOptions = FormOptionsOf(serialized, focusTarget);
            formHandle = await formTwin.AttachAsync(hiddenInput, formOptions);

            // The field reads the hidden input's native state from a report: the first one now.
            _ = ObserveAsync(RefreshFormAsync());

            // A selection committed while the attach was in flight is sent once it lands.
            serialized = Serialized(store);
            focusTarget = FocusTarget(store.State);
        }

        if (formHandle is null)
        {
            return;
        }

        if (!string.Equals(formOptions?.Control?.Value, serialized, StringComparison.Ordinal))
        {
            formRevision++;
        }

        // A changed constraint changes the native state: the next update asks for a report of it.
        (bool Required, bool Disabled, bool ReadOnly) constraints = Constraints(store.State);
        if (constraints != sentConstraints)
        {
            sentConstraints = constraints;
            formRefreshes++;
        }

        if (formOptions?.Control is { } sent
            && sent.Revision == formRevision + formRefreshes
            && string.Equals(sent.Value, serialized, StringComparison.Ordinal)
            && string.Equals(sent.FocusTarget, focusTarget, StringComparison.Ordinal)
            && string.Equals(sent.FormId, fieldBehavior?.FormToken, StringComparison.Ordinal)
            && string.Equals(sent.CustomValidity, fieldBehavior?.OwnMessage, StringComparison.Ordinal))
        {
            return;
        }

        formOptions = FormOptionsOf(serialized, focusTarget);
        await formHandle.UpdateAsync(formOptions);
    }

    // DP6 (the checkable controls' and Select's refresh): a commit reads the hidden input's native state from a fresh report.
    // Only a field reads it, and only `required` constrains the hidden input.
    private Task RefreshFormAsync()
    {
        if (formHandle is null || disposed || fieldBehavior is not { InField: true } || !root.Store.State.Required)
        {
            return Task.CompletedTask;
        }

        refreshSource ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        formRefreshes++;
        refreshRevision = formRevision + formRefreshes;
        _ = ObserveAsync(SyncFormAsync());
        return refreshSource.Task;
    }

    private void OnControlReport(FormControlReport report)
    {
        if (disposed || !string.Equals(report.Type, "sync", StringComparison.Ordinal))
        {
            return;
        }

        if (report.Validity is { } validity)
        {
            fieldBehavior?.ReportValidity(validity);
        }

        if (refreshSource is { } source && report.Revision >= refreshRevision)
        {
            refreshSource = null;
            source.TrySetResult();
        }
    }

    private static (bool Required, bool Disabled, bool ReadOnly) Constraints(ComboboxStoreState state) => (state.Required, state.Disabled, state.ReadOnly);

    private static string? FocusTarget(ComboboxStoreState state) =>
        state.InputInsidePopup ? state.ElementIds.Trigger : state.ElementIds.Input ?? state.ElementIds.Trigger;

    private FormOptions FormOptionsOf(string serialized, string? focusTarget) => new()
    {
        Control = new FormControlOptions
        {
            FormId = fieldBehavior?.FormToken,
            CustomValidity = fieldBehavior?.OwnMessage,
            ReportEvents = false,
            ReportAutofill = true,
            Controlled = true,
            Value = serialized,
            Revision = formRevision + formRefreshes,
            FocusTarget = focusTarget,
        },
    };

    private Task RouteAsync(OpenChangeRequest request) => root.Store.RequestOpenAsync(request.Open, request.Details);

    // Base UI's root renders the hidden input's serialized value with the selection (AriaCombobox.tsx:1690-1700); a selection
    // that renders no root here (the input inside the popup fills nothing) still reaches the form module's control.
    private void OnSelectionCommitted()
    {
        if (rendered && !disposed)
        {
            // AriaCombobox.tsx:1243-1323 run at every render of the root, which a selection causes in React: the field's
            // filled state, its registration and the change's validation follow the selection with no render of the part.
            fieldBehavior!.AfterRender(inputs.Id, inputs.Name, HiddenInputId(root.Store));
            _ = ObserveAsync(SyncFormAsync());
        }
    }

    private void OnCommitted()
    {
        if (rendered && !disposed)
        {
            CopyAndSyncSession();
            _ = ObserveAsync(list!.FlushAsync());

            // The hidden input's focus target follows the parts' ids, which commit with no render of the root.
            _ = ObserveAsync(SyncFormAsync());
        }
    }

    private void CopyAndSyncSession()
    {
        if (rendered && !disposed)
        {
            CopySessionInputs();
            _ = ObserveAsync(session!.AfterRenderAsync());
        }
    }

    private void CopySessionInputs()
    {
        ComboboxStore<TValue> store = root.Store;
        ComboboxStoreState state = store.State;
        PopupLifecycleState lifecycle = state.Lifecycle;

        // AriaCombobox.tsx:1333-1340: the floating context is open while inline (CMB-ARIA-02).
        session!.Open = state.Inline || lifecycle.EffectiveOpen;
        session.FloatingId = lifecycle.FloatingId;

        // AriaCombobox.tsx:1336-1338: the floating root's reference is the trigger while the input is inside the popup (or there
        // is none); a trigger press with the input outside makes the trigger the DOM reference (ComboboxTrigger.tsx:196-199).
        session.ActiveTriggerId = store.DomReferenceTriggerId ?? (state.InputInsidePopup ? state.ElementIds.Trigger : null);
        session.Ending = lifecycle.DerivedTransitionStatus == TransitionStatus.Ending;
        session.Mounted = lifecycle.Mounted;
        session.Relay = lifecycle.OpenChange;
        session.ParentToken = floatingParent?.SessionToken;
        session.Dismiss = Dismiss(store);
        list!.SelectedIndex = state.SelectedIndex;

        // AriaCombobox.tsx:1425-1426 derives it at every render; the store's flag moves between parameter sets, and every
        // open commits through here before the list's options are flushed.
        list.FocusItemOnOpen = FocusItemOnOpen(store);
        if (inputs.Virtualized && state.HasItems)
        {
            list.ItemCount = state.FilteredCount;
        }
    }

    private bool? FocusItemOnOpen(ComboboxStore<TValue> store) =>
        store.QueryChangedAfterOpen || (store.State.SelectionMode == ComboboxSelectionMode.None && inputs.AutoHighlight == ComboboxAutoHighlightMode.Off) ? false : null;

    private InteractionsDismissOptions Dismiss(ComboboxStore<TValue> store)
    {
        ComboboxStoreState state = store.State;
        bool enabled = !state.Disabled && !state.Inline;

        // Design point 5 (a), settled by the orchestrator 2026-11-04: with items, nothing filtered and no empty state, Escape bubbles to a
        // parent popup (AriaCombobox.tsx:765-775) on both modes; the store's AllowPropagation covers the C# chain.
        bool bubbleEscape = state.Inline || (state.HasItems && state.FilteredCount == 0 && !store.EmptyMounted);
        ComboboxElementIds elementIds = state.ElementIds;
        DismissKey key = new(enabled, state.Inline, bubbleEscape, elementIds.Trigger, elementIds.Clear, elementIds.Chips, elementIds.InputGroup);
        if (dismiss is not null && key == dismissKey)
        {
            return dismiss;
        }

        dismissKey = key;
        List<string> exempt = [];
        AddId(exempt, elementIds.Trigger);
        AddId(exempt, elementIds.Clear);
        AddId(exempt, elementIds.Chips);
        AddId(exempt, elementIds.InputGroup);
        dismiss = new InteractionsDismissOptions
        {
            EscapeKey = enabled ? null : false,
            OutsidePress = enabled ? null : false,
            OutsidePressEvent = s_outsidePressEvent,
            BubblesEscapeKey = bubbleEscape ? true : null,
            BubblesOutsidePress = state.Inline ? true : null,
            OutsidePressExemptIds = exempt.Count > 0 ? exempt : null,
        };
        return dismiss;
    }

    private async Task ObserveAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception) when (!task.IsCanceled)
        {
            await dispatchException(exception);
        }
    }

    private readonly record struct DismissKey(bool Enabled, bool Inline, bool BubbleEscape, string? Trigger, string? Clear, string? Chips, string? InputGroup);
}

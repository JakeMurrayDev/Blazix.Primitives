using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives;

// combobox/clear/ComboboxClear.tsx (CMB §3.8): a native button by default, tabindex="-1", "x" by default, rendered iff kept
// mounted or mounted; Visible (the input text in no-selection mode, else a selected value) drives a TransitionsTwin with
// OpenSelector "[data-visible]" (CMB-ANI-04; the ItemIndicatorBehavior precedent: C# renders the initial status under the
// handover, the module reads the visibility from the applied batch and reports the exit's completion); its click clears with
// `clear-press` (CMB-EVT-05); the plain trigger mapping (data-popup-open only, §16 Q8 replicated). Its `combobox` handle
// prevents the mousedown default (CMB-PTR-07).
/// <summary>The behavior of a combobox-family clear button: its visibility, its transitions and its clear request.</summary>
internal sealed class ComboboxClearBehavior : IAsyncDisposable
{
    private const string OpenSelector = "[data-visible]";

    private readonly ComboboxPartServices services;

    private readonly ButtonBehavior button;

    private readonly Action<TransitionStatusChange> onStatus;

    private readonly Action<bool> onCompleted;

    private readonly TaskCompletionSource attached = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private ComboboxStore? store;

    private FieldRootContext? fieldScope;

    private ComboboxTwin? twin;

    private TwinHandle<ComboboxOptions>? handle;

    private ComboboxOptions? sentOptions;

    private TransitionsTwin? transitions;

    private TwinHandle<TransitionsOptions>? transitionsHandle;

    private TransitionsOptions? sentTransitions;

    private ElementReference? element;

    private string? id;

    private bool disabledParameter;

    private bool keepMounted;

    private bool visible;

    private bool mounted;

    private TransitionStatus? status;

    private TransitionStatus? renderedStatus;

    private bool initialized;

    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="ComboboxClearBehavior"/> class.</summary>
    /// <param name="services">The part's services.</param>
    /// <param name="developmentLog">The button mechanism's development log.</param>
    /// <param name="onKeyDown">The part's own <c>onkeydown</c> handler.</param>
    /// <param name="onKeyUp">The part's own <c>onkeyup</c> handler.</param>
    public ComboboxClearBehavior(ComboboxPartServices services, ButtonDevelopmentLog developmentLog, EventCallback onKeyDown, EventCallback onKeyUp)
    {
        this.services = services;
        button = new ButtonBehavior(services.JSRuntime, services.Logger, developmentLog, services.Ids, services.RequestRender, services.DispatchException, onKeyDown, onKeyUp);
        onStatus = change => _ = services.InvokeAsync(() =>
        {
            Mirror(change.Status, change.Mounted);
            return Task.CompletedTask;
        });
        onCompleted = open => _ = services.InvokeAsync(() =>
        {
            Complete(open);
            return Task.CompletedTask;
        });
    }

    /// <summary>Gets the root's store.</summary>
    public ComboboxStore Store => store ?? throw new InvalidOperationException("Blazix: the combobox clear button is used before its first render.");

    /// <summary>Gets a value indicating whether there is something to clear.</summary>
    public bool Visible => visible;

    /// <summary>Gets a value indicating whether the element renders: kept mounted, or mounted.</summary>
    public bool Shown => keepMounted || mounted;

    /// <summary>Gets the transition status the element renders.</summary>
    public TransitionStatus? TransitionStatus => status;

    /// <summary>Gets a value indicating whether the clear button is disabled, by the part, the root or the field.</summary>
    public bool Disabled => disabledParameter || Store.State.Disabled || (fieldScope?.Disabled ?? false);

    /// <summary>Gets a task that completes once the transitions handle has attached.</summary>
    public Task TwinAttached => attached.Task;

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
            store.SetElementIds(ids => ids.Clear == own ? ids with { Clear = null } : ids);
        }

        button.Dispose();
        if (transitions is not null)
        {
            transitions.StatusChanged -= onStatus;
            transitions.TransitionCompleted -= onCompleted;
            await transitions.DisposeAsync();
        }

        if (twin is not null)
        {
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
        SelectorSubscription<ComboboxStoreState> subscription = subscribe(store);
        subscription.Select(ComboboxStoreState.Open);
        subscription.Select(static state => state.Disabled);
        subscription.Select(static state => state.ReadOnly);
        subscription.Select(static state => state.ElementIds.Input);
        subscription.Select(IsVisible);
        twin = new ComboboxTwin(services.JSRuntime);
    }

    /// <summary>Applies the part's parameters.</summary>
    /// <param name="nativeButton">The part's <c>NativeButton</c>.</param>
    /// <param name="disabled">The part's <c>Disabled</c>.</param>
    /// <param name="keepMounted">The part's <c>KeepMounted</c>.</param>
    /// <param name="id">The id the part renders.</param>
    /// <param name="consumerAttributes">The consumer's attributes.</param>
    public void Apply(bool nativeButton, bool disabled, bool keepMounted, string? id, IReadOnlyDictionary<string, object>? consumerAttributes)
    {
        disabledParameter = disabled;
        this.keepMounted = keepMounted;
        this.id = id;
        string? own = id;
        Store.SetElementIds(ids => ids with { Clear = own });
        button.Native = nativeButton;
        button.FocusableWhenDisabled = null;
        button.Disabled = Disabled;
        button.SetConsumerAttributes(consumerAttributes);
    }

    /// <summary>Follows the visibility before a render: the first render takes the initial status, a change runs the machine.</summary>
    public void BeforeRender()
    {
        bool next = IsVisible(Store.State);
        if (!initialized)
        {
            initialized = true;
            visible = next;
            (mounted, status) = TransitionStatusRules.Initial(next, enableIdleState: false, animateInitialOpen: false);
            return;
        }

        if (next != visible)
        {
            visible = next;
            ApplyVisibility();
        }
    }

    /// <summary>Adds the clear button's attributes.</summary>
    /// <param name="attributes">The part's attributes.</param>
    /// <param name="onClick">The part's own <c>onclick</c> handler.</param>
    public void AddPartAttributes(AttributeList attributes, EventCallback onClick)
    {
        button.Disabled = Disabled;
        button.AddAttributes(attributes, id, composite: false);
        attributes.Set("tabindex", "-1");
        attributes.Set("onclick", onClick);

        // The handover value of an element created visible: the module owns the attribute from attach on.
        if (renderedStatus == Blazix.Primitives.TransitionStatus.Starting)
        {
            attributes.Set(ComboboxClearDataAttributes.StartingStyle, string.Empty);
        }
    }

    /// <summary>Determines whether the consumer's handler for an event renders on the element.</summary>
    /// <param name="eventName">The event's attribute name.</param>
    /// <returns><see langword="false"/> for a handler the part declines and runs itself.</returns>
    public bool ForwardsConsumerHandler(string eventName) => button.ForwardsConsumerHandler(eventName);

    /// <summary>Clears the input text, the selection and the highlight with <c>clear-press</c>, unless disabled or read-only.</summary>
    /// <param name="args">The click.</param>
    /// <returns>A task that completes when the clear has run.</returns>
    public async Task HandleClickAsync(MouseEventArgs args)
    {
        ComboboxStore root = Store;
        if (Disabled || root.State.ReadOnly)
        {
            return;
        }

        // ComboboxClear.tsx:86-112: the text and the value with one details object, then the highlight. The module's handle
        // moves the focus to the input on the click.
        EventSnapshot snapshot = EventSnapshot.FromEventArgs(args);
        await root.ClearAsync(EventReason.ClearPress, snapshot, shareDetails: false);
        await root.ClearHighlightAsync(snapshot);
    }

    /// <summary>Runs the declined consumer <c>onkeydown</c>.</summary>
    /// <param name="args">The keydown.</param>
    /// <returns>A task that completes when the handler has run.</returns>
    public Task HandleKeyDownAsync(KeyboardEventArgs args) => button.HandleKeyDownAsync(args);

    /// <summary>Runs the declined consumer <c>onkeyup</c>.</summary>
    /// <param name="args">The keyup.</param>
    /// <returns>A task that completes when the handler has run.</returns>
    public Task HandleKeyUpAsync(KeyboardEventArgs args) => button.HandleKeyUpAsync(args);

    /// <summary>Attaches the button, the press default and the transitions handle, and sends their changed options.</summary>
    /// <param name="rendered">The clear button's element, or <see langword="null"/> while it does not render.</param>
    /// <returns>A task that completes when the handles have their options.</returns>
    public async Task AfterRenderAsync(ElementReference? rendered)
    {
        if (disposed)
        {
            return;
        }

        element = Shown ? rendered : null;
        if (element is not { } target)
        {
            await ReleaseAsync();
            return;
        }

        if (transitionsHandle is not null && !string.Equals(transitionsHandle.Key, target.Id, StringComparison.Ordinal))
        {
            await ReleaseAsync();
        }

        button.Disabled = Disabled;
        await Task.WhenAll(button.AfterRenderAsync(true, target, id, composite: false), SyncTwinAsync(target), SyncTransitionsAsync(target));
    }

    // combobox/clear/ComboboxClear.tsx:47-54: the input text in no-selection mode, else a selected value or chip.
    private static bool IsVisible(ComboboxStoreState state) =>
        state.SelectionMode == ComboboxSelectionMode.None ? state.InputValue.Length != 0 : state.HasSelectedValue;

    private async Task SyncTwinAsync(ElementReference target)
    {
        if (twin is null)
        {
            return;
        }

        ComboboxOptions options = new()
        {
            Part = ComboboxTwin.ClearPart,
            SessionToken = Store.SessionToken,
            Disabled = Disabled,
            ReadOnly = Store.State.ReadOnly,
            InputId = Store.State.ElementIds.Input,
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

    private async Task SyncTransitionsAsync(ElementReference target)
    {
        if (transitionsHandle is not null)
        {
            // With OpenSelector the module reads the visibility from the applied batch; only KeepMounted is sent again.
            if (sentTransitions!.KeepMounted != keepMounted)
            {
                sentTransitions = sentTransitions with { KeepMounted = keepMounted };
                await transitionsHandle.UpdateAsync(sentTransitions);
            }

            return;
        }

        if (transitions is null)
        {
            transitions = new TransitionsTwin(services.JSRuntime);
            transitions.StatusChanged += onStatus;
            transitions.TransitionCompleted += onCompleted;
        }

        // Mounted and Status are what the element was rendered with, read on attach only.
        sentTransitions = new TransitionsOptions
        {
            Open = visible,
            Mounted = mounted,
            Status = renderedStatus,
            KeepMounted = keepMounted,
            OpenSelector = OpenSelector,
        };
        transitionsHandle = await transitions.AttachAsync(target, sentTransitions);
        attached.TrySetResult();
    }

    private async Task ReleaseAsync()
    {
        if (transitionsHandle is { } released)
        {
            transitionsHandle = null;
            sentTransitions = null;
            await released.DisposeAsync();
        }

        if (handle is { } pressDefault)
        {
            handle = null;
            sentOptions = null;
            await pressDefault.DisposeAsync();
        }
    }

    // The C# half of the transition machine for this render (TransitionStatusRules): an element created visible starts in
    // 'starting'; an element with no transitions handle to animate it leaves at once.
    private void ApplyVisibility()
    {
        bool creating = visible && !mounted && (!keepMounted || transitionsHandle is null);
        (bool nextMounted, TransitionStatus? nextStatus) = TransitionStatusRules.Evaluate(visible, mounted, status, deferEndingState: false);
        if (creating)
        {
            renderedStatus = nextStatus;
        }
        else if (!visible && mounted && transitionsHandle is null && element is null)
        {
            nextMounted = false;
            nextStatus = null;
            renderedStatus = null;
        }

        mounted = nextMounted;
        status = nextStatus;
    }

    private void Mirror(TransitionStatus? next, bool nextMounted)
    {
        if (disposed || (next == status && nextMounted == mounted))
        {
            return;
        }

        status = next;
        mounted = nextMounted;
        services.RequestRender();
    }

    // CMB-ANI-04 (useOpenChangeComplete): the exit finished, so the element unmounts (unless kept) and its status is unset.
    private void Complete(bool open)
    {
        if (disposed || open || visible)
        {
            return;
        }

        mounted = false;
        status = null;
        renderedStatus = null;
        services.RequestRender();
    }
}

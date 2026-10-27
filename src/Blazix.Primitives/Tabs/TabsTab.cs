using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// TAB-STA-20; tabs/tab/TabsTab.tsx:179-184,235-252. The direction is mirrored (ADR 04 Amendments 2026-10-26).
/// <summary>Represents the state of a tab, which the part exposes as data attributes and passes to its class and style functions.</summary>
/// <param name="Disabled">A value indicating whether the tab cannot be activated; projected as <c>data-disabled</c>.</param>
/// <param name="Active">A value indicating whether the tab is the active one; projected as <c>data-active</c>.</param>
/// <param name="Orientation">The axis along which the tabs are laid out; projected as <c>data-orientation</c>.</param>
/// <param name="TabActivationDirection">The direction from the previously active tab to the active one, rendered as <c>data-activation-direction</c> by the browser-side behavior.</param>
[PartState]
public sealed partial record TabsTabState(bool Disabled, bool Active, Orientation Orientation, [property: MirroredField] TabsActivationDirection TabActivationDirection);

// TAB §3.3; tabs/tab/TabsTabDataAttributes.ts, in its order.
/// <summary>Provides the names of the data attributes a tab renders.</summary>
public static class TabsTabDataAttributes
{
    /// <summary>Represents the attribute whose value is the activation direction, <c>left</c>, <c>right</c>, <c>up</c>, <c>down</c> or <c>none</c>: <c>data-activation-direction</c>.</summary>
    public const string ActivationDirection = "data-activation-direction";

    /// <summary>Represents the attribute whose value is the orientation, <c>horizontal</c> or <c>vertical</c>: <c>data-orientation</c>.</summary>
    public const string Orientation = "data-orientation";

    /// <summary>Represents the attribute present, with an empty value, while the tab is disabled: <c>data-disabled</c>.</summary>
    public const string Disabled = "data-disabled";

    /// <summary>Represents the attribute present, with an empty value, while the tab is the active one: <c>data-active</c>.</summary>
    public const string Active = "data-active";
}

// Base UI's Tabs.Tab (tabs/tab/TabsTab.tsx); the contract is docs/components/tabs.md.
// - A `button` with role="tab" over the writer's role, aria-selected, aria-controls from the Root's panel registry and
//   the composite marker data-composite-item-active while active (TAB-STR-03, TAB-ARIA-03..05, TAB-DOM-04..10).
// - The button mechanism (ButtonBehavior) with focusableWhenDisabled and composite mode always: a Tab outside a List
//   throws (TAB-EDGE-02), so UBT-STA-01's inference is always true. ButtonAttributeWriter gives type="button",
//   aria-disabled "true"/"false" in native mode and never `disabled` (TAB-DOM-10, TAB-ARIA-06, §16 Q7).
// - A composite item of its List (CompositeItemBehavior): tabindex="-1" on every render, the composite's from attach
//   (ADR 04 Amendments 2026-10-22). One keydown gate (option (i), the RadioRoot precedent): arrows go to the item, every
//   other key to the button mechanism, so a key goes through at most one decision.
// - The activation is the `tabs` module's (ADR 04 Amendments 2026-10-26, chosen by the user 2026-10-26): it listens for
//   the Tab's click and focus on the List and asks `select`. The Tab declines the consumer's onclick and onfocus and
//   runs them from the List: inside the decision on WebAssembly, from the module's report on Server, and from the
//   module's report for a gesture that activates nothing (design point 5 of the Tabs track, settled by the orchestrator
//   2026-10-26). A Tab that is active or disabled requests nothing (TAB-PTR-02/03).
// - The Tab's value and disabled reach the Root's registry through its entry, in the order the List's composite module
//   reports (track 1's E2, TAB-STA-04); a change of either re-validates the Root after the Tab renders. The tab stop
//   follows the active Tab through the composite module (track 1's E5, TAB-FOC-04..07).
/// <summary>
/// Represents one tab of a tab list: Base UI's <c>Tabs.Tab</c>, rendered as a <c>button</c> with <c>role="tab"</c>.
/// Clicking the tab, or pressing Enter or Space on it, makes it the active tab.
/// </summary>
/// <typeparam name="TValue">The type of the tab's value, which is the type of its root's value.</typeparam>
/// <remarks>The part must be placed within a <see cref="TabsList"/>.</remarks>
public sealed class TabsTab<TValue> : PartBase<TabsTabState>
{
    private const string DefaultTag = "button";
    private const string ArrowKeyPrefix = "Arrow";
    private const string ClickType = "click";

    private readonly EventCallback onKeyDown;
    private readonly EventCallback onKeyUp;
    private readonly Action<bool> rootChanged;
    private readonly Entry entry;
    private TabsRootContext<TValue>? root;
    private TabsListContext? list;
    private ButtonBehavior? behavior;
    private CompositeItemBehavior? item;
    private TabsTabState? state;
    private AttributeValue consumerClick;
    private AttributeValue consumerFocus;
    private string? renderedPanelId;
    private string? renderedDirection;
    private bool registered;
    private TValue publishedValue = default!;
    private bool publishedDisabled;
    private bool metadataChanged;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="TabsTab{TValue}"/> class.</summary>
    public TabsTab()
    {
        onKeyDown = CreateHandler<KeyboardEventArgs>("onkeydown", HandleKeyDownAsync);
        onKeyUp = CreateHandler<KeyboardEventArgs>("onkeyup", e => behavior!.HandleKeyUpAsync(e));
        rootChanged = OnRootChanged;
        entry = new Entry(this);
    }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private ILogger<TabsTab<TValue>> Logger { get; set; } = null!;

    [Inject]
    private ButtonDevelopmentLog DevelopmentLog { get; set; } = null!;

    [Inject]
    private PartIdGenerator IdGenerator { get; set; } = null!;

    private bool IsActive => root!.IsActive(Value);

    // Design point R7 of M2 (settled by the orchestrator 2026-10-23): Blazor supplies a cascading value to a parameter of
    // an assignable type, so a Tab whose TValue differs from its Root's still receives the Root as this base.
    [CascadingParameter]
    private TabsRootContext? AnyRootContext { get; set; }

    [CascadingParameter]
    private TabsRootContext<TValue>? RootContext { get; set; }

    [CascadingParameter]
    private TabsListContext? ListContext { get; set; }

    [CascadingParameter]
    private CompositeRootContext? CompositeContext { get; set; }

    // TAB-API-07, TAB-EDGE-07
    /// <summary>Gets or sets the value that identifies the tab.</summary>
    /// <value>
    /// The value, compared with the root's by identity for a reference type other than <see cref="string"/>, by value for a
    /// value type and ordinally for a <see cref="string"/>. <see langword="null"/> is never active.
    /// </value>
    [Parameter, EditorRequired]
    public TValue Value { get; set; } = default!;

    // TAB-API-08
    /// <summary>Gets or sets a value indicating whether the tab cannot be activated.</summary>
    /// <value>
    /// <see langword="true"/> to ignore clicks, Enter, Space and focus activation, which also withholds the consumer's
    /// <c>onclick</c>, <c>onpointerdown</c>, <c>onkeydown</c> and <c>onkeyup</c> handlers; the tab stays focusable and
    /// reachable with the arrow keys. The default is <see langword="false"/>.
    /// </value>
    [Parameter]
    public bool Disabled { get; set; }

    // TAB-API-09, UBT-API-03
    /// <summary>Gets or sets a value indicating whether the rendered element is a native <c>button</c>.</summary>
    /// <value>
    /// <see langword="true"/>, the default, for a <c>button</c> element; <see langword="false"/> when
    /// <see cref="PartBase{TState}.Render"/> supplies another element, such as an <c>a</c>, which then gets clicks from
    /// Enter and Space.
    /// </value>
    [Parameter]
    public bool NativeButton { get; set; } = true;

    /// <inheritdoc />
    protected override string DefaultTagName => DefaultTag;

    /// <inheritdoc />
    protected override TabsTabState State => state!;

    // TAB-DOM-04, TAB-API-10: the tab always has an id, the consumer's or a generated one.
    /// <inheritdoc />
    protected override bool UsesGeneratedId => true;

    // For the fixtures (tests/Blazix.Primitives.Fixtures.Client): a browser test waits for the button module's listeners.
    /// <summary>Gets a task that completes when the tab's button behavior is attached to its element.</summary>
    /// <value>A task that completes after the first interactive render has attached it.</value>
    internal Task TwinAttached => behavior!.TwinAttached;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// The tab is not placed within a <see cref="TabsRoot{TValue}"/> and a <see cref="TabsList"/>, or its
    /// <typeparamref name="TValue"/> differs from the root's.
    /// </exception>
    protected override void OnInitialized()
    {
        // TAB-EDGE-01, then TAB-EDGE-02: the Root lookup runs first (tabs/tab/TabsTab.tsx:42-51).
        TabsRootContext.Require(AnyRootContext);
        if (RootContext is null)
        {
            // Design point R7 of M2 (ADR 02 Amendments 2026-10-20, part-misuse errors are always on).
            throw new InvalidOperationException(
                "Blazix: A `TabsTab` must have the `TValue` of its `TabsRoot`. Set `TValue` on the tab, or give `Value` the root's type, for example `Value=\"(int?)1\"` in a `TabsRoot` of `int?`.");
        }

        root = RootContext;
        list = TabsListContext.Require(ListContext);
        root.AddTabListener(rootChanged);
        behavior = new ButtonBehavior(JSRuntime, Logger, DevelopmentLog, IdGenerator, StateHasChanged, DispatchExceptionAsync, onKeyDown, onKeyUp)
        {
            FocusableWhenDisabled = true,
        };
        item = new CompositeItemBehavior(CompositeContext, "TabsTab", onKeyDown);
        base.OnInitialized();
    }

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        behavior!.Disabled = Disabled;
        behavior.Native = NativeButton;
        behavior.SetConsumerAttributes(AdditionalAttributes);
        item!.SetRegistration(Id, AdditionalAttributes);

        // TAB-PTR-09, UBT-EVT-03: a disabled tab's key handlers are not invoked, so the List's key handling runs none either.
        item.SetConsumerAttributes(Disabled ? null : AdditionalAttributes);
        bool hadClick = consumerClick.IsHandler;
        bool hadFocus = consumerFocus.IsHandler;
        consumerClick = FindConsumerHandler("onclick");
        consumerFocus = FindConsumerHandler("onfocus");
        if (!registered)
        {
            registered = true;
            list!.Register(entry);
        }
        else
        {
            if (hadClick != consumerClick.IsHandler || hadFocus != consumerFocus.IsHandler)
            {
                list!.HandlersChanged();
            }

            // TAB-STA-04: the metadata is re-published when the value or `disabled` changes; the order is the List's report.
            metadataChanged |= publishedDisabled != Disabled || !Equality.Is(publishedValue, Value);
        }

        publishedValue = Value;
        publishedDisabled = Disabled;
        UpdateState();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (disposed)
        {
            return;
        }

        await behavior!.AfterRenderAsync(Enabled, Element, Id, composite: true);
        await item!.AfterRenderAsync(Element);

        // TAB-STA-06..12: post-commit C# state work, as Base UI's layout effect on the registry (ADR 01's "EVD-STA-07,
        // CMP-STA-10, FFI registration" row).
        if (metadataChanged)
        {
            metadataChanged = false;
            await root!.TabChangedAsync();
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            root?.RemoveTabListener(rootChanged);
            behavior?.Dispose();
            item?.Dispose();
            if (registered)
            {
                registered = false;
                list!.Unregister(entry);
            }
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override void AddPartAttributes(AttributeList attributes)
    {
        // TAB-DOM-10, TAB-ARIA-06: the focusable-when-disabled composite button (type, aria-disabled, no disabled).
        behavior!.AddAttributes(attributes, Id, composite: true);

        // TAB-DOM-09: the composite item's tabindex replaces the writer's; a consumer tabindex still wins at the merge.
        item!.AddAttributes(attributes);

        // TAB-ARIA-03/04: role="tab" over the writer's role="button"; the consumer's role still wins at the merge.
        bool active = IsActive;
        attributes.Set("role", "tab");
        attributes.Set("aria-selected", active ? "true" : "false");

        // TAB-ARIA-05, TAB-STA-21: the panel registered for the tab's value.
        renderedPanelId = root!.PanelIdFor(Value);
        if (renderedPanelId is not null)
        {
            attributes.Set("aria-controls", renderedPanelId);
        }

        // TAB-DOM-08: the marker the composite reads for the initial tab stop.
        if (active)
        {
            attributes.Set(CompositeAttributes.ActiveItem, string.Empty);
        }

        // ADR 04 Amendments 2026-10-18 and 2026-10-26: the direction the element is created with (the handover).
        renderedDirection ??= TabsLiterals.ToLiteral(root.Direction);
        attributes.Set(TabsTabDataAttributes.ActivationDirection, renderedDirection);
    }

    // The tabs module runs the consumer's onclick and onfocus (design point 5, settled by the orchestrator 2026-10-26);
    // the button mechanism and the composite item decline their own.
    /// <inheritdoc />
    protected override bool ForwardsConsumerHandler(string eventName)
    {
        if (string.Equals(eventName, "onclick", StringComparison.OrdinalIgnoreCase) || string.Equals(eventName, "onfocus", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return behavior!.ForwardsConsumerHandler(eventName) && item!.ForwardsConsumerHandler(eventName);
    }

    private static MouseEventArgs ToMouseEventArgs(EventSnapshot snapshot)
    {
        return new MouseEventArgs
        {
            Type = snapshot.Type,
            Detail = snapshot.Detail,
            Buttons = snapshot.Buttons,
            AltKey = snapshot.AltKey,
            CtrlKey = snapshot.CtrlKey,
            MetaKey = snapshot.MetaKey,
            ShiftKey = snapshot.ShiftKey,
        };
    }

    private static FocusEventArgs ToFocusEventArgs(EventSnapshot snapshot) => new() { Type = snapshot.Type };

    private AttributeValue FindConsumerHandler(string eventName)
    {
        if (AdditionalAttributes is null || !AdditionalAttributes.TryGetValue(eventName, out object? value))
        {
            return default;
        }

        AttributeValue handler = AttributeValue.FromObject(value);
        return MergeProps.IsHandler(eventName, handler) ? handler : default;
    }

    // Option (i) of ADR 04's note of 2026-10-22 (chosen by the user 2026-10-23), the RadioRoot gate: the button mechanism and
    // the composite item both decline the consumer's onkeydown, and a key reaches at most one decision point (Space the
    // `button` module's `activate`, an arrow the composite's `navigate`).
    private Task HandleKeyDownAsync(KeyboardEventArgs e)
    {
        return e.Key is { } key && key.StartsWith(ArrowKeyPrefix, StringComparison.Ordinal)
            ? item!.HandleKeyDownAsync(e)
            : behavior!.HandleKeyDownAsync(e);
    }

    // TAB-PTR-09: the consumer's handler for a click (never while disabled, UBT-EVT-03) or a focus, before the Tab's own.
    private EventArgs? RunConsumerHandler(EventSnapshot snapshot, out Task task)
    {
        task = Task.CompletedTask;
        if (string.Equals(snapshot.Type, ClickType, StringComparison.Ordinal))
        {
            if (!consumerClick.IsHandler || Disabled)
            {
                return null;
            }

            MouseEventArgs click = ToMouseEventArgs(snapshot);
            task = consumerClick.InvokeAsync(click);
            return click;
        }

        if (!consumerFocus.IsHandler)
        {
            return null;
        }

        FocusEventArgs focus = ToFocusEventArgs(snapshot);
        task = consumerFocus.InvokeAsync(focus);
        return focus;
    }

    // TAB-STA-03, TAB-EVT-02: reason none, the click or focus event, the direction measured in its dispatch; nothing for an
    // active or disabled tab (TAB-PTR-02/03).
    private Task<bool> Request(EventSnapshot snapshot, TabsActivationDirection direction)
    {
        if (Disabled || IsActive)
        {
            return Task.FromResult(false);
        }

        return root!.Request(Value, new TabsChangeEventDetails(EventReason.None, direction, snapshot));
    }

    private DecisionPolicy DecideActivation(EventSnapshot snapshot, TabsActivationDirection direction)
    {
        // Only the synchronous part of a handler can veto; an asynchronous rest runs on (ADR 04 §3).
        EventArgs? args;
        Task consumer;
        try
        {
            args = RunConsumerHandler(snapshot, out consumer);
        }
        catch (Exception exception)
        {
            _ = DispatchExceptionAsync(exception);
            return DecisionPolicy.Run;
        }

        Observe(consumer);
        if (args is not null && args.PartHandlerPrevented())
        {
            return DecisionPolicy.Stop;
        }

        try
        {
            Observe(Request(snapshot, direction));
        }
        catch (Exception exception)
        {
            _ = DispatchExceptionAsync(exception);
        }

        return DecisionPolicy.Run;
    }

    private async Task ActivateAsync(EventSnapshot snapshot, TabsActivationDirection direction)
    {
        EventArgs? args = RunConsumerHandler(snapshot, out Task consumer);
        await consumer;
        if (args is not null && args.PartHandlerPrevented())
        {
            return;
        }

        await Request(snapshot, direction);
    }

    private Task RunConsumerHandlerAsync(EventSnapshot snapshot)
    {
        RunConsumerHandler(snapshot, out Task task);
        return task;
    }

    private void Observe(Task task)
    {
        if (!task.IsCompleted)
        {
            _ = ObserveAsync(task);
        }
        else if (task.IsFaulted)
        {
            _ = DispatchExceptionAsync(task.Exception!.GetBaseException());
        }
    }

    private async Task ObserveAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception exception) when (!task.IsCanceled)
        {
            await DispatchExceptionAsync(exception);
        }
    }

    // A change of the root's value, orientation, mirrored direction or panel registry. In the root's first pass only a tab
    // that becomes active renders, so it renders before the tab it replaces (TabsRootContext.AddTabListener).
    private void OnRootChanged(bool activationPass)
    {
        if (disposed || (activationPass && (state!.Active || !IsActive)))
        {
            return;
        }

        if (UpdateState() || !string.Equals(root!.PanelIdFor(Value), renderedPanelId, StringComparison.Ordinal))
        {
            StateHasChanged();
        }
    }

    // Returns whether the state changed; a new record is made only then (ADR 16 rule 7).
    private bool UpdateState()
    {
        bool active = IsActive;
        Orientation orientation = root!.Orientation;
        TabsActivationDirection direction = root.Direction;
        if (state is { } current
            && current.Disabled == Disabled
            && current.Active == active
            && current.Orientation == orientation
            && current.TabActivationDirection == direction)
        {
            return false;
        }

        state = new TabsTabState(Disabled, active, orientation, direction);
        return true;
    }

    /// <summary>The tab as its list's registry sees it.</summary>
    private sealed class Entry(TabsTab<TValue> tab) : TabsTabEntry<TValue>
    {
        /// <inheritdoc />
        public override string ElementId => tab.Id ?? string.Empty;

        /// <inheritdoc />
        public override TValue Value => tab.Value;

        /// <inheritdoc />
        public override bool Disabled => tab.Disabled;

        /// <inheritdoc />
        public override bool HasConsumerClick => tab.consumerClick.IsHandler;

        /// <inheritdoc />
        public override bool HasConsumerFocus => tab.consumerFocus.IsHandler;

        /// <inheritdoc />
        public override DecisionPolicy DecideActivation(EventSnapshot snapshot, TabsActivationDirection direction) => tab.DecideActivation(snapshot, direction);

        /// <inheritdoc />
        public override Task ActivateAsync(EventSnapshot snapshot, TabsActivationDirection direction) => tab.ActivateAsync(snapshot, direction);

        /// <inheritdoc />
        public override Task RunConsumerHandlerAsync(EventSnapshot snapshot) => tab.RunConsumerHandlerAsync(snapshot);
    }
}

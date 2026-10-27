using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives;

// Base UI's AccordionItemContext (accordion/item/AccordionItemContext.ts): the default trigger id and its three-state
// registration (ACC-STA-11; accordion/item/AccordionItem.tsx:98-102: undefined = the generated id, null = a trigger registered
// and then unmounted), the index (ACC-STA-13) and the item's state. A plain listener list cascaded fixed (ADR 16 rule 9). The
// non-generic base lets a part whose TValue differs from its item's report the mismatch (design point R7's pattern).
/// <summary>The part of an accordion item's shared state that does not depend on the type of its value.</summary>
internal abstract class AccordionItemContext
{
    private readonly List<Action> listeners = [];
    private string? registeredTriggerId;
    private bool triggerUnregistered;

    /// <summary>Initializes a new instance of the <see cref="AccordionItemContext"/> class.</summary>
    /// <param name="defaultTriggerId">The generated id a trigger without an id of its own renders.</param>
    protected AccordionItemContext(string defaultTriggerId)
    {
        DefaultTriggerId = defaultTriggerId;
    }

    /// <summary>Gets the generated id a trigger without an id of its own renders.</summary>
    /// <value>The item's default trigger id.</value>
    public string DefaultTriggerId { get; }

    /// <summary>Gets the id the panel's <c>aria-labelledby</c> references.</summary>
    /// <value>
    /// The registered trigger's id, the default trigger id while no trigger registered one, or <see langword="null"/> once a
    /// trigger unregistered.
    /// </value>
    public string? TriggerId => triggerUnregistered ? null : registeredTriggerId ?? DefaultTriggerId;

    /// <summary>Adds a listener that runs after every change of the item's state or trigger id.</summary>
    /// <param name="listener">The listener.</param>
    public void AddListener(Action listener) => listeners.Add(listener);

    /// <summary>Removes a listener.</summary>
    /// <param name="listener">The listener.</param>
    public void RemoveListener(Action listener) => listeners.Remove(listener);

    // AccordionTrigger.tsx:53-58: register (registeredId ?? (current === null ? undefined : current)).
    /// <summary>Registers a trigger and its id.</summary>
    /// <param name="registeredId">The trigger's non-empty id, or <see langword="null"/> for a trigger that uses the default id.</param>
    public void RegisterTrigger(string? registeredId)
    {
        string? before = TriggerId;
        if (registeredId is not null)
        {
            registeredTriggerId = registeredId;
            triggerUnregistered = false;
        }
        else if (triggerUnregistered)
        {
            triggerUnregistered = false;
        }

        PublishIfChanged(before);
    }

    // AccordionTrigger.tsx:56: unregister only the id that is registered (current === registeredId ? null : current).
    /// <summary>Unregisters a trigger.</summary>
    /// <param name="registeredId">The id the trigger registered, or <see langword="null"/> for one that used the default id.</param>
    public void UnregisterTrigger(string? registeredId)
    {
        bool registered = registeredId is null
            ? registeredTriggerId is null && !triggerUnregistered
            : !triggerUnregistered && string.Equals(registeredTriggerId, registeredId, StringComparison.Ordinal);
        if (registered)
        {
            string? before = TriggerId;
            registeredTriggerId = null;
            triggerUnregistered = true;
            PublishIfChanged(before);
        }
    }

    /// <summary>Runs every listener, in the order they were added.</summary>
    protected void Publish()
    {
        for (int i = 0; i < listeners.Count; i++)
        {
            listeners[i]();
        }
    }

    private void PublishIfChanged(string? before)
    {
        if (!string.Equals(before, TriggerId, StringComparison.Ordinal))
        {
            Publish();
        }
    }
}

// Base UI's AccordionItem body (accordion/item/AccordionItem.tsx:39-127):
// - the identity (ACC-API-10) and the derived open flag, tested with SameValueZero (ACC-STA-04);
// - its CollapsibleRootContext, driven through OpenState.Update(true, open, false, false) and ApplyParameters (ACC §4.1: always
//   consumer-owned; ACC-STA-09);
// - the chain the collapsible's OnOpenChange runs (settled by the orchestrator 2026-10-25, the CheckboxGroup C3 precedent): the
//   consumer's Item callback, then, unless it canceled, the Root's request with the same details, whose commit notifies every
//   Item before the chain returns (ACC-EVT-01..04, EVD-EVT-04; design point D2 of track 6, option (a), settled by the orchestrator 2026-10-26);
// - the item's state (ACC-STA-02, ACC-STA-10), rebuilt when the root, the collapsible state or the parameters change.
/// <summary>The state an accordion item shares with its header, trigger and panel.</summary>
/// <typeparam name="TValue">The type of the items' values.</typeparam>
internal sealed class AccordionItemContext<TValue> : AccordionItemContext, IDisposable
{
    private readonly Action rootChanged;
    private readonly IDisposable collapsibleSubscription;
    private bool disabled;
    private bool applied;

    /// <summary>Initializes a new instance of the <see cref="AccordionItemContext{TValue}"/> class.</summary>
    /// <param name="root">The shared state of the item's root.</param>
    /// <param name="collapsible">The item's collapsible state, which its trigger and panel run on.</param>
    /// <param name="defaultTriggerId">The generated id a trigger without an id of its own renders.</param>
    public AccordionItemContext(AccordionRootContext<TValue> root, CollapsibleRootContext collapsible, string defaultTriggerId)
        : base(defaultTriggerId)
    {
        Root = root;
        Collapsible = collapsible;
        // ACC-STA-03/09, ACC-EDGE-14 (D5 (a)): no root element mirrors the item's status, so its panel reports it.
        collapsible.PanelReportsStatus = true;
        collapsible.OnOpenChange = new EventCallback<ValueChangeEventArgs<bool>>(null, (Func<ValueChangeEventArgs<bool>, Task>)ChainAsync);
        rootChanged = Sync;
        root.AddListener(rootChanged);
        collapsibleSubscription = collapsible.Subscribe(_ => Rebuild());
        State = new AccordionItemState<TValue>(root.Value, false, root.Orientation, Hidden: true, Index: -1, Open: false);
    }

    /// <summary>Gets the shared state of the item's root.</summary>
    /// <value>The root's context.</value>
    public AccordionRootContext<TValue> Root { get; }

    /// <summary>Gets the item's collapsible state.</summary>
    /// <value>The context the item cascades to its trigger and panel, and to Collapsible parts inside it.</value>
    public CollapsibleRootContext Collapsible { get; }

    /// <summary>Gets the item's identity in the root's value.</summary>
    /// <value>The item's value, or its generated id.</value>
    public TValue Identity { get; private set; } = default!;

    /// <summary>Gets the item's state, which its header and trigger render and its panel extends.</summary>
    /// <value>The current state; a new record after every change.</value>
    public AccordionItemState<TValue> State { get; private set; }

    /// <summary>Gets or sets the consumer's callback that the item's request runs first.</summary>
    /// <value>The item's <c>OnOpenChange</c>; the latest one is always the one invoked.</value>
    public EventCallback<ValueChangeEventArgs<bool>> OnOpenChange { get; set; }

    /// <inheritdoc />
    public void Dispose()
    {
        Root.RemoveListener(rootChanged);
        collapsibleSubscription.Dispose();
    }

    /// <summary>Applies the item's parameters of one parameter set.</summary>
    /// <param name="identity">The item's identity.</param>
    /// <param name="itemDisabled">The item's own <c>Disabled</c>.</param>
    public void ApplyParameters(TValue identity, bool itemDisabled)
    {
        Identity = identity;
        disabled = itemDisabled;
        Sync();
    }

    // ACC-STA-13: the item's position in the order its root's registration-only list reported, -1 before the first report.
    /// <summary>Sets the item's index and publishes the new state when it changed.</summary>
    /// <param name="index">The index the list reported for the item, or -1.</param>
    public void SetIndex(int index)
    {
        if (State.Index != index)
        {
            State = State with { Index = index };
            Publish();
        }
    }

    private void Sync()
    {
        // ACC-API-11: the effective disabled; ACC-STA-04: the derived open flag.
        bool effectiveDisabled = disabled || Root.Disabled;
        bool open = AccordionValues.Contains(Root.Value, Identity);
        Collapsible.OpenState.Update(true, open, false, false);
        Collapsible.ApplyParameters(effectiveDisabled, firstSet: !applied);
        applied = true;
        Rebuild();
    }

    private void Rebuild()
    {
        CollapsibleRootStoreState shared = Collapsible.State;
        bool hidden = !shared.Open && !shared.Mounted;
        AccordionItemState<TValue> current = State;
        if (ReferenceEquals(current.Value, Root.Value) && current.Disabled == shared.Disabled && current.Orientation == Root.Orientation
            && current.Hidden == hidden && current.Open == shared.Open)
        {
            return;
        }

        State = current with { Value = Root.Value, Disabled = shared.Disabled, Orientation = Root.Orientation, Hidden = hidden, Open = shared.Open };
        Publish();
    }

    private async Task ChainAsync(ValueChangeEventArgs<bool> args)
    {
        await OnOpenChange.InvokeAsync(args);
        if (args.Details.IsCanceled)
        {
            return;
        }

        await Root.RequestAsync(Identity, args.Value, args.Details);
    }
}

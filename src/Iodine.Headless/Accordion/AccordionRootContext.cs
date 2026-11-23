using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace Blazix.Primitives;

// Base UI's AccordionRootContext (accordion/root/AccordionRootContext.ts): the Root-level settings every Item and Panel reads
// (ACC-API-05..07, ACC-API-11/16/17) and the value with its request (ACC-STA-04..07, ACC-EVT-03/04/09). A plain listener list
// cascaded fixed (ADR 16 rule 9; the RadioGroupContext precedent, design point R3 of M2): a change of the value or a setting
// notifies every Item, which recomputes what it renders. Design point R7 (settled 2026-10-23): this non-generic base lets an
// Item whose TValue differs from its Root's report the mismatch, and lets a Panel read the Root's defaults first (ACC-EDGE-03).
/// <summary>The part of an accordion root's shared state that does not depend on the type of its value.</summary>
internal abstract class AccordionRootContext
{
    private readonly List<Action> listeners = [];

    /// <summary>Gets a value indicating whether every item ignores user interaction.</summary>
    /// <value>The root's <c>Disabled</c> parameter.</value>
    public bool Disabled { get; protected set; }

    /// <summary>Gets the orientation every part reflects in its state.</summary>
    /// <value>The root's <c>Orientation</c> parameter; <see cref="Orientation.Vertical"/> by default.</value>
    public Orientation Orientation { get; protected set; } = Orientation.Vertical;

    /// <summary>Gets a value indicating whether a panel without its own <c>KeepMounted</c> stays in the document while closed.</summary>
    /// <value>The root's <c>KeepMounted</c> parameter.</value>
    public bool KeepMounted { get; protected set; }

    /// <summary>Gets a value indicating whether a panel without its own <c>HiddenUntilFound</c> is hidden until found while closed.</summary>
    /// <value>The root's <c>HiddenUntilFound</c> parameter.</value>
    public bool HiddenUntilFound { get; protected set; }

    /// <summary>Adds a listener that runs after every change of the value or a setting.</summary>
    /// <param name="listener">The listener.</param>
    public void AddListener(Action listener) => listeners.Add(listener);

    /// <summary>Removes a listener.</summary>
    /// <param name="listener">The listener.</param>
    public void RemoveListener(Action listener) => listeners.Remove(listener);

    /// <summary>Runs every listener, in the order they were added.</summary>
    protected void Publish()
    {
        for (int i = 0; i < listeners.Count; i++)
        {
            listeners[i]();
        }
    }
}

// ACC §11 `useControlled` row: Controlled<IReadOnlyList<TValue>>("Accordion", "value"), fixed at the first parameter set (ADR 05
// Amendments 2026-10-18); a null value reads as the shared empty list (ACC-API-02). The request is handleValueChange: the next
// value (AccordionValues.Next), OnValueChange with the item's details (EVD-EVT-04), then, unless canceled, ValueChanged and the
// commit of an uncontrolled value, which notifies every Item before the request returns (the CheckboxGroup C3 precedent; design
// point D2 of track 6 relies on it). The latest callbacks are read at request time (ACC-EVT-09). Disabled never gates it
// (ACC-EDGE-08).
/// <summary>The state an accordion root shares with its items: the value, the settings, and the request that changes the value.</summary>
/// <typeparam name="TValue">The type of the items' values.</typeparam>
internal sealed class AccordionRootContext<TValue> : AccordionRootContext
{
    private readonly Controlled<IReadOnlyList<TValue>> valueState;

    /// <summary>Initializes a new instance of the <see cref="AccordionRootContext{TValue}"/> class.</summary>
    /// <param name="logger">The logger of the root's development warnings, or <see langword="null"/>.</param>
    public AccordionRootContext(ILogger? logger)
    {
        valueState = new Controlled<IReadOnlyList<TValue>>("Accordion", "value", logger);
    }

    /// <summary>Gets the value's ownership.</summary>
    /// <value>The controlled value the root reads its <c>Value</c> and <c>DefaultValue</c> parameters into.</value>
    public Controlled<IReadOnlyList<TValue>> ValueState => valueState;

    /// <summary>Gets the values of the open items.</summary>
    /// <value>The current value; the shared empty list while none was given.</value>
    public IReadOnlyList<TValue> Value { get; private set; } = AccordionValues.Empty<TValue>();

    /// <summary>Gets a value indicating whether several items can be open at once.</summary>
    /// <value>The root's <c>Multiple</c> parameter.</value>
    public bool Multiple { get; private set; }

    /// <summary>Gets or sets the consumer's callback that a request runs first.</summary>
    /// <value>The root's <c>OnValueChange</c>; the latest one is always the one invoked.</value>
    public EventCallback<ValueChangeEventArgs<IReadOnlyList<TValue>>> OnValueChange { get; set; }

    /// <summary>Gets or sets the binding callback that an accepted request runs.</summary>
    /// <value>The root's <c>ValueChanged</c>.</value>
    public EventCallback<IReadOnlyList<TValue>> ValueChanged { get; set; }

    /// <summary>Gets or sets the action that renders the root after it committed a value of its own.</summary>
    /// <value>The root's render request, or <see langword="null"/>.</value>
    public Action? Committed { get; set; }

    /// <summary>Applies the parameters of one parameter set and notifies the items when anything they read changed.</summary>
    /// <param name="multiple">The root's <c>Multiple</c>.</param>
    /// <param name="disabled">The root's <c>Disabled</c>.</param>
    /// <param name="orientation">The root's <c>Orientation</c>.</param>
    /// <param name="keepMounted">The root's <c>KeepMounted</c>.</param>
    /// <param name="hiddenUntilFound">The root's <c>HiddenUntilFound</c>.</param>
    public void ApplyParameters(bool multiple, bool disabled, Orientation orientation, bool keepMounted, bool hiddenUntilFound)
    {
        Multiple = multiple;
        IReadOnlyList<TValue> value = valueState.Value ?? AccordionValues.Empty<TValue>();
        if (ReferenceEquals(value, Value) && disabled == Disabled && orientation == Orientation && keepMounted == KeepMounted && hiddenUntilFound == HiddenUntilFound)
        {
            return;
        }

        Value = value;
        Disabled = disabled;
        Orientation = orientation;
        KeepMounted = keepMounted;
        HiddenUntilFound = hiddenUntilFound;
        Publish();
    }

    /// <summary>Runs an item's request: the change callback, then the binding callback and the commit unless it was canceled.</summary>
    /// <param name="identity">The identity of the item that requested the change.</param>
    /// <param name="nextOpen">The open state the item asked for.</param>
    /// <param name="details">The item's details, shared with the item's own callback.</param>
    /// <returns>A task whose result is <see langword="true"/> when the request was not canceled.</returns>
    public async Task<bool> RequestAsync(TValue identity, bool nextOpen, ChangeEventDetails details)
    {
        IReadOnlyList<TValue> next = AccordionValues.Next(Value, identity, nextOpen, Multiple);
        if (!await valueState.NotifyAsync(next, details, OnValueChange))
        {
            return false;
        }

        await valueState.CommitAsync(next, ValueChanged);
        if (!valueState.IsControlled)
        {
            Value = next;
            Publish();
            Committed?.Invoke();
        }

        return true;
    }
}

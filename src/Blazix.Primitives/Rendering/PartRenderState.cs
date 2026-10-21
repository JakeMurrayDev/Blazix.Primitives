using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Rendering;

// ADR 16 rule 8
/// <summary>
/// Defines what the render pipeline needs from the component that owns a render: the event receiver and the element slot.
/// </summary>
internal interface IPartHost : IHandleEvent
{
    /// <summary>Stores the rendered element.</summary>
    /// <param name="element">The rendered element, or <see langword="null"/> when there is none.</param>
    void SetElement(ElementReference? element);

    /// <summary>Determines whether the consumer's handler for an event is rendered on the element this render.</summary>
    /// <param name="eventName">The event's attribute name in lowercase, such as <c>onclick</c>.</param>
    /// <returns><see langword="true"/> to render the consumer's handler; <see langword="false"/> to leave it off.</returns>
    bool ForwardsConsumerHandler(string eventName);

    /// <summary>Adjusts the merged attributes of this render before the element is emitted.</summary>
    /// <param name="attributes">The merged attributes, with <c>class</c>, <c>style</c> and the handlers already in place.</param>
    void FinishAttributes(AttributeList attributes);
}

// ADR 16 rules 2, 3, 5, 7; ADR 02 decision 3
/// <summary>Holds the per-instance state of the render pipeline.</summary>
/// <remarks>
/// The state is kept per instance so that a render allocates as little as possible.
/// It is made of the attribute lists the pipeline fills each render, the scratch builder of the frame rewrite, the one real
/// handler per event name, the class and style memo, and the one element-capture delegate. It is owned by
/// <see cref="PartBase{TState}"/> and <see cref="RenderElement{TState}"/>.
/// </remarks>
internal sealed class PartRenderState : IDisposable
{
    private readonly IPartHost host;
    private RenderTreeBuilder? scratch;

    public ClassMemo ClassMemo;

    public StyleMemo StyleMemo;

    public PartRenderState(IPartHost host)
    {
        this.host = host;
        CaptureAction = element => host.SetElement(element);
    }

    public AttributeList PartAttributes { get; } = new();

    public AttributeList Merged { get; } = new();

    public List<HandlerSlot> Slots { get; } = [];

    public List<KeyValuePair<string, object?>> ComponentParameters { get; } = [];

    public Action<ElementReference> CaptureAction { get; }

    public RenderTreeBuilder Scratch => scratch ??= new RenderTreeBuilder();

    /// <inheritdoc />
    public void Dispose()
    {
        scratch?.Dispose();
        scratch = null;
    }

    public HandlerSlot GetOrAddSlot(string name)
    {
        for (int i = 0; i < Slots.Count; i++)
        {
            if (string.Equals(Slots[i].Name, name, StringComparison.Ordinal))
            {
                return Slots[i];
            }
        }

        HandlerSlot slot = new(name);
        Slots.Add(slot);
        return slot;
    }

    public void ResetSlots()
    {
        for (int i = 0; i < Slots.Count; i++)
        {
            Slots[i].Reset();
        }
    }

    // ADR 02 decision 3
    /// <summary>Creates the real handler for <paramref name="eventName"/>.</summary>
    /// <typeparam name="TArgs">The type of the event arguments.</typeparam>
    /// <param name="eventName">The event's attribute name; it is stored in lowercase.</param>
    /// <param name="handler">The part's own handler, which runs last.</param>
    /// <returns>
    /// One callback, created once, whose dispatch runs the substitute's and the consumer's handlers before
    /// <paramref name="handler"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="eventName"/> or <paramref name="handler"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="eventName"/> is empty.</exception>
    public EventCallback CreateHandler<TArgs>(string eventName, Func<TArgs, Task> handler)
        where TArgs : EventArgs
    {
        ArgumentException.ThrowIfNullOrEmpty(eventName);
        ArgumentNullException.ThrowIfNull(handler);
        HandlerSlot slot = GetOrAddSlot(MergeProps.HandlerName(eventName));
        slot.Typed = new EventCallback(host, new Func<TArgs, Task>(args => HandlerChain.InvokeAsync(args, slot.Substitute, slot.Consumer, handler)));
        return slot.Typed;
    }

    /// <summary>
    /// Returns the chain callback for a slot with no typed part handler but more than one outer handler.
    /// </summary>
    /// <param name="slot">The handler slot.</param>
    /// <returns>The slot's chain callback, created once per slot.</returns>
    public EventCallback OuterChain(HandlerSlot slot)
    {
        if (!slot.Outer.HasDelegate)
        {
            slot.Outer = new EventCallback(host, new Func<EventArgs?, Task>(args => HandlerChain.InvokeAsync(args, slot.Substitute, slot.Consumer, slot.Part)));
        }

        return slot.Outer;
    }
}

/// <summary>
/// Holds the handlers of one event name across the three sources, refreshed each render and read at dispatch.
/// </summary>
internal sealed class HandlerSlot(string name)
{
    public AttributeValue Substitute;

    public AttributeValue Consumer;

    public AttributeValue Part;

    /// <summary>Holds the callback that <see cref="PartRenderState.CreateHandler{TArgs}"/> made for this name, if any.</summary>
    public EventCallback Typed;

    /// <summary>Holds the generic chain callback, made on first need.</summary>
    public EventCallback Outer;

    public string Name { get; } = name;

    public void Reset()
    {
        Substitute = default;
        Consumer = default;
        Part = default;
    }
}

// ADR 16 rule 5
/// <summary>Holds the last inputs and output of the class merge, whose inputs are compared by reference.</summary>
internal struct ClassMemo
{
    private string? substitute;
    private string? consumer;
    private string? function;
    private string? part;
    private string? result;
    private bool primed;

    public string? Merge(string? substituteClass, string? consumerClass, string? functionClass, string? partClass)
    {
        if (primed
            && ReferenceEquals(substitute, substituteClass)
            && ReferenceEquals(consumer, consumerClass)
            && ReferenceEquals(function, functionClass)
            && ReferenceEquals(part, partClass))
        {
            return result;
        }

        substitute = substituteClass;
        consumer = consumerClass;
        function = functionClass;
        part = partClass;
        primed = true;
        result = Join(substituteClass, consumerClass, functionClass, partClass);
        return result;
    }

    // RND-DOM-14/15 and ADR 02 decision 2: substitute class, consumer string, consumer function, part classes.
    private static string? Join(string? a, string? b, string? c, string? d)
    {
        string? joined = null;
        Append(ref joined, a);
        Append(ref joined, b);
        Append(ref joined, c);
        Append(ref joined, d);
        return joined;
    }

    private static void Append(ref string? joined, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        joined = joined is null ? value : string.Concat(joined, " ", value);
    }
}

// ADR 16 rule 5
/// <summary>Holds the last inputs and output of the style merge, whose inputs are compared by reference.</summary>
internal struct StyleMemo
{
    private string? part;
    private string? consumer;
    private string? function;
    private string? substitute;
    private string? result;
    private bool primed;

    public string? Merge(string? partStyle, string? consumerStyle, string? functionStyle, string? substituteStyle)
    {
        if (primed
            && ReferenceEquals(part, partStyle)
            && ReferenceEquals(consumer, consumerStyle)
            && ReferenceEquals(function, functionStyle)
            && ReferenceEquals(substitute, substituteStyle))
        {
            return result;
        }

        part = partStyle;
        consumer = consumerStyle;
        function = functionStyle;
        substitute = substituteStyle;
        primed = true;
        // RND-DOM-16: part style, overridden by the consumer string, by the function, by the substitute; first-seen order kept.
        result = MergeProps.MergeStyles(MergeProps.MergeStyles(MergeProps.MergeStyles(partStyle, consumerStyle), functionStyle), substituteStyle);
        return result;
    }
}

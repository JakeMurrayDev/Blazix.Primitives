using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Blazix.Primitives.Rendering;

// ADR 16 rule 8
/// <summary>What the pipeline needs from the component that owns a render: the event receiver and the element slot.</summary>
internal interface IPartHost : IHandleEvent
{
    void SetElement(ElementReference? element);
}

// ADR 16 rules 2, 3, 5, 7; ADR 02 decision 3
/// <summary>
/// The per-instance state of the pipeline, kept so a render allocates as little as possible: the attribute lists it fills
/// each render, the scratch builder of the frame rewrite, the one real handler per event name, the class and style memo, and the one
/// element-capture delegate. Owned by <see cref="PartBase{TState}"/> and <see cref="RenderElement{TState}"/>.
/// </summary>
internal sealed class PartRenderState : IDisposable
{
    private readonly IPartHost host;
    private RenderTreeBuilder? scratch;

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

    public ClassMemo ClassMemo;

    public StyleMemo StyleMemo;

    public RenderTreeBuilder Scratch => scratch ??= new RenderTreeBuilder();

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
    /// <summary>The real handler for <paramref name="eventName"/>: one callback, created once, whose dispatch runs the substitute's and the consumer's handlers before <paramref name="handler"/>.</summary>
    public EventCallback CreateHandler<TArgs>(string eventName, Func<TArgs, Task> handler)
        where TArgs : EventArgs
    {
        ArgumentException.ThrowIfNullOrEmpty(eventName);
        ArgumentNullException.ThrowIfNull(handler);
        HandlerSlot slot = GetOrAddSlot(MergeProps.HandlerName(eventName));
        slot.Typed = new EventCallback(host, new Func<TArgs, Task>(args => HandlerChain.InvokeAsync(args, slot.Substitute, slot.Consumer, handler)));
        return slot.Typed;
    }

    /// <summary>The chain callback for a slot with no typed part handler but more than one outer handler, created once per slot.</summary>
    public EventCallback OuterChain(HandlerSlot slot)
    {
        if (!slot.Outer.HasDelegate)
        {
            slot.Outer = new EventCallback(host, new Func<EventArgs?, Task>(args => HandlerChain.InvokeAsync(args, slot.Substitute, slot.Consumer, slot.Part)));
        }

        return slot.Outer;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        scratch?.Dispose();
        scratch = null;
    }
}

/// <summary>The handlers of one event name across the three sources, refreshed each render and read at dispatch.</summary>
internal sealed class HandlerSlot(string name)
{
    public string Name { get; } = name;

    public AttributeValue Substitute;

    public AttributeValue Consumer;

    public AttributeValue Part;

    /// <summary>The callback <see cref="PartRenderState.CreateHandler{TArgs}"/> made for this name, if any.</summary>
    public EventCallback Typed;

    /// <summary>The generic chain callback, made on first need.</summary>
    public EventCallback Outer;

    public void Reset()
    {
        Substitute = default;
        Consumer = default;
        Part = default;
    }
}

// ADR 16 rule 5
/// <summary>The last inputs and output of the class merge, compared by reference.</summary>
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
/// <summary>The last inputs and output of the style merge, compared by reference.</summary>
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

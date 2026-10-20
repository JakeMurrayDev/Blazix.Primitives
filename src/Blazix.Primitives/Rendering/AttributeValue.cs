using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;

// BL0006: reading RenderTreeFrame through the public GetFrames() is ADR 02 decision 1 (verified against the API docs);
// the advisory is re-checked at each SDK bump. ASP0006: the sequence number is the caller's literal passed through.
#pragma warning disable BL0006
#pragma warning disable ASP0006

namespace Blazix.Primitives.Rendering;

// ADR 05 §4, ADR 02 Amendments 2026-10-17
/// <summary>
/// One attribute value of the merged attribute list: a string, a boolean, an untyped
/// <see cref="EventCallback"/> or a delegate. A hand-written
/// <c>[Union]</c> struct with the non-boxing access pattern (<see cref="HasValue"/>, <see cref="TryGetValue(out string)"/>
/// and its siblings); <see cref="Value"/> exists because the union contract requires it and is never read by the library.
/// The default instance has no value and stands for an absent attribute.
/// </summary>
[Union]
public readonly struct AttributeValue : IUnion
{
    // The string or delegate case; strings and delegates are reference types, so nothing boxes here.
    private readonly object? reference;
    private readonly EventCallback callback;
    private readonly bool boolean;
    private readonly AttributeValueKind kind;

    /// <summary>Creates the string case.</summary>
    public AttributeValue(string value)
    {
        reference = value;
        kind = AttributeValueKind.String;
    }

    /// <summary>Creates the boolean case.</summary>
    public AttributeValue(bool value)
    {
        boolean = value;
        kind = AttributeValueKind.Boolean;
    }

    /// <summary>Creates the <see cref="EventCallback"/> case; an empty callback is the absent value.</summary>
    public AttributeValue(EventCallback value)
    {
        callback = value;
        kind = value.HasDelegate ? AttributeValueKind.Callback : AttributeValueKind.None;
    }

    /// <summary>Creates the delegate case.</summary>
    public AttributeValue(MulticastDelegate value)
    {
        reference = value;
        kind = AttributeValueKind.Delegate;
    }

    /// <summary>The kind of value held, <see cref="AttributeValueKind.None"/> for the absent value.</summary>
    public AttributeValueKind Kind => kind;

    /// <summary>Whether a value is held; the default instance holds none.</summary>
    public bool HasValue => kind != AttributeValueKind.None;

    /// <summary>Whether the value is a handler: a callback or a delegate.</summary>
    public bool IsHandler => kind is AttributeValueKind.Callback or AttributeValueKind.Delegate;

    // ADR 05 §4
    /// <inheritdoc />
    /// <remarks>Boxes the boolean and callback cases; the library never reads it.</remarks>
    public object? Value => kind switch
    {
        AttributeValueKind.String => reference,
        AttributeValueKind.Boolean => boolean,
        AttributeValueKind.Callback => callback,
        AttributeValueKind.Delegate => reference,
        _ => null,
    };

    public static implicit operator AttributeValue(string value) => new(value);

    public static implicit operator AttributeValue(bool value) => new(value);

    public static implicit operator AttributeValue(EventCallback value) => new(value);

    public static implicit operator AttributeValue(MulticastDelegate value) => new(value);

    // ADR 05 Amendments 2026-10-20
    /// <summary>
    /// Converts a value of the shape Blazor's attribute APIs accept into the union: <c>null</c> is absent, strings and
    /// booleans are themselves, an <see cref="EventCallback"/> or delegate is a handler, a boxed <see cref="EventCallback{TValue}"/>
    /// is unpacked to its untyped form, and anything else is its string representation. This is the conversion the
    /// unmatched-attribute bag and the <see cref="MergeProps"/> dictionary overload go through: those carry case values,
    /// never the union itself, because a union boxed to <see cref="object"/> hides its case.
    /// </summary>
    public static AttributeValue FromObject(object? value)
    {
        switch (value)
        {
            case null:
                return default;
            case string text:
                return new AttributeValue(text);
            case bool flag:
                return new AttributeValue(flag);
            case EventCallback untyped:
                return new AttributeValue(untyped);
            case MulticastDelegate handler:
                return new AttributeValue(handler);
            default:
                return FromFrameValue(UnpackThroughBuilder(value));
        }
    }

    /// <summary>
    /// Converts an attribute value read back from a <see cref="RenderTreeFrame"/>: the builder has already reduced it to
    /// a string, a boxed <c>true</c> (or the empty string on WebAssembly), an untyped callback or a delegate.
    /// </summary>
    public static AttributeValue FromFrameValue(object? value)
    {
        return value switch
        {
            null => default,
            string text => new AttributeValue(text),
            bool flag => new AttributeValue(flag),
            EventCallback untyped => new AttributeValue(untyped),
            MulticastDelegate handler => new AttributeValue(handler),
            _ => new AttributeValue(value.ToString() ?? string.Empty),
        };
    }

    public bool TryGetValue(out string value)
    {
        if (kind == AttributeValueKind.String)
        {
            value = (string)reference!;
            return true;
        }

        value = string.Empty;
        return false;
    }

    public bool TryGetValue(out bool value)
    {
        value = boolean;
        return kind == AttributeValueKind.Boolean;
    }

    public bool TryGetValue(out EventCallback value)
    {
        value = callback;
        return kind == AttributeValueKind.Callback;
    }

    public bool TryGetValue(out MulticastDelegate value)
    {
        if (kind == AttributeValueKind.Delegate)
        {
            value = (MulticastDelegate)reference!;
            return true;
        }

        value = null!;
        return false;
    }

    /// <summary>The case value boxed, for the places Blazor's own contracts force an <c>object</c> slot (component parameters, the attribute dictionary of a substitute callback).</summary>
    public object? ToObject() => Value;

    /// <summary>
    /// Invokes a handler value the way Blazor's renderer would: a callback through its receiver, a delegate wrapped
    /// with its target as the receiver (the renderer's own rule for delegate attribute values).
    /// </summary>
    public Task InvokeAsync(object? arg)
    {
        return kind switch
        {
            AttributeValueKind.Callback => callback.InvokeAsync(arg),
            AttributeValueKind.Delegate => InvokeDelegateAsync((MulticastDelegate)reference!, arg),
            _ => throw new InvalidOperationException("The attribute value is not a handler."),
        };
    }

    /// <summary>Emits the value as an attribute of the element or component most recently opened on <paramref name="builder"/> through the typed overload of its case.</summary>
    public void EmitTo(RenderTreeBuilder builder, int sequence, string name)
    {
        switch (kind)
        {
            case AttributeValueKind.String:
                builder.AddAttribute(sequence, name, (string)reference!);
                break;
            case AttributeValueKind.Boolean:
                builder.AddAttribute(sequence, name, boolean);
                break;
            case AttributeValueKind.Callback:
                builder.AddAttribute(sequence, name, callback);
                break;
            case AttributeValueKind.Delegate:
                builder.AddAttribute(sequence, name, (MulticastDelegate)reference!);
                break;
        }
    }

    /// <inheritdoc />
    public override string ToString() => kind switch
    {
        AttributeValueKind.String => (string)reference!,
        AttributeValueKind.Boolean => boolean ? "true" : "false",
        AttributeValueKind.Callback => "EventCallback",
        AttributeValueKind.Delegate => reference!.GetType().Name,
        _ => string.Empty,
    };

    // A boxed EventCallback<T> can only be invoked when T is known, and the interface that unpacks it is internal to
    // Blazor. RenderTreeBuilder.AddAttribute(int, string, object) performs that unpacking for element attributes, so
    // the value is pushed through a throwaway element frame and read back: exactly what Blazor does for an
    // @attributes splat, through public API and without reflection.
    private static object? UnpackThroughBuilder(object value)
    {
        RenderTreeBuilder builder = s_unpackBuilder ??= new RenderTreeBuilder();
        builder.OpenElement(0, "x");
        builder.AddAttribute(1, "x", value);
        builder.CloseElement();
        ArrayRange<RenderTreeFrame> frames = builder.GetFrames();
        object? unpacked = frames.Count > 1 ? frames.Array[1].AttributeValue : null;
        builder.Clear();
        return unpacked;
    }

    private static Task InvokeDelegateAsync(MulticastDelegate handler, object? arg)
    {
        return new EventCallback(handler.Target as IHandleEvent, handler).InvokeAsync(arg);
    }

    [ThreadStatic]
    private static RenderTreeBuilder? s_unpackBuilder;
}

/// <summary>The cases of <see cref="AttributeValue"/>.</summary>
public enum AttributeValueKind : byte
{
    None,
    String,
    Boolean,
    Callback,
    Delegate,
}

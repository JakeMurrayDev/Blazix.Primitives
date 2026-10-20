using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.RenderTree;

// BL0006: reading RenderTreeFrame through the public GetFrames() is ADR 02 decision 1 (verified against the API docs);
// the advisory is re-checked at each SDK bump.
#pragma warning disable BL0006

namespace Blazix.Primitives.Rendering;

// ADR 05 §4, ADR 02 Amendments 2026-10-17
/// <summary>
/// Represents one attribute value of a merged attribute list: a string, a Boolean, an untyped
/// <see cref="EventCallback"/> or a delegate.
/// </summary>
/// <remarks>
/// <para>
/// This is a hand-written union, marked <see cref="UnionAttribute"/>, with the non-boxing access pattern:
/// <see cref="HasValue"/>, <see cref="TryGetValue(out string)"/> and its siblings. <see cref="Value"/> exists because
/// <see cref="IUnion"/> requires it.
/// </para>
/// <para>The default instance has no value and stands for an absent attribute.</para>
/// </remarks>
[Union]
public readonly struct AttributeValue : IUnion
{
    // The string or delegate case; strings and delegates are reference types, so nothing boxes here.
    private readonly object? reference;
    private readonly EventCallback callback;
    private readonly bool boolean;
    private readonly AttributeValueKind kind;

    [ThreadStatic]
    private static RenderTreeBuilder? s_unpackBuilder;

    /// <summary>Initializes a new instance of the <see cref="AttributeValue"/> struct that holds a string.</summary>
    /// <param name="value">The string value.</param>
    public AttributeValue(string value)
    {
        reference = value;
        kind = AttributeValueKind.String;
    }

    /// <summary>Initializes a new instance of the <see cref="AttributeValue"/> struct that holds a Boolean.</summary>
    /// <param name="value">The Boolean value.</param>
    public AttributeValue(bool value)
    {
        boolean = value;
        kind = AttributeValueKind.Boolean;
    }

    /// <summary>Initializes a new instance of the <see cref="AttributeValue"/> struct that holds an <see cref="EventCallback"/>.</summary>
    /// <param name="value">The callback. A callback with no delegate produces the absent value.</param>
    public AttributeValue(EventCallback value)
    {
        callback = value;
        kind = value.HasDelegate ? AttributeValueKind.Callback : AttributeValueKind.None;
    }

    /// <summary>Initializes a new instance of the <see cref="AttributeValue"/> struct that holds a delegate.</summary>
    /// <param name="value">The delegate.</param>
    public AttributeValue(MulticastDelegate value)
    {
        reference = value;
        kind = AttributeValueKind.Delegate;
    }

    /// <summary>Gets the kind of value held.</summary>
    /// <value>The case of the value, or <see cref="AttributeValueKind.None"/> for the absent value.</value>
    public AttributeValueKind Kind => kind;

    /// <summary>Gets a value indicating whether a value is held.</summary>
    /// <value>
    /// <see langword="true"/> if a value is held; otherwise, <see langword="false"/>. The default instance holds no value.
    /// </value>
    public bool HasValue => kind != AttributeValueKind.None;

    /// <summary>Gets a value indicating whether the value is a handler.</summary>
    /// <value><see langword="true"/> if the value is a callback or a delegate; otherwise, <see langword="false"/>.</value>
    public bool IsHandler => kind is AttributeValueKind.Callback or AttributeValueKind.Delegate;

    // ADR 05 §4
    /// <inheritdoc />
    /// <remarks>Reading it boxes the Boolean and callback cases.</remarks>
    public object? Value => kind switch
    {
        AttributeValueKind.String => reference,
        AttributeValueKind.Boolean => boolean,
        AttributeValueKind.Callback => callback,
        AttributeValueKind.Delegate => reference,
        _ => null,
    };

    /// <summary>Converts a string to an <see cref="AttributeValue"/> that holds it.</summary>
    /// <param name="value">The string value.</param>
    /// <returns>An <see cref="AttributeValue"/> of kind <see cref="AttributeValueKind.String"/>.</returns>
    public static implicit operator AttributeValue(string value) => new(value);

    /// <summary>Converts a Boolean to an <see cref="AttributeValue"/> that holds it.</summary>
    /// <param name="value">The Boolean value.</param>
    /// <returns>An <see cref="AttributeValue"/> of kind <see cref="AttributeValueKind.Boolean"/>.</returns>
    public static implicit operator AttributeValue(bool value) => new(value);

    /// <summary>Converts an <see cref="EventCallback"/> to an <see cref="AttributeValue"/> that holds it.</summary>
    /// <param name="value">The callback.</param>
    /// <returns>
    /// An <see cref="AttributeValue"/> of kind <see cref="AttributeValueKind.Callback"/>, or the absent value when
    /// <paramref name="value"/> has no delegate.
    /// </returns>
    public static implicit operator AttributeValue(EventCallback value) => new(value);

    /// <summary>Converts a delegate to an <see cref="AttributeValue"/> that holds it.</summary>
    /// <param name="value">The delegate.</param>
    /// <returns>An <see cref="AttributeValue"/> of kind <see cref="AttributeValueKind.Delegate"/>.</returns>
    public static implicit operator AttributeValue(MulticastDelegate value) => new(value);

    // ADR 05 Amendments 2026-10-20
    /// <summary>Converts a value of the shape Blazor's attribute APIs accept to an <see cref="AttributeValue"/>.</summary>
    /// <param name="value">The value to convert.</param>
    /// <returns>
    /// The absent value for <see langword="null"/>; the string or Boolean itself for a string or Boolean; a handler value for
    /// an <see cref="EventCallback"/> or a delegate; the untyped form of a boxed <see cref="EventCallback{TValue}"/>; and the
    /// string representation of anything else.
    /// </returns>
    /// <remarks>
    /// The unmatched bag and <see cref="MergeProps.MergeDictionaries"/> go through this conversion. Those carry
    /// case values, never the union itself, because a union boxed to <see cref="object"/> hides its case.
    /// </remarks>
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

    /// <summary>Converts an attribute value read back from a <see cref="RenderTreeFrame"/> to an <see cref="AttributeValue"/>.</summary>
    /// <param name="value">
    /// The frame's attribute value. The builder has already reduced it to a string, a boxed <see langword="true"/> (or the
    /// empty string on WebAssembly), an untyped callback or a delegate.
    /// </param>
    /// <returns>
    /// The absent value for <see langword="null"/>; the matching case for a string, Boolean, <see cref="EventCallback"/> or
    /// delegate; and the string representation of anything else.
    /// </returns>
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

    /// <summary>Gets the string value, if this instance holds one.</summary>
    /// <param name="value">
    /// When this method returns, the string value if the kind is <see cref="AttributeValueKind.String"/>; otherwise,
    /// <see cref="string.Empty"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the kind is <see cref="AttributeValueKind.String"/>; otherwise, <see langword="false"/>.
    /// </returns>
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

    /// <summary>Gets the Boolean value, if this instance holds one.</summary>
    /// <param name="value">
    /// When this method returns, the Boolean value if the kind is <see cref="AttributeValueKind.Boolean"/>; otherwise,
    /// <see langword="false"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the kind is <see cref="AttributeValueKind.Boolean"/>; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGetValue(out bool value)
    {
        value = boolean;
        return kind == AttributeValueKind.Boolean;
    }

    /// <summary>Gets the callback, if this instance holds one.</summary>
    /// <param name="value">
    /// When this method returns, the callback if the kind is <see cref="AttributeValueKind.Callback"/>; otherwise, an
    /// <see cref="EventCallback"/> with no delegate.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the kind is <see cref="AttributeValueKind.Callback"/>; otherwise, <see langword="false"/>.
    /// </returns>
    public bool TryGetValue(out EventCallback value)
    {
        value = callback;
        return kind == AttributeValueKind.Callback;
    }

    /// <summary>Gets the delegate, if this instance holds one.</summary>
    /// <param name="value">
    /// When this method returns, the delegate if the kind is <see cref="AttributeValueKind.Delegate"/>; otherwise,
    /// <see langword="null"/>.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the kind is <see cref="AttributeValueKind.Delegate"/>; otherwise, <see langword="false"/>.
    /// </returns>
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

    /// <summary>Returns the case value boxed to <see cref="object"/>.</summary>
    /// <returns>The string, Boolean, callback or delegate held, or <see langword="null"/> for the absent value.</returns>
    /// <remarks>
    /// Use this in the places where Blazor's own contracts force an <see cref="object"/> slot: component parameters and the
    /// attribute dictionary of a substitute callback.
    /// </remarks>
    public object? ToObject() => Value;

    /// <summary>Invokes a handler value the way Blazor's renderer would.</summary>
    /// <param name="arg">The event argument passed to the handler.</param>
    /// <returns>A task that completes when the handler has finished.</returns>
    /// <exception cref="InvalidOperationException">The value is not a handler.</exception>
    /// <remarks>
    /// A callback is invoked through its receiver. A delegate is wrapped in an <see cref="EventCallback"/> with its target as
    /// the receiver when the target implements <see cref="IHandleEvent"/>, which is the renderer's own rule for delegate
    /// attribute values.
    /// </remarks>
    public Task InvokeAsync(object? arg)
    {
        return kind switch
        {
            AttributeValueKind.Callback => callback.InvokeAsync(arg),
            AttributeValueKind.Delegate => InvokeDelegateAsync((MulticastDelegate)reference!, arg),
            _ => throw new InvalidOperationException("Blazix: the attribute value is not a handler, so it cannot be invoked."),
        };
    }

    // BLZX2002: the sequence number is the caller's constant, passed through (ADR 16 rule 3).
#pragma warning disable BLZX2002
    /// <summary>
    /// Adds the value as an attribute of the element or component most recently opened on <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The render tree builder to add the attribute to.</param>
    /// <param name="sequence">The sequence number of the attribute frame.</param>
    /// <param name="name">The attribute name.</param>
    /// <remarks>
    /// The value is added through the typed <c>AddAttribute</c> overload that matches its case. The absent value adds
    /// nothing.
    /// </remarks>
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
#pragma warning restore BLZX2002

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
}

/// <summary>Specifies the case held by an <see cref="AttributeValue"/>.</summary>
public enum AttributeValueKind : byte
{
    /// <summary>No value is held, so the attribute is absent.</summary>
    None,
    /// <summary>The value is a <see cref="string"/>.</summary>
    String,
    /// <summary>The value is a <see cref="bool"/>.</summary>
    Boolean,
    /// <summary>The value is an untyped <see cref="EventCallback"/> that has a delegate.</summary>
    Callback,
    /// <summary>The value is a <see cref="MulticastDelegate"/> handler.</summary>
    Delegate,
}

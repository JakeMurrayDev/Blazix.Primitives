using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives;

// ADR 04 Amendments 2026-10-27, decision 5 (chosen by the user 2026-10-27); ADR 05 §4 (Duration is the model); ADR 08
// Amendments 2026-10-27. Base UI's `container?: HTMLElement | ShadowRoot | RefObject | null` (FLT-API-13, PSH §6.1):
// an element; a selector (ADR 04 §5), resolved in the portal wrapper's owner document; a node a consumer's script holds,
// which covers a closed shadow root; and Pending, Base UI's null. A part's Container parameter is nullable, and null
// means "not supplied": the enclosing portal node, else <body>.
/// <summary>
/// Represents where a portal moves its content: an element, a CSS selector, a node a script holds, or
/// <see cref="Pending"/>, which renders nothing until a container is supplied.
/// </summary>
/// <remarks>
/// <para>
/// This is a hand-written union, marked <see cref="UnionAttribute"/>, with the non-boxing access pattern:
/// <see cref="HasValue"/> and one <c>TryGetValue</c> overload per case. <see cref="Value"/> exists because
/// <see cref="IUnion"/> requires it.
/// </para>
/// <para>
/// In Razor, write <c>Container="@element"</c> for an <see cref="ElementReference"/>, <c>Container="#target"</c> for a
/// selector (matched in the document that holds the portal, first match), <c>Container="@node"</c> for an
/// <see cref="IJSObjectReference"/> your script returns, such as a shadow root, or
/// <c>Container="@PortalContainerValue.Pending"</c>; each converts implicitly. A part's <c>Container</c> parameter is
/// nullable, and
/// <see langword="null"/> means the enclosing portal, else the document's body. The default instance has no value.
/// </para>
/// </remarks>
[Union]
public readonly struct PortalContainerValue : IUnion
{
    private readonly ElementReference element;
    private readonly string? selector;
    private readonly IJSObjectReference? node;
    private readonly ContainerKind kind;

    /// <summary>Initializes a new instance of the <see cref="PortalContainerValue"/> struct that holds an element.</summary>
    /// <param name="value">The element to move the portal's content into.</param>
    public PortalContainerValue(ElementReference value)
    {
        element = value;
        kind = ContainerKind.Element;
    }

    /// <summary>Initializes a new instance of the <see cref="PortalContainerValue"/> struct that holds a CSS selector.</summary>
    /// <param name="value">The selector of the element to move the portal's content into.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public PortalContainerValue(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        selector = value;
        kind = ContainerKind.Selector;
    }

    /// <summary>Initializes a new instance of the <see cref="PortalContainerValue"/> struct that holds a node a script holds.</summary>
    /// <param name="value">The reference to the node, such as a shadow root, that your JavaScript returned.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public PortalContainerValue(IJSObjectReference value)
    {
        ArgumentNullException.ThrowIfNull(value);
        node = value;
        kind = ContainerKind.Node;
    }

    /// <summary>Initializes a new instance of the <see cref="PortalContainerValue"/> struct that holds the pending case.</summary>
    /// <param name="value">The pending case.</param>
    public PortalContainerValue(PortalContainerPending value)
    {
        _ = value;
        kind = ContainerKind.Pending;
    }

    /// <summary>Gets the value with which the portal renders nothing until a container is supplied, Base UI's <c>null</c>.</summary>
    /// <value>A <see cref="PortalContainerValue"/> that holds <see cref="PortalContainerPending"/>.</value>
    public static PortalContainerValue Pending => new(default(PortalContainerPending));

    /// <summary>Gets a value indicating whether a value is held.</summary>
    /// <value><see langword="true"/> if a value is held; otherwise, <see langword="false"/>. The default instance holds no value.</value>
    public bool HasValue => kind != ContainerKind.None;

    // BLZX2005: IUnion requires an object-typed Value, so reading it boxes the struct cases; the library reads the cases
    // through TryGetValue and IsPending instead.
#pragma warning disable BLZX2005
    /// <inheritdoc />
    /// <remarks>Reading it boxes an element or the pending case.</remarks>
    public object? Value => kind switch
    {
        ContainerKind.Element => element,
        ContainerKind.Selector => selector,
        ContainerKind.Node => node,
        ContainerKind.Pending => default(PortalContainerPending),
        _ => null,
    };
#pragma warning restore BLZX2005

    /// <summary>Gets a value indicating whether this instance holds the pending case.</summary>
    /// <value><see langword="true"/> if this instance is <see cref="Pending"/>; otherwise, <see langword="false"/>.</value>
    public bool IsPending => kind == ContainerKind.Pending;

    /// <summary>Converts an element to a <see cref="PortalContainerValue"/> that holds it.</summary>
    /// <param name="value">The element.</param>
    /// <returns>A <see cref="PortalContainerValue"/> that holds <paramref name="value"/>.</returns>
    public static implicit operator PortalContainerValue(ElementReference value) => new(value);

    /// <summary>Converts a CSS selector to a <see cref="PortalContainerValue"/> that holds it.</summary>
    /// <param name="value">The selector.</param>
    /// <returns>A <see cref="PortalContainerValue"/> that holds <paramref name="value"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
    public static implicit operator PortalContainerValue(string value) => new(value);

    /// <summary>Converts the pending case to a <see cref="PortalContainerValue"/> that holds it.</summary>
    /// <param name="value">The pending case.</param>
    /// <returns><see cref="Pending"/>.</returns>
    public static implicit operator PortalContainerValue(PortalContainerPending value) => new(value);

    /// <summary>Gets the element, if this instance holds one.</summary>
    /// <param name="value">When this method returns, the element if one is held; otherwise, the default reference.</param>
    /// <returns><see langword="true"/> if an element is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(out ElementReference value)
    {
        value = element;
        return kind == ContainerKind.Element;
    }

    /// <summary>Gets the CSS selector, if this instance holds one.</summary>
    /// <param name="value">When this method returns, the selector if one is held; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a selector is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value)
    {
        value = selector;
        return kind == ContainerKind.Selector;
    }

    /// <summary>Gets the node a script holds, if this instance holds one.</summary>
    /// <param name="value">When this method returns, the node reference if one is held; otherwise, <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if a node reference is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out IJSObjectReference? value)
    {
        value = node;
        return kind == ContainerKind.Node;
    }

    /// <summary>Gets the pending case, if this instance holds it.</summary>
    /// <param name="value">When this method returns, the pending case.</param>
    /// <returns><see langword="true"/> if the pending case is held; otherwise, <see langword="false"/>.</returns>
    public bool TryGetValue(out PortalContainerPending value)
    {
        value = default;
        return kind == ContainerKind.Pending;
    }

    private enum ContainerKind : byte
    {
        None,
        Element,
        Selector,
        Node,
        Pending,
    }
}

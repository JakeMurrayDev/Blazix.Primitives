using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Rendering;

// RND-STR-04, ADR 02 decision 1; RND §2.1 rows 1–4
/// <summary>
/// Represents what a substitute callback receives: the merged attributes, the state object, and the content to render
/// inside the element.
/// </summary>
/// <typeparam name="TState">The type of the part's state record.</typeparam>
/// <param name="Attributes">
/// The merged attribute set (state attributes, part attributes, consumer attributes, class and style) as the dictionary
/// Blazor's <c>@attributes</c> accepts.
/// </param>
/// <param name="State">
/// The state object. It is never <see langword="null"/> for a part; it is <see langword="null"/> when a
/// <see cref="RenderElement{TState}"/> has no <see cref="RenderElement{TState}.State"/>.
/// </param>
/// <param name="ChildContent">The content to render inside the element, or <see langword="null"/>.</param>
/// <remarks>
/// Nothing is merged after the callback returns; the callback is expected to splat <see cref="Attributes"/> (including
/// the <c>id</c>) onto the element it renders.
/// </remarks>
public sealed record RenderProps<TState>(IReadOnlyDictionary<string, object> Attributes, TState? State, RenderFragment? ChildContent);

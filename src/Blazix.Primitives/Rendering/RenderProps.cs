using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Rendering;

/// <summary>
/// What a substitute callback receives (RND-STR-04, ADR 02 decision 1): the merged attribute set of RND §2.1 rows 1–4
/// as the dictionary Blazor's <c>@attributes</c> accepts, the state object, and the content to render inside the
/// element. Nothing is merged after the callback returns; the callback is expected to splat <see cref="Attributes"/>
/// (including the <c>id</c>) onto the element it renders.
/// </summary>
public sealed record RenderProps<TState>(IReadOnlyDictionary<string, object> Attributes, TState State, RenderFragment? ChildContent);

namespace Blazix.Primitives.Rendering;

// RND-DOM-06
/// <summary>One attribute a custom mapping produces.</summary>
public readonly record struct StateDataAttribute(string Name, string Value);

// RND-DOM-06..08
/// <summary>
/// A custom projection for one state field: receives only the field's value and returns the attributes
/// to emit verbatim, or <c>null</c> for none. Return a shared static array so a render allocates nothing.
/// </summary>
public delegate StateDataAttribute[]? StateFieldProjection<in TValue>(TValue value);

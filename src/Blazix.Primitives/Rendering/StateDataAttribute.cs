namespace Blazix.Primitives.Rendering;

// RND-DOM-06
/// <summary>Represents one attribute that a custom mapping produces.</summary>
/// <param name="Name">The attribute name.</param>
/// <param name="Value">The attribute value.</param>
public readonly record struct StateDataAttribute(string Name, string Value);

// RND-DOM-06..08
/// <summary>Represents a custom projection for one state field.</summary>
/// <typeparam name="TValue">The type of the field.</typeparam>
/// <param name="value">The field's value; the projection receives nothing else.</param>
/// <returns>The attributes to emit verbatim, or <see langword="null"/> for none.</returns>
/// <remarks>Return a shared static array so a render allocates nothing.</remarks>
public delegate StateDataAttribute[]? StateFieldProjection<in TValue>(TValue value);

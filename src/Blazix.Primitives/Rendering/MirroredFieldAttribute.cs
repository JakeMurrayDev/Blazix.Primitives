namespace Blazix.Primitives.Rendering;

// ADR 04 §2
/// <summary>
/// Marks a state property whose DOM attribute JavaScript writes, so that the generated projection skips it.
/// </summary>
/// <remarks>
/// Such a property is a mirrored field: its C# value arrives later from JavaScript. The value stays in the state object
/// for <see cref="PartBase{TState}.ClassValue"/>, <see cref="PartBase{TState}.StyleValue"/> and the substitute callback.
/// A property that has no attribute at all is marked <see cref="NotProjectedAttribute"/> instead.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class MirroredFieldAttribute : Attribute
{
}

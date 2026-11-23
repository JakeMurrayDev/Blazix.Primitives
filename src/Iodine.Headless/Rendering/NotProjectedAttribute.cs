namespace Blazix.Primitives.Rendering;

// ADR 02 Amendments 2026-10-26 (a never-projected state field; the first use is Accordion's value array, ACC-STA-01..03).
/// <summary>
/// Marks a state property that the generated projection never writes as an attribute, whatever its type.
/// </summary>
/// <remarks>
/// The value stays in the state object for <see cref="PartBase{TState}.ClassValue"/>,
/// <see cref="PartBase{TState}.StyleValue"/> and the substitute callback. Unlike <see cref="MirroredFieldAttribute"/>, it
/// says nothing about who writes a DOM attribute: no attribute exists for the property.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class NotProjectedAttribute : Attribute
{
}

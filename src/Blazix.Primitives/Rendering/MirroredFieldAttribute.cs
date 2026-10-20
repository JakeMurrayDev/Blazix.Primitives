namespace Blazix.Primitives.Rendering;

// ADR 04 §2
/// <summary>
/// Marks a state property whose DOM attribute JavaScript writes (a mirrored field, whose C# value arrives later from JavaScript): the value stays in the
/// state object for <c>ClassValue</c>, <c>StyleValue</c> and the substitute callback, but the generated projection
/// skips it. Also the opt-out for a property no projection should emit.
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class MirroredFieldAttribute : Attribute
{
}

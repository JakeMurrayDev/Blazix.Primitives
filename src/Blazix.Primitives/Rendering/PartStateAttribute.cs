namespace Blazix.Primitives.Rendering;

// RND-ALG-07; ADR 08 §3
/// <summary>Marks a part's state record so that the generator implements <see cref="IPartState{TSelf}"/> on it.</summary>
/// <remarks>
/// The record is named <c>&lt;Part&gt;State</c> and declared as a <c>sealed partial record</c>. The generated
/// implementation is the typed projection of the record's own public primitive properties; a property marked
/// <see cref="MirroredFieldAttribute"/> is excluded.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class PartStateAttribute : Attribute
{
}

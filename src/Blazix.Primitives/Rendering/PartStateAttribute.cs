namespace Blazix.Primitives.Rendering;

// RND-ALG-07; ADR 08 §3
/// <summary>
/// Marks a part's state record, named <c>&lt;Part&gt;State</c> and declared as a <c>sealed partial record</c>. The generator
/// implements <see cref="IPartState{TSelf}"/> on it with the typed projection of its own public primitive properties;
/// a property marked <see cref="MirroredFieldAttribute"/> is excluded.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class PartStateAttribute : Attribute
{
}

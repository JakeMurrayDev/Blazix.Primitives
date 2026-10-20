namespace Blazix.Primitives.Rendering;

/// <summary>
/// Marks a part's state record (ADR 08 §3: <c>&lt;Part&gt;State</c>, a <c>sealed partial record</c>). The generator
/// implements <see cref="IPartState{TSelf}"/> on it with the typed projection of its own public primitive properties
/// (RND-ALG-07); a property marked <see cref="MirroredFieldAttribute"/> is excluded.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class PartStateAttribute : Attribute
{
}

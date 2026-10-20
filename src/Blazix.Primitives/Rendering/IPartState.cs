namespace Blazix.Primitives.Rendering;

// RND-ALG-07, ADR 02 as amended by ADR 05
/// <summary>
/// Implemented by the generator for every <c>sealed partial record</c> marked <see cref="PartStateAttribute"/>: the typed
/// projection of the record's own public primitive properties to <c>data-*</c> attributes in declaration order.
/// <typeparamref name="TSelf"/> is the record itself.
/// </summary>
public interface IPartState<TSelf>
    where TSelf : IPartState<TSelf>
{
    // RND-DOM-06..09; RND-DOM-01..05, ADR 08 §3
    /// <summary>
    /// Writes the state data attributes. For each property in declaration order: a custom mapping for the property
    /// replaces the default projection; otherwise <c>true</c> emits <c>data-&lt;name&gt;=""</c>,
    /// <c>false</c>, <c>null</c>, zero and the empty string emit nothing, any other value emits its string form, and
    /// an enum emits the kebab-case form of its member name.
    /// </summary>
    void ProjectStateAttributes(StateAttributesMapping<TSelf>? mapping, StateAttributeWriter writer);
}

namespace Blazix.Primitives.Rendering;

/// <summary>
/// Implemented by the generator for every <c>sealed partial record</c> marked <see cref="PartStateAttribute"/>: the typed
/// projection of the record's own public primitive properties to <c>data-*</c> attributes in declaration order
/// (RND-ALG-07, ADR 02 as amended by ADR 05). <typeparamref name="TSelf"/> is the record itself.
/// </summary>
public interface IPartState<TSelf>
    where TSelf : IPartState<TSelf>
{
    /// <summary>
    /// Writes the state data attributes. For each property in declaration order: a custom mapping for the property
    /// replaces the default projection (RND-DOM-06..09); otherwise <c>true</c> emits <c>data-&lt;name&gt;=""</c>,
    /// <c>false</c>, <c>null</c>, zero and the empty string emit nothing, any other value emits its string form, and
    /// an enum emits the kebab-case form of its member name (RND-DOM-01..05, ADR 08 §3).
    /// </summary>
    void ProjectStateAttributes(StateAttributesMapping<TSelf>? mapping, StateAttributeWriter writer);
}

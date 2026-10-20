namespace Blazix.Primitives.Rendering;

// RND-ALG-07, ADR 02 as amended by ADR 05
/// <summary>
/// Defines the typed projection of a part state record's own public primitive properties to <c>data-*</c> attributes.
/// </summary>
/// <typeparam name="TSelf">The type of the state record that implements the interface.</typeparam>
/// <remarks>
/// The generator implements this interface for every <c>sealed partial record</c> marked with
/// <see cref="PartStateAttribute"/>. The projection follows the declaration order of the properties.
/// </remarks>
public interface IPartState<TSelf>
    where TSelf : IPartState<TSelf>
{
    // RND-DOM-06..09; RND-DOM-01..05, ADR 08 §3
    /// <summary>Writes the state's <c>data-*</c> attributes to <paramref name="writer"/>.</summary>
    /// <param name="mapping">The custom mapping, or <see langword="null"/> for the default projection only.</param>
    /// <param name="writer">The sink the attributes are written to.</param>
    /// <remarks>
    /// For each property in declaration order, a custom mapping for the property replaces the default projection.
    /// Otherwise, <see langword="true"/> emits <c>data-&lt;name&gt;=""</c>; <see langword="false"/>,
    /// <see langword="null"/>, zero and the empty string emit nothing; an enum emits the kebab-case form of its member
    /// name; and any other value emits its string form.
    /// </remarks>
    void ProjectStateAttributes(StateAttributesMapping<TSelf>? mapping, StateAttributeWriter writer);
}

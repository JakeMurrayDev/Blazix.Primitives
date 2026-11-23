namespace Blazix.Primitives.Rendering;

// RND-DOM-09
/// <summary>Represents the sink that generated projections and custom mappings write state attributes to.</summary>
/// <remarks>The sink is a name-keyed set: a later write to the same name wins.</remarks>
public readonly struct StateAttributeWriter
{
    private readonly AttributeList target;

    /// <summary>Initializes a new instance of the <see cref="StateAttributeWriter"/> struct.</summary>
    /// <param name="target">The attribute list the writes go to.</param>
    /// <exception cref="ArgumentNullException"><paramref name="target"/> is <see langword="null"/>.</exception>
    public StateAttributeWriter(AttributeList target)
    {
        ArgumentNullException.ThrowIfNull(target);
        this.target = target;
    }

    /// <summary>
    /// Writes <paramref name="name"/> with <paramref name="value"/>, replacing an earlier write of the same name.
    /// </summary>
    /// <param name="name">The attribute name.</param>
    /// <param name="value">The attribute value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public void Set(string name, string value) => target.Set(name, value);
}

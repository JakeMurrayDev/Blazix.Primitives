namespace Blazix.Primitives.Rendering;

/// <summary>The sink generated projections and custom mappings write to: a name-keyed set where a later write to the same name wins (RND-DOM-09).</summary>
public readonly struct StateAttributeWriter
{
    private readonly AttributeList target;

    public StateAttributeWriter(AttributeList target)
    {
        ArgumentNullException.ThrowIfNull(target);
        this.target = target;
    }

    /// <summary>Writes <paramref name="name"/> with <paramref name="value"/>, replacing an earlier write of the same name.</summary>
    public void Set(string name, string value) => target.Set(name, value);
}

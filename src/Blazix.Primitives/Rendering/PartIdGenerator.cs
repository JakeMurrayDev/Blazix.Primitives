namespace Blazix.Primitives.Rendering;

/// <summary>
/// The scoped generated-id source (ADR 02 decision 5): every part that may need an id draws <c>base-ui-&lt;n&gt;</c> at
/// its first render, in render order, whether or not the consumer supplied one (RND-DOM-27..31). Registered by
/// <c>AddBlazixPrimitives()</c> as a scoped service so prerender and the interactive pass count from the same origin.
/// </summary>
public sealed class PartIdGenerator
{
    /// <summary>The prefix of every generated id; a DOM-level literal that keeps Base UI's form (ADR 08 §4).</summary>
    public const string Prefix = "base-ui-";

    /// <summary>Draws the next id.</summary>
    public string Next()
    {
        throw new NotImplementedException();
    }
}

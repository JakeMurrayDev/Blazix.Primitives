using System.Globalization;

namespace Blazix.Primitives.Rendering;

// ADR 02 decision 5; RND-DOM-27..31
/// <summary>
/// The scoped generated-id source: every part that may need an id draws <c>base-ui-&lt;n&gt;</c> at
/// its first render, in render order, whether or not the consumer supplied one. Registered by
/// <c>AddBlazixPrimitives()</c> as a scoped service so prerender and the interactive pass count from the same origin.
/// </summary>
public sealed class PartIdGenerator
{
    // ADR 08 §4
    /// <summary>The prefix of every generated id; a DOM-level literal that keeps Base UI's form.</summary>
    public const string Prefix = "base-ui-";

    private int last;

    /// <summary>Draws the next id.</summary>
    public string Next() => Prefix + (++last).ToString(CultureInfo.InvariantCulture);
}

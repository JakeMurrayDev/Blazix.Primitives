using System.Globalization;

namespace Blazix.Primitives.Rendering;

// ADR 02 decision 5; RND-DOM-27..31
/// <summary>Provides the generated ids of parts, in the form <c>base-ui-&lt;n&gt;</c>.</summary>
/// <remarks>
/// Every part that may need an id draws one at its first render, in render order, whether or not the consumer supplied
/// one.
/// <see cref="Microsoft.Extensions.DependencyInjection.BlazixPrimitivesServiceCollectionExtensions.AddBlazixPrimitives"/>
/// registers this class as a scoped service so prerender and the interactive pass count from the same origin.
/// </remarks>
public sealed class PartIdGenerator
{
    // ADR 08 §4
    /// <summary>Represents the prefix of every generated id.</summary>
    /// <remarks>The prefix is a DOM-level literal that keeps Base UI's form.</remarks>
    public const string Prefix = "base-ui-";

    private int last;

    /// <summary>Draws the next id.</summary>
    /// <returns><see cref="Prefix"/> followed by the next number of this generator, starting at 1.</returns>
    public string Next() => Prefix + (++last).ToString(CultureInfo.InvariantCulture);
}

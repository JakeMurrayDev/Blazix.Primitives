using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// MeterTrack.test.tsx has only the conformance suite (MeterTrackConformanceTests); these are the port's own tests for the
// two rows that cite no Base UI test (MTR-STR-03, MTR-STR-04). MTR §16 item 3 leaves aligning with Progress to a decision.
/// <summary>The track of a meter: structural, with no dependency on the root.</summary>
public sealed class MeterTrackTests : IDisposable
{
    private readonly BunitContext context = new();

    public MeterTrackTests()
    {
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "renders without an enclosing root")]
    [Trait("Requirement", "MTR-STR-03")]
    public void Renders_without_an_enclosing_root()
    {
        IRenderedComponent<MeterTrack> cut = context.Render<MeterTrack>(p => p.AddChildContent("track"));

        IElement track = cut.Find("div");
        Assert.Equal("track", track.TextContent);
    }

    [Fact(DisplayName = "carries only the attributes the consumer passes")]
    [Trait("Requirement", "MTR-STR-04")]
    [Trait("Requirement", "MTR-DOM-02")]
    [Trait("Requirement", "MTR-FOC-01")]
    public void Carries_only_the_attributes_the_consumer_passes()
    {
        IRenderedComponent<MeterTrack> cut = context.Render<MeterTrack>(p => p
            .AddUnmatched("data-testid", "track")
            .AddUnmatched("class", "mine"));

        IElement track = cut.Find("div");
        // bUnit adds its own element-reference marker to a captured element; everything else is the consumer's.
        string[] names = track.Attributes.Select(a => a.Name).Where(n => !n.StartsWith("blazor:", StringComparison.Ordinal)).Order().ToArray();
        Assert.Equal(["class", "data-testid"], names);
    }

    [Fact(DisplayName = "renders inside a root with the same markup")]
    [Trait("Requirement", "MTR-STR-03")]
    public void Renders_inside_a_root_with_the_same_markup()
    {
        IRenderedComponent<MeterRoot> cut = context.Render<MeterRoot>(p => p
            .Add(x => x.Value, 30)
            .AddChildContent<MeterTrack>(t => t.AddUnmatched("data-testid", "track")));

        IElement track = cut.Find("[data-testid=track]");
        Assert.Equal("div", track.TagName, ignoreCase: true);
        string[] names = track.Attributes.Select(a => a.Name).Where(n => !n.StartsWith("blazor:", StringComparison.Ordinal)).ToArray();
        Assert.Equal(["data-testid"], names);
    }
}

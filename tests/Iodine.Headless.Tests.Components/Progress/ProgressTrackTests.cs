using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

// ProgressTrack.test.tsx has only the conformance suite (ProgressTrackConformanceTests); these are the port's own tests for
// the rows that cite no Base UI test (PRG-STR-02 for the track, PRG-STR-03) and for the track outside a root, which
// throws here unlike Meter's.
/// <summary>The track of a progress bar: structural, with the root's status attributes.</summary>
public sealed class ProgressTrackTests : IDisposable
{
    private readonly BunitContext context = new();

    public ProgressTrackTests()
    {
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "carries only the status attribute and the attributes the consumer passes")]
    [Trait("Requirement", "PRG-STR-03")]
    [Trait("Requirement", "PRG-DOM-02")]
    public void Carries_only_the_status_attribute_and_the_attributes_the_consumer_passes()
    {
        IRenderedComponent<ProgressRoot> cut = context.Render<ProgressRoot>(p => p
            .Add(x => x.Value, 40)
            .AddChildContent<ProgressTrack>(t => t
                .AddUnmatched("data-testid", "track")
                .AddUnmatched("class", "mine")));

        IElement track = cut.Find("[data-testid=track]");
        Assert.Equal("div", track.TagName, ignoreCase: true);
        // bUnit adds its own element-reference marker to a captured element; everything else is the status and the consumer's.
        string[] names = track.Attributes.Select(a => a.Name).Where(n => !n.StartsWith("blazor:", StringComparison.Ordinal)).Order().ToArray();
        Assert.Equal(["class", "data-progressing", "data-testid"], names);
        Assert.Equal(string.Empty, track.GetAttribute("data-progressing"));
    }

    [Fact(DisplayName = "throws a descriptive error when rendered outside <ProgressRoot>")]
    [Trait("Requirement", "PRG-STR-02")]
    [Trait("Requirement", "PRG-EDGE-01")]
    public void Throws_a_descriptive_error_when_rendered_outside_ProgressRoot()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => context.Render<ProgressTrack>());

        Assert.Equal("Blazix: The `ProgressRoot` context is missing. Progress parts must be placed within `<ProgressRoot>`.", error.Message);
    }
}

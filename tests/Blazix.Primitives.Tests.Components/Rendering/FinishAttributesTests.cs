using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Blazix.Primitives.Tests.Components.Rendering;

/// <summary>
/// The pipeline's last word to the part (ADR 02 Amendments 2026-10-21, M1 track 6): <c>FinishAttributes</c> runs once per
/// render after the merge, before the element, the substitute or the callback receives the attributes, and what it
/// replaces wins over the consumer (the handover of a style a JavaScript module owns, ADR 04 Amendments 2026-10-18).
/// </summary>
public sealed class FinishAttributesTests : IDisposable
{
    private readonly BunitContext context = new();

    public FinishAttributesTests()
    {
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact]
    public void The_part_sees_the_merged_style_and_what_it_sets_is_rendered()
    {
        IRenderedComponent<FinishingPart> cut = context.Render<FinishingPart>(p => p
            .AddUnmatched("style", "color: red")
            .AddUnmatched("id", "")
            .Add(x => x.StyleValue, (Func<EmptyState, string?>)(_ => "margin: 0")));

        Assert.Equal(["color: red; margin: 0;"], cut.Instance.SeenStyles);
        Assert.Equal("--x: 1; color: red; margin: 0;", cut.Find("div").GetAttribute("style"));
        Assert.Equal("own", cut.Find("div").GetAttribute("id"));
    }

    [Fact]
    public void A_substitute_callback_receives_the_finished_attributes()
    {
        IReadOnlyDictionary<string, object>? received = null;
        context.Render<FinishingPart>(p => p
            .AddUnmatched("style", "color: red")
            .Add(x => x.RenderFunction, (RenderFragment<RenderProps<EmptyState>>)(props => _ => received = props.Attributes)));

        Assert.Equal("--x: 1; color: red;", received!["style"]);
    }

    [Fact]
    public void What_a_part_changes_there_wins_over_the_consumer_so_review_holds_it_to_its_own_attributes()
    {
        // The pipeline cannot tell a part-owned attribute from any other: a part that changed the consumer's `title` here
        // would win. ADR 02 Amendments 2026-10-21 restrict the hook to part-owned attributes and documented fallbacks,
        // which src/Blazix.Primitives/Rendering/CONTEXT.md lists as a human check.
        IRenderedComponent<FinishingPart> cut = context.Render<FinishingPart>(p => p.AddUnmatched("title", "consumer").AddUnmatched("id", "kept"));

        Assert.Equal("part", cut.Find("div").GetAttribute("title"));
        Assert.Equal("kept", cut.Find("div").GetAttribute("id"));
    }

    /// <summary>
    /// A part that prepends a declaration to the merged style and replaces an empty consumer id; it also overrides the
    /// consumer's <c>title</c>, which a library part must not do, to show that only review prevents it.
    /// </summary>
    public sealed class FinishingPart : PartBase<EmptyState>
    {
        public List<string?> SeenStyles { get; } = [];

        /// <inheritdoc />
        protected override string DefaultTagName => "div";

        /// <inheritdoc />
        protected override EmptyState State => EmptyState.Instance;

        /// <inheritdoc />
        protected override void FinishAttributes(AttributeList attributes)
        {
            string? style = attributes.TryGetValue(MergeProps.StyleAttribute, out AttributeValue value) && value.TryGetValue(out string text) ? text : null;
            SeenStyles.Add(style);
            attributes.Set(MergeProps.StyleAttribute, MergeProps.MergeStyles("--x: 1", style));
            if (attributes.TryGetValue("id", out AttributeValue id) && id.TryGetValue(out string idText) && idText.Length == 0)
            {
                attributes.Set("id", "own");
            }

            if (attributes.Contains("title"))
            {
                attributes.Set("title", "part");
            }
        }
    }
}

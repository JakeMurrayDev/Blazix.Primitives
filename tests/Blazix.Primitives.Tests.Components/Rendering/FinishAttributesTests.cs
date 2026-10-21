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

    /// <summary>A part that prepends a declaration to the merged style and replaces an empty consumer id.</summary>
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
        }
    }
}

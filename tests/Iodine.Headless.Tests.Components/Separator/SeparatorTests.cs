using System.Reflection;
using AngleSharp.Dom;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using LibraryOrientation = Blazix.Primitives.Orientation;

// The namespace is the project's root, not a `Separator` namespace (see SeparatorConformanceTests). `Orientation` is
// aliased because SampleRoot.cs declares a test-local `Orientation` in this namespace that would shadow the library's.
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// <c>Separator.test.tsx</c> (SEP-STR-01, SEP-API-01, SEP-ARIA-01, SEP-ARIA-02), then the port's own tests for the rows
/// that cite no Base UI test (SEP-STA-01, SEP-DOM-01, SEP-DOM-02, SEP-FOC-01) and for the typed port's forms of
/// SEP-API-02 and SEP-API-03.
/// </summary>
public sealed class SeparatorTests : IDisposable
{
    private readonly BunitContext context = new();

    public SeparatorTests()
    {
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "renders a div with the `separator` role")]
    [Trait("Requirement", "SEP-STR-01")]
    [Trait("Requirement", "SEP-ARIA-01")]
    public void Renders_a_div_with_the_separator_role()
    {
        IRenderedComponent<Separator> cut = context.Render<Separator>();

        IElement separator = cut.Find("[role=separator]");
        Assert.Equal("div", separator.TagName, ignoreCase: true);
        // SEP-STR-01: exactly one host element and nothing else.
        Assert.Single(cut.FindAll("*"));
    }

    [Theory(DisplayName = "sets aria-orientation=\"$orientation\"")]
    [Trait("Requirement", "SEP-ARIA-02")]
    [Trait("Requirement", "SEP-API-01")]
    [InlineData(LibraryOrientation.Horizontal, "horizontal")]
    [InlineData(LibraryOrientation.Vertical, "vertical")]
    public void Sets_aria_orientation(LibraryOrientation orientation, string expected)
    {
        IRenderedComponent<Separator> cut = context.Render<Separator>(p => p.Add(x => x.Orientation, orientation));

        Assert.Equal(expected, cut.Find("[role=separator]").GetAttribute("aria-orientation"));
    }

    // Port's own title (SEP-API-01's "omitted" clause has no Base UI test).
    [Fact(DisplayName = "defaults the orientation to horizontal when it is omitted")]
    [Trait("Requirement", "SEP-API-01")]
    public void Defaults_the_orientation_to_horizontal_when_it_is_omitted()
    {
        IRenderedComponent<Separator> cut = context.Render<Separator>();

        IElement separator = cut.Find("[role=separator]");
        Assert.Equal(LibraryOrientation.Horizontal, cut.Instance.Orientation);
        Assert.Equal("horizontal", separator.GetAttribute("aria-orientation"));
        Assert.Equal("horizontal", separator.GetAttribute("data-orientation"));
    }

    // Port's own title (SEP-DOM-01 cites no Base UI test).
    [Theory(DisplayName = "sets data-orientation=\"$orientation\" from the state")]
    [Trait("Requirement", "SEP-DOM-01")]
    [Trait("Requirement", "SEP-STA-01")]
    [InlineData(LibraryOrientation.Horizontal, "horizontal")]
    [InlineData(LibraryOrientation.Vertical, "vertical")]
    public void Sets_data_orientation_from_the_state(LibraryOrientation orientation, string expected)
    {
        IRenderedComponent<Separator> cut = context.Render<Separator>(p => p.Add(x => x.Orientation, orientation));

        Assert.Equal(expected, cut.Find("[role=separator]").GetAttribute(SeparatorDataAttributes.Orientation));
    }

    // Port's own title (SeparatorDataAttributes mirrors SeparatorDataAttributes.ts).
    [Fact(DisplayName = "SeparatorDataAttributes names the data-orientation attribute")]
    [Trait("Requirement", "SEP-DOM-01")]
    public void SeparatorDataAttributes_names_the_data_orientation_attribute()
    {
        Assert.Equal("data-orientation", SeparatorDataAttributes.Orientation);
    }

    // Port's own title (SEP-STA-01 cites no Base UI test): the record has one field, and it is what the functions receive.
    [Fact(DisplayName = "the state record has exactly the field orientation")]
    [Trait("Requirement", "SEP-STA-01")]
    public void The_state_record_has_exactly_the_field_orientation()
    {
        PropertyInfo[] properties = typeof(SeparatorState).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        PropertyInfo only = Assert.Single(properties);
        Assert.Equal("Orientation", only.Name);
        Assert.Equal(typeof(LibraryOrientation), only.PropertyType);
    }

    // Port's own title (SEP-API-02's state-function clause in the typed port; SEP-STA-01).
    [Fact(DisplayName = "passes the effective orientation to the class and style functions")]
    [Trait("Requirement", "SEP-API-02")]
    [Trait("Requirement", "SEP-STA-01")]
    public void Passes_the_effective_orientation_to_the_class_and_style_functions()
    {
        List<SeparatorState> seen = [];

        IRenderedComponent<Separator> cut = context.Render<Separator>(p => p
            .Add(x => x.Orientation, LibraryOrientation.Vertical)
            .Add(x => x.ClassValue, (Func<SeparatorState, string?>)(state =>
            {
                seen.Add(state);
                return state.Orientation == LibraryOrientation.Vertical ? "is-vertical" : "is-horizontal";
            }))
            .Add(x => x.StyleValue, (Func<SeparatorState, string?>)(state => state.Orientation == LibraryOrientation.Vertical ? "width: 1px" : "height: 1px")));

        IElement separator = cut.Find("[role=separator]");
        Assert.True(separator.ClassList.Contains("is-vertical"));
        Assert.Contains("width: 1px", separator.GetAttribute("style"));
        Assert.All(seen, state => Assert.Equal(LibraryOrientation.Vertical, state.Orientation));
    }

    // Port's own title (SEP-API-03: the typed parameter is consumed, never an attribute named `orientation`).
    [Theory(DisplayName = "does not forward the orientation as an attribute named orientation")]
    [Trait("Requirement", "SEP-API-03")]
    [InlineData(LibraryOrientation.Horizontal)]
    [InlineData(LibraryOrientation.Vertical)]
    public void Does_not_forward_the_orientation_as_an_attribute_named_orientation(LibraryOrientation orientation)
    {
        IRenderedComponent<Separator> cut = context.Render<Separator>(p => p.Add(x => x.Orientation, orientation));

        IElement separator = cut.Find("[role=separator]");
        Assert.False(separator.HasAttribute("orientation"));
        Assert.DoesNotContain(separator.Attributes, attribute => attribute.Name.Equals("orientation", StringComparison.OrdinalIgnoreCase));
    }

    // Port's own title (SEP-DOM-02 cites no Base UI test): the built-in attributes are merged first, the consumer's last.
    [Fact(DisplayName = "a consumer role, aria-orientation and data-orientation win over the built-in ones")]
    [Trait("Requirement", "SEP-DOM-02")]
    public void A_consumer_role_aria_orientation_and_data_orientation_win_over_the_built_in_ones()
    {
        IRenderedComponent<Separator> cut = context.Render<Separator>(p => p
            .Add(x => x.Orientation, LibraryOrientation.Horizontal)
            .AddUnmatched("role", "presentation")
            .AddUnmatched("aria-orientation", "vertical")
            .AddUnmatched("data-orientation", "custom")
            .AddUnmatched("data-testid", "override"));

        IElement separator = cut.Find("[data-testid=override]");
        Assert.Equal("presentation", separator.GetAttribute("role"));
        Assert.Equal("vertical", separator.GetAttribute("aria-orientation"));
        Assert.Equal("custom", separator.GetAttribute("data-orientation"));
    }

    // Port's own title (SEP-FOC-01 cites no Base UI test).
    [Theory(DisplayName = "emits no tabindex, and keeps one the consumer adds")]
    [Trait("Requirement", "SEP-FOC-01")]
    [InlineData(LibraryOrientation.Horizontal)]
    [InlineData(LibraryOrientation.Vertical)]
    public void Emits_no_tabindex_and_keeps_one_the_consumer_adds(LibraryOrientation orientation)
    {
        IRenderedComponent<Separator> plain = context.Render<Separator>(p => p.Add(x => x.Orientation, orientation));
        IRenderedComponent<Separator> focusable = context.Render<Separator>(p => p
            .Add(x => x.Orientation, orientation)
            .AddUnmatched("tabindex", "0"));

        Assert.False(plain.Find("[role=separator]").HasAttribute("tabindex"));
        Assert.Equal("0", focusable.Find("[role=separator]").GetAttribute("tabindex"));
    }
}

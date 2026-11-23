using Blazix.Primitives.Rendering;
using Blazix.Primitives.Tests.Components.Rendering.Conformance;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The listbox separator's attributes at L2 (RND-STR-11, API-13, DOM-53, ARIA-02): ListboxSeparator.test.tsx's titles on the
/// test-only separator, which the families' separators re-run.
/// </summary>
public sealed class ListboxSeparatorTests : IDisposable
{
    private readonly BunitContext context = new();

    public ListboxSeparatorTests()
    {
        context.Services.AddBlazixPrimitives();
    }

    /// <inheritdoc />
    public void Dispose() => context.Dispose();

    [Fact(DisplayName = "has role=\"presentation\" and defaults to horizontal")]
    [Trait("Requirement", "RND-ARIA-02")]
    [Trait("Requirement", "RND-DOM-53")]
    public void Has_role_presentation_and_defaults_to_horizontal()
    {
        IRenderedComponent<TestListboxSeparator> cut = context.Render<TestListboxSeparator>();

        AngleSharp.Dom.IElement separator = cut.Find("div");
        Assert.Equal("presentation", separator.GetAttribute("role"));
        Assert.Equal("horizontal", separator.GetAttribute("data-orientation"));
        Assert.False(separator.HasAttribute("aria-orientation"));
    }

    [Theory(DisplayName = "sets data-orientation to $orientation")]
    [InlineData(Orientation.Horizontal, "horizontal")]
    [InlineData(Orientation.Vertical, "vertical")]
    [Trait("Requirement", "RND-API-13")]
    public void Sets_data_orientation(Orientation orientation, string expected)
    {
        IRenderedComponent<TestListboxSeparator> cut = context.Render<TestListboxSeparator>(p => p.Add(x => x.Orientation, orientation));

        Assert.Equal(expected, cut.Find("div").GetAttribute("data-orientation"));
    }

    // RND-STR-11, ARIA-02: the families' separators are the shared implementation. Base UI's describe.each runs one title per
    // family's part; each family adds its case (Select, M5 track 9; Combobox and Autocomplete, tracks 10 and 11).
    [Theory(DisplayName = "exposes the listbox separator behavior")]
    [InlineData("Select.Separator")]
    [Trait("Requirement", "RND-STR-11")]
    [Trait("Requirement", "RND-ARIA-02")]
    public void Exposes_the_listbox_separator_behavior(string part)
    {
        IRenderedComponent<SelectSeparator> cut = part switch
        {
            "Select.Separator" => context.Render<SelectSeparator>(p => p.AddUnmatched("data-testid", "separator")),
            _ => throw new ArgumentOutOfRangeException(nameof(part)),
        };

        AngleSharp.Dom.IElement element = cut.Find("[data-testid=separator]");
        Assert.Equal("DIV", element.TagName);
        Assert.Equal("presentation", element.GetAttribute("role"));
        Assert.Equal("horizontal", element.GetAttribute("data-orientation"));
        Assert.False(element.HasAttribute("aria-orientation"));
    }
}

// The conformance suite on the test-only separator (RND-STR-11: a `div`).
/// <summary>The conformance suite on <see cref="TestListboxSeparator"/>.</summary>
public sealed class TestListboxSeparatorConformanceTests : PartConformanceTests<TestListboxSeparator>
{
    /// <inheritdoc />
    protected override ConformanceOptions<TestListboxSeparator> Options { get; } = new("div");

    /// <inheritdoc />
    protected override ElementReference? ElementOf(TestListboxSeparator part) => part.Element;

    /// <inheritdoc />
    protected override void SetRenderFunction(ComponentParameterCollectionBuilder<TestListboxSeparator> p, Func<IReadOnlyDictionary<string, object>, RenderFragment> render)
    {
        p.Add(x => x.RenderFunction, (RenderFragment<RenderProps<TestListboxSeparatorState>>)(props => render(props.Attributes)));
    }

    /// <inheritdoc />
    protected override void SetClassValue(ComponentParameterCollectionBuilder<TestListboxSeparator> p, Func<string?> classValue)
    {
        p.Add(x => x.ClassValue, (Func<TestListboxSeparatorState, string?>)(_ => classValue()));
    }
}

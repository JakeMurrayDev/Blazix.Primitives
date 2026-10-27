using AngleSharp.Dom;
using Bunit;
using LibraryOrientation = Blazix.Primitives.Orientation;

// The namespace is the project's root: a namespace named after the family would hide the parts' types. `Orientation` is
// aliased because SampleRoot.cs declares a test-local `Orientation` in this namespace that would shadow the library's.
namespace Blazix.Primitives.Tests.Components;

// ToolbarSeparator.test.tsx's titles verbatim and port titles (marked as such).
/// <summary>The toolbar separator: a separator perpendicular to its toolbar unless its orientation is supplied.</summary>
public sealed class ToolbarSeparatorTests : IDisposable
{
    private readonly ToolbarTestContext test = new();

    private BunitContext Context => test.Context;

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Theory(DisplayName = "uses a %s separator in a %s toolbar")]
    [Trait("Requirement", "TLB-API-17")]
    [Trait("Requirement", "TLB-DOM-10")]
    [Trait("Requirement", "TLB-ARIA-07")]
    [InlineData("horizontal", LibraryOrientation.Vertical)]
    [InlineData("vertical", LibraryOrientation.Horizontal)]
    public void Uses_a_separator_perpendicular_to_its_toolbar(string separatorOrientation, LibraryOrientation toolbarOrientation)
    {
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p
            .Add(x => x.Orientation, toolbarOrientation)
            .AddChildContent<ToolbarSeparator>());

        Assert.Equal(separatorOrientation, cut.Find("[role=separator]").GetAttribute("aria-orientation"));
    }

    [Fact(DisplayName = "allows its orientation to be overridden")]
    [Trait("Requirement", "TLB-API-17")]
    [Trait("Requirement", "TLB-EDGE-08")]
    public void Allows_its_orientation_to_be_overridden()
    {
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p
            .Add(x => x.Orientation, LibraryOrientation.Horizontal)
            .AddChildContent<ToolbarSeparator>(s => s.Add(x => x.Orientation, LibraryOrientation.Horizontal)));

        Assert.Equal("horizontal", cut.Find("[role=separator]").GetAttribute("aria-orientation"));
    }

    [Fact(DisplayName = "throws a descriptive error when rendered outside Toolbar.Root")]
    [Trait("Requirement", "TLB-STR-02")]
    [Trait("Requirement", "TLB-EDGE-01")]
    public void Throws_a_descriptive_error_when_rendered_outside_toolbar_root()
    {
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => Context.Render<ToolbarSeparator>());

        Assert.Equal("Blazix: The `ToolbarRoot` context is missing. Toolbar parts must be placed within `<ToolbarRoot>`.", error.Message);
    }

    [Fact(DisplayName = "port: data-orientation and the state carry the resolved orientation, which follows the toolbar's")]
    [Trait("Requirement", "TLB-DOM-10")]
    [Trait("Requirement", "TLB-STA-01")]
    [Trait("Requirement", "TLB-API-18")]
    public void Port_data_orientation_and_the_state_carry_the_resolved_orientation()
    {
        List<ToolbarSeparatorState> states = [];
        IRenderedComponent<ToolbarRoot> cut = Context.Render<ToolbarRoot>(p => p.AddChildContent<ToolbarSeparator>(s => s
            .AddUnmatched("data-testid", "separator")
            .Add(x => x.ClassValue, state =>
            {
                states.Add(state);
                return null;
            })));

        IElement separator = cut.Find("[data-testid=separator]");
        Assert.Equal("vertical", separator.GetAttribute("data-orientation"));
        Assert.Equal(new ToolbarSeparatorState(LibraryOrientation.Vertical), states[^1]);
        Assert.False(separator.HasAttribute("tabindex"));

        cut.Render(p => p.Add(x => x.Orientation, LibraryOrientation.Vertical));

        Assert.Equal("horizontal", cut.Find("[data-testid=separator]").GetAttribute("data-orientation"));
        Assert.Equal("horizontal", cut.Find("[data-testid=separator]").GetAttribute("aria-orientation"));
        Assert.Equal(new ToolbarSeparatorState(LibraryOrientation.Horizontal), states[^1]);
        Assert.Equal("data-orientation", ToolbarSeparatorDataAttributes.Orientation);
    }
}

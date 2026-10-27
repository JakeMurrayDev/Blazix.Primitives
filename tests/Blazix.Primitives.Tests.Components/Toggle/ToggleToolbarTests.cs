using Bunit;

// Family tests use the project's root namespace: a `Toggle` namespace would hide the `Toggle` type.
namespace Blazix.Primitives.Tests.Components;

// The toolbar clauses of toggle.md's rows (M3 track 4). The direct members of a toolbar's ToggleGroup are in
// ToggleGroupToolbarTests.
/// <summary>A toggle inside a toolbar without a toggle group.</summary>
public sealed class ToggleToolbarTests : IDisposable
{
    private readonly ToolbarTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: a bare toggle under ToolbarRoot registers nothing and is in composite button mode")]
    [Trait("Requirement", "TGL-EDGE-09")]
    [Trait("Requirement", "TLB-EDGE-09")]
    public void Port_a_bare_toggle_under_toolbar_root_registers_nothing()
    {
        IRenderedComponent<ToolbarRoot> cut = test.Context.Render<ToolbarRoot>(p => p.AddChildContent<Toggle>());

        Assert.Empty(ToolbarTestContext.ItemIds(test.LastCompositeOptions));
        Assert.Contains("\"composite\":true", Assert.Single(test.ButtonAttaches), StringComparison.Ordinal);
        Assert.False(cut.Find("button").HasAttribute("tabindex"));
        Assert.Equal("false", cut.Find("button").GetAttribute("aria-disabled"));
    }
}

using AngleSharp.Dom;
using Bunit;

// Family tests use the project's root namespace.
namespace Blazix.Primitives.Tests.Components;

// ComboboxRoot.test.tsx's `prop: grid` titles cited at L2 whose assertions are what C# renders: the grid, gridcell and
// rowgroup roles. The arrow-key titles are the `composite` module's grid navigation (CMB-API-21's grid-navigation clause
// is L4).
/// <summary>Combobox's root at L2: ComboboxRoot.test.tsx's <c>prop: grid</c> block.</summary>
public sealed class ComboboxRootPropGridTests : IDisposable
{
    private readonly ComboboxTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "sets grid roles when grid is enabled and rows are used")]
    [Trait("Requirement", "CMB-API-21")]
    [Trait("Requirement", "CMB-ARIA-09")]
    public void Sets_grid_roles_when_grid_is_enabled_and_rows_are_used()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = Render([["1", "2", "3"], ["4", "5", "6"]]);

        Assert.Single(cut.FindAll("[role=grid]"));
        Assert.Equal(6, cut.FindAll("[role=gridcell]").Count);
    }

    [Fact(DisplayName = "does not render aria-orientation on the grid role")]
    [Trait("Requirement", "CMB-ARIA-08")]
    public void Does_not_render_aria_orientation_on_the_grid_role()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = Render([["1", "2"]]);

        // The grid role does not support aria-orientation (axe: aria-allowed-attr).
        Assert.False(cut.Find("[role=grid]").HasAttribute("aria-orientation"));
    }

    [Fact(DisplayName = "renders groups with the rowgroup role")]
    [Trait("Requirement", "CMB-API-21")]
    [Trait("Requirement", "CMB-ARIA-10")]
    public void Renders_groups_with_the_rowgroup_role()
    {
        IRenderedComponent<ComboboxRootBatchBHarness> cut = Render([["1", "2"]], grouped: true);

        // `grid` may only own `row`/`rowgroup` elements (axe: aria-required-children), and `row` must be owned by `grid`,
        // `rowgroup`, or `treegrid` (axe: aria-required-parent).
        Assert.Empty(cut.FindAll("[role=group]"));
        IElement rowgroup = cut.Find("[role=rowgroup]");
        string labelledBy = rowgroup.GetAttribute("aria-labelledby") ?? string.Empty;
        Assert.Equal("Fruits", cut.Find("#" + labelledBy).TextContent);
    }

    private IRenderedComponent<ComboboxRootBatchBHarness> Render(IReadOnlyList<IReadOnlyList<string>> rows, bool grouped = false) =>
        test.Context.Render<ComboboxRootBatchBHarness>(p => p
            .Add(x => x.RootParameters, new Dictionary<string, object?> { ["Grid"] = true, ["DefaultOpen"] = true })
            .Add(x => x.Rows, rows)
            .Add(x => x.GroupRows, grouped)
            .Add(x => x.GroupLabel, grouped ? "Fruits" : null));
}

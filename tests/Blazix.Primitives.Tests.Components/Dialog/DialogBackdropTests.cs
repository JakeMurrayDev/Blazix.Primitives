using Bunit;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The <c>[L2]</c> titles of <c>DialogBackdrop.test.tsx</c>: the role (DLG-ARIA-08) and the nested rule with
/// <c>ForceRender</c> (DLG-STR-05, API-19, ANI-01). Titles are Base UI's.
/// </summary>
public sealed class DialogBackdropTests : IDisposable
{
    private readonly DialogTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "has role=\"presentation\"")]
    [Trait("Requirement", "DLG-ARIA-08")]
    public void Has_role_presentation()
    {
        IRenderedComponent<DialogRoot<NoPayload>> cut = test.Context.Render<DialogRoot<NoPayload>>(p => p
            .Add(x => x.Open, true)
            .AddChildContent<DialogBackdrop>(backdrop => backdrop.AddUnmatched("data-testid", "backdrop")));

        Assert.Equal("presentation", cut.Find("[data-testid=backdrop]").GetAttribute("role"));
    }

    [Fact(DisplayName = "renders only the root backdrop by default")]
    [Trait("Requirement", "DLG-STR-05")]
    public void Renders_only_the_root_backdrop_by_default()
    {
        IRenderedComponent<DialogNestedHarness> cut = test.Context.Render<DialogNestedHarness>(p => p.Add(x => x.Levels, 2));

        Assert.Single(cut.FindAll("[data-testid=level-1-backdrop]"));
        Assert.Empty(cut.FindAll("[data-testid=level-2-backdrop]"));
    }

    [Fact(DisplayName = "always renders by default when not nested")]
    [Trait("Requirement", "DLG-STR-05")]
    [Trait("Requirement", "DLG-ANI-01")]
    public void Always_renders_by_default_when_not_nested()
    {
        IRenderedComponent<DialogHarness> cut = test.Context.Render<DialogHarness>(p => p
            .Add(x => x.DefaultOpen, true)
            .Add(x => x.WithTrigger, false)
            .Add(x => x.WithBackdrop, true)
            .Add(x => x.BackdropInPortal, false));

        Assert.Single(cut.FindAll("[data-testid=backdrop]"));
    }

    [Fact(DisplayName = "renders only the root backdrop with multiple nesting levels")]
    [Trait("Requirement", "DLG-STR-05")]
    public void Renders_only_the_root_backdrop_with_multiple_nesting_levels()
    {
        IRenderedComponent<DialogNestedHarness> cut = test.Context.Render<DialogNestedHarness>(p => p.Add(x => x.Levels, 3));

        Assert.Single(cut.FindAll("[data-testid=level-1-backdrop]"));
        Assert.Empty(cut.FindAll("[data-testid=level-2-backdrop]"));
        Assert.Empty(cut.FindAll("[data-testid=level-3-backdrop]"));
    }

    [Fact(DisplayName = "always renders when true")]
    [Trait("Requirement", "DLG-STR-05")]
    [Trait("Requirement", "DLG-API-19")]
    public void Always_renders_when_true()
    {
        IRenderedComponent<DialogNestedHarness> cut = test.Context.Render<DialogNestedHarness>(p => p.Add(x => x.Levels, 3).Add(x => x.ForceRender, true));

        Assert.Single(cut.FindAll("[data-testid=level-1-backdrop]"));
        Assert.Single(cut.FindAll("[data-testid=level-2-backdrop]"));
        Assert.Single(cut.FindAll("[data-testid=level-3-backdrop]"));
    }
}

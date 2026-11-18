using AngleSharp.Dom;
using Bunit;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// <c>DrawerProvider.test.tsx</c>, <c>DrawerIndent.test.tsx</c> and <c>DrawerIndentBackground.test.tsx</c> (titles verbatim)
/// and the root's registration (DRW-STA-14, API-14, STR-08, DOM-31). "Before passive effects" is the render batch: the
/// indent parts show the provider's new state in the batch that opened, closed or removed the root.
/// </summary>
public sealed class DrawerProviderTests : IDisposable
{
    private readonly DialogTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "stays active until every open drawer is closed or removed")]
    [Trait("Requirement", "DRW-STA-14")]
    [Trait("Requirement", "DRW-API-14")]
    public void Stays_active_until_every_open_drawer_is_closed_or_removed()
    {
        IRenderedComponent<DrawerProviderHarness> cut = test.Context.Render<DrawerProviderHarness>();
        Assert.True(Background(cut).HasAttribute("data-inactive"));

        cut.Render(p => p.Add(x => x.FirstOpen, true).Add(x => x.SecondOpen, false).Add(x => x.ShowSecond, true));
        Assert.True(Background(cut).HasAttribute("data-active"));

        cut.Render(p => p.Add(x => x.FirstOpen, false).Add(x => x.SecondOpen, true).Add(x => x.ShowSecond, true));
        Assert.True(Background(cut).HasAttribute("data-active"));

        cut.Render(p => p.Add(x => x.FirstOpen, false).Add(x => x.SecondOpen, true).Add(x => x.ShowSecond, false));
        Assert.True(Background(cut).HasAttribute("data-inactive"));
    }

    [Fact(DisplayName = "ignores redundant registry updates without disturbing active state")]
    [Trait("Requirement", "DRW-STA-14")]
    public void Ignores_redundant_registry_updates_without_disturbing_active_state()
    {
        DrawerProviderContext context = new();
        int changes = 0;
        context.AddListener(() => changes++);
        object registered = new();

        context.Report(registered, open: true);
        Assert.True(context.Active);

        context.Report(registered, open: true);
        context.Remove(new object());
        Assert.True(context.Active);

        context.Report(registered, open: false);
        Assert.False(context.Active);

        context.Remove(registered);
        context.Remove(registered);
        Assert.False(context.Active);
        Assert.Equal(2, changes);
    }

    // Base UI's "releases closed drawers once they are removed" forces a garbage collection in jsdom (not ported, the brief's
    // reason); this is its C# form: the set keeps no closed or removed root.
    [Fact(DisplayName = "port: the provider keeps no root once it reports closed or is removed")]
    [Trait("Requirement", "DRW-STA-14")]
    public void The_provider_keeps_no_closed_or_removed_root()
    {
        DrawerProviderContext context = new();
        for (int i = 0; i < 10; i++)
        {
            object drawer = new();
            context.Report(drawer, open: true);
            Assert.Equal(1, context.Count);
            context.Report(drawer, open: false);
            context.Remove(drawer);
        }

        Assert.Equal(0, context.Count);
        Assert.False(context.Active);
    }

    [Fact(DisplayName = "allows indent parts to render without a provider")]
    [Trait("Requirement", "DRW-STR-08")]
    [Trait("Requirement", "DRW-DOM-31")]
    public void Allows_indent_parts_to_render_without_a_provider()
    {
        IRenderedComponent<DrawerProviderHarness> cut = test.Context.Render<DrawerProviderHarness>(p => p.Add(x => x.NoProvider, true));

        Assert.True(cut.Find("[data-testid=indent]").HasAttribute("data-inactive"));
        Assert.True(Background(cut).HasAttribute("data-inactive"));
    }

    [Fact(DisplayName = "sets data-active when any drawer is open")]
    [Trait("Requirement", "DRW-DOM-31")]
    [Trait("Requirement", "DRW-STA-14")]
    public void Sets_data_active_when_any_drawer_is_open()
    {
        IRenderedComponent<DrawerProviderHarness> cut = test.Context.Render<DrawerProviderHarness>(p => p.Add(x => x.ShowSecond, false));
        IElement indent = cut.Find("[data-testid=indent]");
        Assert.True(indent.HasAttribute("data-inactive"));
        Assert.False(indent.HasAttribute("data-active"));

        cut.Render(p => p.Add(x => x.FirstOpen, true).Add(x => x.ShowSecond, false));

        indent = cut.Find("[data-testid=indent]");
        Assert.True(indent.HasAttribute("data-active"));
        Assert.False(indent.HasAttribute("data-inactive"));
        Assert.True(Background(cut).HasAttribute("data-active"));
    }

    // DrawerRoot.test.tsx "reports and unregisters $label before passive effects" (×3: controlled, default-open, and without
    // useId; the last is React 17's useId case, not ported): the open root registers in the batch that renders it, and its
    // removal unregisters in the batch that removes it.
    [Theory(DisplayName = "reports and unregisters $label before passive effects")]
    [InlineData("a controlled open drawer")]
    [InlineData("a default-open drawer")]
    [Trait("Requirement", "DRW-STA-14")]
    public void Reports_and_unregisters_before_passive_effects(string label)
    {
        bool controlled = label == "a controlled open drawer";
        IRenderedComponent<DrawerProviderHarness> cut = test.Context.Render<DrawerProviderHarness>(p => p
            .Add(x => x.SecondOpen, controlled)
            .Add(x => x.SecondDefaultOpen, !controlled));
        Assert.True(Background(cut).HasAttribute("data-active"));
        Assert.True(cut.Find("[data-testid=indent]").HasAttribute("data-active"));

        cut.Render(p => p.Add(x => x.SecondOpen, controlled).Add(x => x.SecondDefaultOpen, !controlled).Add(x => x.ShowSecond, false));

        Assert.True(Background(cut).HasAttribute("data-inactive"));
        Assert.True(cut.Find("[data-testid=indent]").HasAttribute("data-inactive"));
    }

    private static IElement Background(IRenderedComponent<DrawerProviderHarness> cut) => cut.Find("[data-testid=background]");
}

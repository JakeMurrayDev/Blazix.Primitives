using AngleSharp.Dom;
using Bunit;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary><c>DrawerContent.test.tsx</c>'s title outside its conformance suite (DRW-DOM-24, DRW-STR-06), titles verbatim.</summary>
public sealed class DrawerContentTests : IDisposable
{
    private readonly DialogTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "does not add public swipe-ignore attributes")]
    [Trait("Requirement", "DRW-DOM-24")]
    [Trait("Requirement", "DRW-STR-06")]
    public void Does_not_add_public_swipe_ignore_attributes()
    {
        IRenderedComponent<DrawerContentHarness> cut = test.Context.Render<DrawerContentHarness>();

        IElement content = cut.Find("[data-testid=content]");
        Assert.Equal("div", content.LocalName);
        Assert.Equal(string.Empty, content.GetAttribute("data-drawer-content"));
        Assert.False(content.HasAttribute("data-swipe-ignore"));
        Assert.False(content.HasAttribute("data-base-ui-swipe-ignore"));
    }
}

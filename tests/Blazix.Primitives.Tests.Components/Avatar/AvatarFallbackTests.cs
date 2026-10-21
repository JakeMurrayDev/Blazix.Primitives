using Bunit;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// <c>AvatarFallback.test.tsx</c> at L2. Base UI stubs the image's status computation; here the image's twin reports the
/// status the stub returned (the <c>dom</c> module's side, driven through <see cref="AvatarInterop"/>). The port's own
/// tests for the rows that cite no Base UI test follow the translated ones.
/// </summary>
public sealed class AvatarFallbackTests : IDisposable
{
    private readonly AvatarInterop interop = new();

    /// <inheritdoc />
    public void Dispose() => interop.Dispose();

    [Fact(DisplayName = "should not render the children if the image loaded")]
    [Trait("Requirement", "AVA-STA-05")]
    [Trait("Requirement", "AVA-DOM-09")]
    public void Should_not_render_the_children_if_the_image_loaded()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.Src, "avatar.png"));

        interop.ReportImage(cut, "loaded");

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=fallback]")));
    }

    [Fact(DisplayName = "should render the fallback if the image fails to load")]
    [Trait("Requirement", "AVA-STA-05")]
    [Trait("Requirement", "AVA-DOM-09")]
    public void Should_render_the_fallback_if_the_image_fails_to_load()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.Src, "avatar.png"));

        interop.ReportImage(cut, "error");

        cut.WaitForAssertion(() => Assert.Equal("JD", cut.Find("[data-testid=fallback]").TextContent));
    }

    [Fact(DisplayName = "shows the fallback when a loaded image is unmounted")]
    [Trait("Requirement", "AVA-STA-06")]
    public void Shows_the_fallback_when_a_loaded_image_is_unmounted()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.Src, "avatar.png"));
        interop.ReportImage(cut, "loaded");
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=fallback]")));
        Assert.NotNull(cut.Find("[data-testid=image]"));

        cut.Render(p => p.Add(x => x.ShowImage, false));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("[data-testid=fallback]")));
        Assert.Empty(cut.FindAll("[data-testid=image]"));
    }

    [Fact(DisplayName = "keeps fallback mounted and image unmounted while the image is loading")]
    [Trait("Requirement", "AVA-DOM-01")]
    [Trait("Requirement", "AVA-DOM-09")]
    [Trait("Requirement", "AVA-STA-08")]
    public void Keeps_fallback_mounted_and_image_unmounted_while_the_image_is_loading()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render();
        interop.ReportImage(cut, "error");

        Assert.Empty(cut.FindAll("[data-testid=image]"));
        Assert.NotNull(cut.Find("[data-testid=fallback]"));

        cut.Render(p => p.Add(x => x.Src, "avatar.png"));
        interop.ReportImage(cut, "loading");

        Assert.Empty(cut.FindAll("[data-testid=image]"));
        Assert.NotNull(cut.Find("[data-testid=fallback]"));
    }

    [Fact(DisplayName = "keeps only one of image or fallback mounted when switching to image")]
    [Trait("Requirement", "AVA-ANI-06")]
    [Trait("Requirement", "AVA-DOM-01")]
    public void Keeps_only_one_of_image_or_fallback_mounted_when_switching_to_image()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render();
        interop.ReportImage(cut, "error");
        Assert.Empty(cut.FindAll("[data-testid=image]"));
        Assert.NotNull(cut.Find("[data-testid=fallback]"));

        cut.Render(p => p.Add(x => x.Src, "avatar.png"));
        interop.ReportImage(cut, "loaded");

        // The status change renders the image and removes the fallback in the same dispatch.
        Assert.Empty(cut.FindAll("[data-testid=fallback]"));
        Assert.NotNull(cut.Find("[data-testid=image]"));
    }

    // Port's own title: AVA-STR-03 has no Base UI test of its own.
    [Fact(DisplayName = "renders the fallback for as long as the root has no image")]
    [Trait("Requirement", "AVA-STR-03")]
    public void Renders_the_fallback_for_as_long_as_the_root_has_no_image()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.ShowImage, false));

        Assert.Equal("JD", cut.Find("[data-testid=fallback]").TextContent);
        Assert.Empty(interop.DomAttaches);
    }

}

using Bunit;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// <c>AvatarFallback.test.tsx</c> at L2. Base UI stubs the image's status computation; here the image's twin reports the
/// status the stub returned (the <c>dom</c> module's side, driven through <see cref="AvatarInterop"/>), and the delay
/// runs under a <see cref="Microsoft.Extensions.Time.Testing.FakeTimeProvider"/> (ADR 06 §1). The port's own tests for
/// the rows that cite no Base UI test follow the translated ones.
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

    [Fact(DisplayName = "shows the fallback when the delay has elapsed")]
    [Trait("Requirement", "AVA-ANI-07")]
    [Trait("Requirement", "AVA-API-07")]
    [Trait("Requirement", "AVA-STR-03")]
    [Trait("Requirement", "AVA-DOM-09")]
    public void Shows_the_fallback_when_the_delay_has_elapsed()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.Delay, 100));

        Assert.Empty(cut.FindAll("[data-testid=fallback]"));

        interop.Clock.Advance(TimeSpan.FromMilliseconds(100));

        cut.WaitForAssertion(() => Assert.Equal("JD", cut.Find("[data-testid=fallback]").TextContent));
    }

    // Port's own title: the delay given as a TimeSpan (Duration, ADR 05 §4).
    [Fact(DisplayName = "shows the fallback when a TimeSpan delay has elapsed")]
    [Trait("Requirement", "AVA-ANI-07")]
    [Trait("Requirement", "AVA-API-07")]
    public void Shows_the_fallback_when_a_TimeSpan_delay_has_elapsed()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.DelaySpan, TimeSpan.FromMilliseconds(150)));

        interop.Clock.Advance(TimeSpan.FromMilliseconds(149));
        Assert.Empty(cut.FindAll("[data-testid=fallback]"));

        interop.Clock.Advance(TimeSpan.FromMilliseconds(1));

        cut.WaitForAssertion(() => Assert.Equal("JD", cut.Find("[data-testid=fallback]").TextContent));
    }

    [Fact(DisplayName = "shows the fallback immediately when delay is 0")]
    [Trait("Requirement", "AVA-ANI-08")]
    [Trait("Requirement", "AVA-DOM-09")]
    public void Shows_the_fallback_immediately_when_delay_is_0()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.Delay, 0));

        // No time is advanced: a zero delay renders the fallback in the first render.
        Assert.Equal("JD", cut.Find("[data-testid=fallback]").TextContent);
        Assert.Equal(1, cut.FindComponent<AvatarFallback>().RenderCount);
    }

    [Fact(DisplayName = "shows the fallback when delay changes to 0")]
    [Trait("Requirement", "AVA-ANI-08")]
    [Trait("Requirement", "AVA-ANI-07")]
    public void Shows_the_fallback_when_delay_changes_to_0()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.Delay, 100));
        interop.ReportImage(cut, "error");
        Assert.Empty(cut.FindAll("[data-testid=fallback]"));

        cut.Render(p => p.Add(x => x.Delay, 0));

        Assert.Equal("JD", cut.Find("[data-testid=fallback]").TextContent);
    }

    [Fact(DisplayName = "keeps the fallback visible when delay changes from undefined to a number")]
    [Trait("Requirement", "AVA-ANI-07")]
    public void Keeps_the_fallback_visible_when_delay_changes_from_undefined_to_a_number()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render();
        interop.ReportImage(cut, "error");
        Assert.Equal("JD", cut.Find("[data-testid=fallback]").TextContent);

        cut.Render(p => p.Add(x => x.Delay, 100));

        Assert.Equal("JD", cut.Find("[data-testid=fallback]").TextContent);
    }

    [Fact(DisplayName = "keeps the fallback visible across a number -> undefined -> number delay change")]
    [Trait("Requirement", "AVA-ANI-07")]
    public void Keeps_the_fallback_visible_across_a_number_undefined_number_delay_change()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.Delay, 100));
        interop.ReportImage(cut, "error");

        // Hidden until the delay elapses.
        Assert.Empty(cut.FindAll("[data-testid=fallback]"));

        // Removing the delay (the C# default, 0) before it elapses shows the fallback at once.
        cut.Render(p => p.Add(x => x.Delay, 0));
        Assert.Equal("JD", cut.Find("[data-testid=fallback]").TextContent);

        // Restoring the delay does not hide the fallback again.
        cut.Render(p => p.Add(x => x.Delay, 100));
        Assert.Equal("JD", cut.Find("[data-testid=fallback]").TextContent);
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

    // Port's own title: AVA-EDGE-05 is untested in Base UI (§16 Q5). A negative delay hides the first render and shows
    // the fallback right after it, without a timer.
    [Fact(DisplayName = "shows a fallback with a negative delay right after its first render")]
    [Trait("Requirement", "AVA-EDGE-05")]
    public void Shows_a_fallback_with_a_negative_delay_right_after_its_first_render()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.Delay, -1));

        IRenderedComponent<AvatarFallback> fallback = cut.FindComponent<AvatarFallback>();
        Assert.Equal("JD", cut.Find("[data-testid=fallback]").TextContent);
        Assert.Equal(2, fallback.RenderCount);
    }

    // Port's own title: AVA-ANI-07's clause "cancelled on unmount".
    [Fact(DisplayName = "cancels the pending delay when the fallback is removed")]
    [Trait("Requirement", "AVA-ANI-07")]
    public void Cancels_the_pending_delay_when_the_fallback_is_removed()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.Delay, 100));
        IRenderedComponent<AvatarFallback> fallback = cut.FindComponent<AvatarFallback>();
        int renders = fallback.RenderCount;

        cut.Render(p => p.Add(x => x.ShowFallback, false));
        interop.Clock.Advance(TimeSpan.FromMilliseconds(100));

        Assert.Equal(renders, fallback.RenderCount);
        Assert.Empty(cut.FindAll("[data-testid=fallback]"));
    }

    // Port's own title: the fallback's id is what the `dom` module hides at load (ADR 04 Amendments 2026-10-18), and a
    // consumer id replaces the generated one.
    [Fact(DisplayName = "gives the image the ids of the fallbacks to hide when it loads")]
    [Trait("Requirement", "AVA-DOM-10")]
    [Trait("Requirement", "AVA-EDGE-02")]
    public void Gives_the_image_the_ids_of_the_fallbacks_to_hide_when_it_loads()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p
            .Add(x => x.Src, "avatar.png")
            .Add(x => x.FallbackAttributes, new Dictionary<string, object> { ["id"] = "initials" }));

        Assert.Equal("initials", cut.Find("[data-testid=fallback]").Id);
        Assert.Contains("\"hideWhenLoaded\":\"initials\"", (string)interop.DomAttaches[^1].Arguments[1]!, StringComparison.Ordinal);
    }
}

using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The Avatar image's twin contract (ADR 06 §1, L2): the <see cref="DomOptions"/> it sends on <c>attach</c> and
/// <c>update</c> (the source properties in the order the probe applies them, the fallback ids to hide), the
/// <c>ImageStatusChanged</c> sequences the module reports driven into C#, and the <see cref="TransitionsOptions"/> and
/// callbacks of its <see cref="TransitionsTwin"/>.
/// </summary>
public sealed class AvatarTwinWiringTests : IDisposable
{
    private readonly AvatarInterop interop = new();

    /// <inheritdoc />
    public void Dispose() => interop.Dispose();

    [Fact(DisplayName = "attaches one dom twin to the root's element in preload mode and updates it on a source change only")]
    [Trait("Requirement", "AVA-STA-09")]
    [Trait("Requirement", "AVA-ALG-01")]
    public void Attaches_one_dom_twin_to_the_root_in_preload_mode_and_updates_it_on_a_source_change_only()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.Src, "a.png"));

        JSRuntimeInvocation attach = Assert.Single(interop.DomAttaches);
        Assert.Equal(cut.FindComponent<AvatarRoot>().Instance.Element, Assert.IsType<ElementReference>(attach.Arguments[0]));
        Assert.Equal("""{"liveRegion":false,"image":{"src":"a.png","keepMounted":false}}""", attach.Arguments[1]);
        Assert.IsType<DotNetObjectReference<DomTwin>>(attach.Arguments[2]);

        cut.Render(p => p.Add(x => x.ShowFallback, true));
        Assert.Empty(interop.DomUpdates);

        cut.Render(p => p.Add(x => x.Src, "b.png").Add(x => x.ReferrerPolicy, "no-referrer"));
        JSRuntimeInvocation update = Assert.Single(interop.DomUpdates);
        Assert.Equal("""{"liveRegion":false,"image":{"src":"b.png","referrerPolicy":"no-referrer","keepMounted":false}}""", update.Arguments[0]);
        Assert.Single(interop.DomAttaches);
    }

    [Theory(DisplayName = "drives ImageStatusChanged into the image's status and callback")]
    [Trait("Requirement", "AVA-EVT-01")]
    [Trait("Requirement", "AVA-EVT-02")]
    [InlineData(new[] { "loading", "loaded" }, new[] { ImageLoadingStatus.Loading, ImageLoadingStatus.Loaded })]
    [InlineData(new[] { "loading", "error" }, new[] { ImageLoadingStatus.Loading, ImageLoadingStatus.Error })]
    [InlineData(new[] { "error" }, new[] { ImageLoadingStatus.Error })]
    [InlineData(new[] { "loaded" }, new[] { ImageLoadingStatus.Loaded })]
    [InlineData(new[] { "loaded", "loaded" }, new[] { ImageLoadingStatus.Loaded })]
    public void Drives_ImageStatusChanged_into_the_images_status_and_callback(string[] reports, ImageLoadingStatus[] expected)
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.Src, "a.png"));

        foreach (string report in reports)
        {
            interop.ReportImage(cut, report);
        }

        Assert.Equal(expected, cut.Instance.Statuses);
    }

    [Fact(DisplayName = "attaches the dom twin to the image element in kept-mounted mode and moves it on a switch")]
    [Trait("Requirement", "AVA-API-04")]
    [Trait("Requirement", "AVA-EDGE-07")]
    public void Attaches_the_dom_twin_to_the_image_element_in_kept_mounted_mode_and_moves_it_on_a_switch()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.Src, "a.png"));
        ElementReference? root = cut.FindComponent<AvatarRoot>().Instance.Element;

        cut.Render(p => p.Add(x => x.KeepMounted, true));

        Assert.Equal(2, interop.DomAttaches.Count);
        Assert.Equal(cut.FindComponent<AvatarImage>().Instance.Element, Assert.IsType<ElementReference>(interop.DomAttaches[1].Arguments[0]));
        Assert.Contains("\"keepMounted\":true", (string)interop.DomAttaches[1].Arguments[1]!, StringComparison.Ordinal);
        Assert.Single(AvatarInterop.Invocations(interop.DomHandle, "dispose"));

        cut.Render(p => p.Add(x => x.KeepMounted, false));

        Assert.Equal(root, Assert.IsType<ElementReference>(interop.DomAttaches[2].Arguments[0]));
    }

    [Fact(DisplayName = "attaches a transitions twin to a preload image with the options of an animated image and releases it at exit end")]
    [Trait("Requirement", "AVA-ANI-01")]
    [Trait("Requirement", "AVA-ANI-03")]
    public void Attaches_a_transitions_twin_to_a_preload_image_and_releases_it_at_exit_end()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.Src, "a.png"));
        Assert.Empty(interop.TransitionsAttaches);

        interop.ReportImage(cut, "loaded");

        JSRuntimeInvocation attach = Assert.Single(interop.TransitionsAttaches);
        Assert.Equal(cut.FindComponent<AvatarImage>().Instance.Element, Assert.IsType<ElementReference>(attach.Arguments[0]));
        Assert.IsType<DotNetObjectReference<TransitionsTwin>>(attach.Arguments[2]);

        // The status the element was rendered with travels on every message; the module reads it on attach only.
        interop.ReportImage(cut, "error");
        Assert.Equal(
            """{"open":false,"mounted":true,"status":"starting","enableIdleState":false,"deferEndingState":false,"keepMounted":false,"hiddenUntilFound":false,"hiddenWhenUnmounted":false,"suppressEndingStyle":false,"reportOpenComplete":false,"revealOnBeforeMatch":false,"unmountRequests":0}""",
            Assert.Single(interop.TransitionsUpdates).Arguments[0]);

        interop.CompleteExit(cut);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=image]")));
        Assert.Single(AvatarInterop.Invocations(interop.TransitionsHandle, "dispose"));
    }

    [Fact(DisplayName = "mirrors the transition status the module reports into the image's state")]
    [Trait("Requirement", "AVA-STA-02")]
    [Trait("Requirement", "AVA-STA-07")]
    public void Mirrors_the_transition_status_the_module_reports_into_the_images_state()
    {
        List<TransitionStatus?> seen = [];
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p
            .Add(x => x.Src, "a.png")
            .Add(x => x.ImageClassValue, (Func<AvatarImageState, string?>)(state => { seen.Add(state.TransitionStatus); return null; })));

        interop.ReportImage(cut, "loaded");
        Assert.Equal(TransitionStatus.Starting, seen[^1]);

        interop.ReportTransition(cut, null, mounted: true);
        Assert.Null(seen[^1]);

        interop.ReportImage(cut, "loading");
        interop.ReportTransition(cut, "ending", mounted: true);
        Assert.Equal(TransitionStatus.Ending, seen[^1]);

        // The data attributes are the module's: the frozen handover value stays in C#'s output and is never changed again.
        Assert.False(cut.Find("[data-testid=image]").HasAttribute("data-ending-style"));
    }

    [Fact(DisplayName = "attaches the kept-mounted transitions twin with the open-state selector once a status is known")]
    [Trait("Requirement", "AVA-ANI-01")]
    [Trait("Requirement", "AVA-ANI-05")]
    public void Attaches_the_kept_mounted_transitions_twin_with_the_open_state_selector_once_a_status_is_known()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.KeepMounted, true).Add(x => x.Src, "a.png"));
        Assert.Empty(interop.TransitionsAttaches);

        interop.ReportImage(cut, "loading");

        Assert.Equal(
            """{"open":false,"mounted":false,"enableIdleState":false,"deferEndingState":false,"keepMounted":true,"hiddenUntilFound":false,"hiddenWhenUnmounted":false,"suppressEndingStyle":true,"reportOpenComplete":false,"revealOnBeforeMatch":false,"unmountRequests":0,"openSelector":":not([aria-hidden])"}""",
            Assert.Single(interop.TransitionsAttaches).Arguments[1]);

        // A later load is not the first status: the module plays the enter phase, read from the applied render.
        cut.Find("[data-testid=image]").TriggerEvent("onload", new Microsoft.AspNetCore.Components.Web.ProgressEventArgs());

        Assert.Contains("\"open\":true", (string)Assert.Single(interop.TransitionsUpdates).Arguments[0]!, StringComparison.Ordinal);
        Assert.False(cut.Find("[data-testid=image]").HasAttribute("data-starting-style"));
    }
}

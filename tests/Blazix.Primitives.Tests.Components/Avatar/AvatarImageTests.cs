using AngleSharp.Dom;
using Blazix.Primitives.Interop;
using Blazix.Primitives.Rendering;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using ErrorEventArgs = Microsoft.AspNetCore.Components.Web.ErrorEventArgs;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// <c>AvatarImage.test.tsx</c> at L2. Base UI's tests replace the detached image or read the rendered one; here the
/// <c>dom</c> module's reports are driven through the image's twin (<see cref="AvatarInterop"/>), the element's
/// <c>load</c> and <c>error</c> events are dispatched to the part's handlers, and the <c>transitions</c> module's reports
/// are driven the same way. What only a browser shows (the probe itself, the derivation from the element, frame timing)
/// is L3 and L4.
/// </summary>
public sealed class AvatarImageTests : IDisposable
{
    private readonly AvatarInterop interop = new();

    /// <inheritdoc />
    public void Dispose() => interop.Dispose();

    [Fact(DisplayName = "passes native image props to the rendered image")]
    [Trait("Requirement", "AVA-API-02")]
    [Trait("Requirement", "AVA-API-03")]
    [Trait("Requirement", "AVA-API-06")]
    public void Passes_native_image_props_to_the_rendered_image()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p
            .Add(x => x.CrossOrigin, "anonymous")
            .Add(x => x.ReferrerPolicy, "no-referrer")
            .Add(x => x.Sizes, "48px")
            .Add(x => x.Src, "avatar.png")
            .Add(x => x.SrcSet, "avatar.png 1x, avatar@2x.png 2x"));
        interop.ReportImage(cut, "loaded");

        IElement image = cut.Find("[data-testid=image]");
        Assert.Equal("img", image.TagName, ignoreCase: true);
        Assert.Equal("anonymous", image.GetAttribute("crossorigin"));
        Assert.Equal("no-referrer", image.GetAttribute("referrerpolicy"));
        Assert.Equal("48px", image.GetAttribute("sizes"));
        Assert.Equal("avatar.png 1x, avatar@2x.png 2x", image.GetAttribute("srcset"));
    }

    [Fact(DisplayName = "shows the image when only srcSet is provided")]
    [Trait("Requirement", "AVA-API-02")]
    [Trait("Requirement", "AVA-STA-10")]
    [Trait("Requirement", "AVA-DOM-09")]
    public void Shows_the_image_when_only_srcSet_is_provided()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.Sizes, "48px").Add(x => x.SrcSet, "avatar.png 1x"));
        interop.ReportImage(cut, "loaded");

        IElement image = cut.Find("[data-testid=image]");
        Assert.Equal("avatar.png 1x", image.GetAttribute("srcset"));
        Assert.False(image.HasAttribute("src"));
        Assert.Empty(cut.FindAll("[data-testid=fallback]"));
    }

    [Fact(DisplayName = "passes responsive image props to the loading probe")]
    [Trait("Requirement", "AVA-API-02")]
    [Trait("Requirement", "AVA-API-03")]
    [Trait("Requirement", "AVA-ALG-01")]
    public void Passes_responsive_image_props_to_the_loading_probe()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p
            .Add(x => x.Sizes, "48px")
            .Add(x => x.Src, "fallback.png")
            .Add(x => x.SrcSet, "avatar.png 1x, avatar@2x.png 2x")
            .Add(x => x.CrossOrigin, "anonymous")
            .Add(x => x.ReferrerPolicy, "no-referrer"));

        // The probe is attached to the root's element: the image has no element before it loads.
        JSRuntimeInvocation attach = Assert.Single(interop.DomAttaches);
        Assert.Equal(cut.FindComponent<AvatarRoot>().Instance.Element, Assert.IsType<ElementReference>(attach.Arguments[0]));
        Assert.Equal(
            """{"liveRegion":false,"image":{"src":"fallback.png","srcSet":"avatar.png 1x, avatar@2x.png 2x","sizes":"48px","crossOrigin":"anonymous","referrerPolicy":"no-referrer","keepMounted":false}}""",
            attach.Arguments[1]);
    }

    [Fact(DisplayName = "fires when the image loads")]
    [Trait("Requirement", "AVA-EVT-01")]
    [Trait("Requirement", "AVA-STA-09")]
    [Trait("Requirement", "AVA-ALG-01")]
    [Trait("Requirement", "AVA-API-05")]
    public void Fires_when_the_image_loads()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.Src, "avatar.png"));

        interop.ReportImage(cut, "loading");
        Assert.Equal([ImageLoadingStatus.Loading], cut.Instance.Statuses);

        interop.ReportImage(cut, "loaded");
        Assert.Equal([ImageLoadingStatus.Loading, ImageLoadingStatus.Loaded], cut.Instance.Statuses);
    }

    [Fact(DisplayName = "fires when the image errors")]
    [Trait("Requirement", "AVA-EVT-01")]
    [Trait("Requirement", "AVA-STA-09")]
    [Trait("Requirement", "AVA-ALG-01")]
    public void Fires_when_the_image_errors()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.Src, "avatar.png"));

        interop.ReportImage(cut, "loading");
        interop.ReportImage(cut, "error");

        Assert.Equal([ImageLoadingStatus.Loading, ImageLoadingStatus.Error], cut.Instance.Statuses);
    }

    [Fact(DisplayName = "fires for cached image errors without emitting idle")]
    [Trait("Requirement", "AVA-EVT-02")]
    [Trait("Requirement", "AVA-STA-04")]
    [Trait("Requirement", "AVA-STA-10")]
    public void Fires_for_cached_image_errors_without_emitting_idle()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.Src, "avatar.png"));

        interop.ReportImage(cut, "error");

        Assert.Equal([ImageLoadingStatus.Error], cut.Instance.Statuses);
        Assert.DoesNotContain(ImageLoadingStatus.Idle, cut.Instance.Statuses);
    }

    [Fact(DisplayName = "mounts the image while loading without preloading it")]
    [Trait("Requirement", "AVA-API-04")]
    [Trait("Requirement", "AVA-STA-11")]
    [Trait("Requirement", "AVA-DOM-02")]
    public void Mounts_the_image_while_loading_without_preloading_it()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.KeepMounted, true).Add(x => x.Src, "avatar.png"));

        IElement image = cut.Find("[data-testid=image]");
        Assert.Equal("avatar.png", image.GetAttribute("src"));
        Assert.Equal("JD", cut.Find("[data-testid=fallback]").TextContent);

        // No probe: the module derives the status from the image element itself.
        JSRuntimeInvocation attach = Assert.Single(interop.DomAttaches);
        Assert.Equal(cut.FindComponent<AvatarImage>().Instance.Element, Assert.IsType<ElementReference>(attach.Arguments[0]));
        Assert.Contains("\"keepMounted\":true", (string)attach.Arguments[1]!, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "loads the image without a detached preload")]
    [Trait("Requirement", "AVA-STA-11")]
    [Trait("Requirement", "AVA-API-04")]
    public void Loads_the_image_without_a_detached_preload()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.KeepMounted, true).Add(x => x.Src, "avatar.png").Add(x => x.ImageAttributes, new Dictionary<string, object> { ["alt"] = "Jane Doe" }));

        // The derivation on attach finds the element loaded.
        interop.ReportImage(cut, "loaded");

        Assert.Empty(cut.FindAll("[data-testid=fallback]"));
        Assert.Equal("avatar.png", cut.Find("[data-testid=image]").GetAttribute("src"));
        Assert.All(interop.DomAttaches, attach => Assert.Contains("\"keepMounted\":true", (string)attach.Arguments[1]!, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "derives the status from the rendered element load event")]
    [Trait("Requirement", "AVA-STA-14")]
    [Trait("Requirement", "AVA-EVT-01")]
    public void Derives_the_status_from_the_rendered_element_load_event()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.KeepMounted, true).Add(x => x.Src, "avatar.png"));
        interop.ReportImage(cut, "loading");

        cut.Find("[data-testid=image]").TriggerEvent("onload", new ProgressEventArgs());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=fallback]")));
        Assert.Equal([ImageLoadingStatus.Loading, ImageLoadingStatus.Loaded], cut.Instance.Statuses);
    }

    [Fact(DisplayName = "keeps the image mounted when it fails to load")]
    [Trait("Requirement", "AVA-STA-14")]
    [Trait("Requirement", "AVA-DOM-02")]
    public void Keeps_the_image_mounted_when_it_fails_to_load()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.KeepMounted, true).Add(x => x.Src, "avatar.png"));
        interop.ReportImage(cut, "loading");

        cut.Find("[data-testid=image]").TriggerEvent("onerror", new ErrorEventArgs());

        Assert.Equal([ImageLoadingStatus.Loading, ImageLoadingStatus.Error], cut.Instance.Statuses);
        IElement image = cut.Find("[data-testid=image]");
        Assert.False(image.HasAttribute("hidden"));
        Assert.False(image.HasAttribute("style"));
        Assert.Equal("JD", cut.Find("[data-testid=fallback]").TextContent);
    }

    [Fact(DisplayName = "calls the user onError handler")]
    [Trait("Requirement", "AVA-STA-14")]
    public void Calls_the_user_onError_handler()
    {
        int calls = 0;
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p
            .Add(x => x.KeepMounted, true)
            .Add(x => x.Src, "avatar.png")
            .Add(x => x.ImageAttributes, new Dictionary<string, object> { ["onerror"] = EventCallback.Factory.Create<ErrorEventArgs>(this, () => calls++) }));

        cut.Find("[data-testid=image]").TriggerEvent("onerror", new ErrorEventArgs());

        Assert.Equal(1, calls);
        Assert.Equal("JD", cut.Find("[data-testid=fallback]").TextContent);
    }

    [Fact(DisplayName = "calls the user onLoad handler")]
    [Trait("Requirement", "AVA-STA-14")]
    public void Calls_the_user_onLoad_handler()
    {
        int calls = 0;
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p
            .Add(x => x.KeepMounted, true)
            .Add(x => x.Src, "avatar.png")
            .Add(x => x.ImageAttributes, new Dictionary<string, object> { ["onload"] = EventCallback.Factory.Create<ProgressEventArgs>(this, () => calls++) }));

        cut.Find("[data-testid=image]").TriggerEvent("onload", new ProgressEventArgs());

        Assert.Equal(1, calls);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=fallback]")));
    }

    [Fact(DisplayName = "lets a user handler prevent the status update")]
    [Trait("Requirement", "AVA-STA-14")]
    public void Lets_a_user_handler_prevent_the_status_update()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p
            .Add(x => x.KeepMounted, true)
            .Add(x => x.Src, "avatar.png")
            .Add(x => x.ImageAttributes, new Dictionary<string, object> { ["onload"] = EventCallback.Factory.Create<ProgressEventArgs>(this, e => e.PreventPartHandler()) }));
        interop.ReportImage(cut, "loading");

        cut.Find("[data-testid=image]").TriggerEvent("onload", new ProgressEventArgs());

        Assert.True(cut.Find("[data-testid=image]").HasAttribute("data-loading"));
        Assert.Equal("JD", cut.Find("[data-testid=fallback]").TextContent);
        Assert.Equal([ImageLoadingStatus.Loading], cut.Instance.Statuses);
    }

    [Fact(DisplayName = "resets the status when a rendered image changes source")]
    [Trait("Requirement", "AVA-STA-12")]
    public void Resets_the_status_when_a_rendered_image_changes_source()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.KeepMounted, true).Add(x => x.ImageRender, SubstituteImage("avatar-1.png")));
        cut.Find("[data-testid=image]").TriggerEvent("onload", new ProgressEventArgs());
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=fallback]")));
        cut.Instance.Statuses.Clear();

        cut.Render(p => p.Add(x => x.ImageRender, SubstituteImage("avatar-2.png")));

        // The module re-derives from the element when its source attributes change in a render (L3 for the observer).
        Assert.Equal("avatar-2.png", cut.Find("[data-testid=image]").GetAttribute("src"));
        interop.ReportImage(cut, "loading");
        Assert.Equal([ImageLoadingStatus.Loading], cut.Instance.Statuses);
        Assert.Equal("JD", cut.Find("[data-testid=fallback]").TextContent);

        // The reset status resolves again from the new load.
        cut.Find("[data-testid=image]").TriggerEvent("onload", new ProgressEventArgs());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=fallback]")));
        Assert.Equal([ImageLoadingStatus.Loading, ImageLoadingStatus.Loaded], cut.Instance.Statuses);
    }

    [Fact(DisplayName = "resets the status when the src prop changes")]
    [Trait("Requirement", "AVA-STA-12")]
    [Trait("Requirement", "AVA-STA-04")]
    public void Resets_the_status_when_the_src_prop_changes()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.KeepMounted, true).Add(x => x.Src, "avatar-1.png"));
        cut.Find("[data-testid=image]").TriggerEvent("onload", new ProgressEventArgs());
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=fallback]")));
        cut.Instance.Statuses.Clear();

        cut.Render(p => p.Add(x => x.Src, "avatar-2.png"));

        // The new source reaches the module, which derives `loading` (never `idle`) from the element.
        Assert.Contains("\"src\":\"avatar-2.png\"", (string)interop.DomUpdates[^1].Arguments[0]!, StringComparison.Ordinal);
        interop.ReportImage(cut, "loading");
        Assert.Equal([ImageLoadingStatus.Loading], cut.Instance.Statuses);
        Assert.Equal("JD", cut.Find("[data-testid=fallback]").TextContent);
    }

    [Fact(DisplayName = "resets the status when the source changes to an unloaded one")]
    [Trait("Requirement", "AVA-STA-12")]
    [Trait("Requirement", "AVA-ALG-02")]
    [Trait("Requirement", "AVA-DOM-04")]
    [Trait("Requirement", "AVA-EVT-02")]
    public void Resets_the_status_when_the_source_changes_to_an_unloaded_one()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.KeepMounted, true).Add(x => x.Src, "cached.png"));
        interop.ReportImage(cut, "loaded");
        Assert.Empty(cut.FindAll("[data-testid=fallback]"));

        cut.Render(p => p.Add(x => x.Src, "/missing-avatar.png"));
        interop.ReportImage(cut, "loading");
        Assert.True(cut.Find("[data-testid=image]").HasAttribute("data-loading"));

        cut.Find("[data-testid=image]").TriggerEvent("onerror", new ErrorEventArgs());

        Assert.True(cut.Find("[data-testid=image]").HasAttribute("data-error"));
        Assert.Equal([ImageLoadingStatus.Loaded, ImageLoadingStatus.Loading, ImageLoadingStatus.Error], cut.Instance.Statuses);
    }

    [Fact(DisplayName = "preserves loaded status when the render element changes")]
    [Trait("Requirement", "AVA-STA-12")]
    [Trait("Requirement", "AVA-EVT-01")]
    public void Preserves_loaded_status_when_the_render_element_changes()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.KeepMounted, true).Add(x => x.ImageRender, SubstituteImage("avatar.png", "initial")));
        interop.ReportImage(cut, "loaded");
        cut.Instance.Statuses.Clear();

        cut.Render(p => p.Add(x => x.ImageRender, SubstituteImage("avatar.png", "updated")));
        // A derivation after the render finds the same status: nothing is reported again.
        interop.ReportImage(cut, "loaded");

        Assert.Contains("updated", cut.Find("[data-testid=image]").ClassList);
        Assert.Empty(cut.Instance.Statuses);
    }

    [Fact(DisplayName = "reports an error when there is no source")]
    [Trait("Requirement", "AVA-ALG-02")]
    [Trait("Requirement", "AVA-EVT-02")]
    [Trait("Requirement", "AVA-STA-12")]
    public void Reports_an_error_when_there_is_no_source()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.KeepMounted, true));

        Assert.False(cut.Find("[data-testid=image]").HasAttribute("src"));
        interop.ReportImage(cut, "error");

        Assert.Equal([ImageLoadingStatus.Error], cut.Instance.Statuses);
        Assert.Equal("JD", cut.Find("[data-testid=fallback]").TextContent);
    }

    [Fact(DisplayName = "keeps the status reported by an element that does not forward a ref")]
    [Trait("Requirement", "AVA-STA-13")]
    [Trait("Requirement", "AVA-EVT-03")]
    public void Keeps_the_status_reported_by_an_element_that_does_not_forward_a_ref()
    {
        // ADR 02's relaxation of RND-DOM-20..24: under a substitute callback the part has no element, so nothing derives
        // the status from it and only the element's events report.
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p
            .Add(x => x.KeepMounted, true)
            .Add(x => x.Src, "avatar.png")
            .Add(x => x.ImageRenderFunction, CallbackImage("initial")));

        cut.Find("[data-testid=image]").TriggerEvent("onload", new ProgressEventArgs());
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=fallback]")));

        cut.Render(p => p.Add(x => x.ImageRenderFunction, CallbackImage("updated")));

        Assert.Empty(cut.FindAll("[data-testid=fallback]"));
        Assert.Equal([ImageLoadingStatus.Loaded], cut.Instance.Statuses);
        Assert.Empty(interop.DomAttaches);
    }

    [Fact(DisplayName = "does not apply data-ending-style with keepMounted")]
    [Trait("Requirement", "AVA-STA-07")]
    [Trait("Requirement", "AVA-DOM-03")]
    [Trait("Requirement", "AVA-ANI-05")]
    public void Does_not_apply_data_ending_style_with_keepMounted()
    {
        List<TransitionStatus?> seen = [];
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p
            .Add(x => x.KeepMounted, true)
            .Add(x => x.Src, "cached.png")
            .Add(x => x.ImageClassValue, (Func<AvatarImageState, string?>)(state => { seen.Add(state.TransitionStatus); return null; })));
        interop.ReportImage(cut, "loaded");

        JSRuntimeInvocation attach = Assert.Single(interop.TransitionsAttaches);
        Assert.Contains("\"suppressEndingStyle\":true", (string)attach.Arguments[1]!, StringComparison.Ordinal);

        cut.Render(p => p.Add(x => x.Src, "/missing-avatar.png"));
        interop.ReportImage(cut, "loading");
        // Even a report of the ending phase is the unset status in kept-mounted mode.
        interop.ReportTransition(cut, "ending", mounted: true);

        IElement image = cut.Find("[data-testid=image]");
        Assert.False(image.HasAttribute("data-ending-style"));
        Assert.True(image.HasAttribute("data-loading"));
        Assert.DoesNotContain(TransitionStatus.Ending, seen);
    }

    [Fact(DisplayName = "applies data-ending-style before unmount")]
    [Trait("Requirement", "AVA-DOM-01")]
    [Trait("Requirement", "AVA-DOM-03")]
    [Trait("Requirement", "AVA-ANI-03")]
    [Trait("Requirement", "AVA-STA-08")]
    public void Applies_data_ending_style_before_unmount()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.Src, "avatar.png"));
        interop.ReportImage(cut, "loaded");

        // The element appears with data-starting-style (rendered by C# under the handover) and its twin attached.
        Assert.True(cut.Find("[data-testid=image]").HasAttribute("data-starting-style"));
        JSRuntimeInvocation attach = Assert.Single(interop.TransitionsAttaches);
        Assert.Equal(
            """{"open":true,"mounted":true,"status":"starting","enableIdleState":false,"deferEndingState":false,"keepMounted":false,"hiddenUntilFound":false,"hiddenWhenUnmounted":false,"suppressEndingStyle":false,"reportOpenComplete":false,"revealOnBeforeMatch":false,"unmountRequests":0}""",
            attach.Arguments[1]);
        interop.ReportTransition(cut, null, mounted: true);

        // No source: the module reports `error` without a probe, and the close reaches the transitions module.
        cut.Render(p => p.Add(x => x.Src, null));
        interop.ReportImage(cut, "error");
        Assert.Contains("\"open\":false", (string)interop.TransitionsUpdates[^1].Arguments[0]!, StringComparison.Ordinal);
        interop.ReportTransition(cut, "ending", mounted: true);

        // The element stays until its exit animations finish.
        Assert.NotNull(cut.Find("[data-testid=image]"));

        interop.CompleteExit(cut);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=image]")));
    }

    [Fact(DisplayName = "marks the not-loaded states with data attributes")]
    [Trait("Requirement", "AVA-DOM-04")]
    public void Marks_the_not_loaded_states_with_data_attributes()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.KeepMounted, true).Add(x => x.Src, "avatar.png"));
        interop.ReportImage(cut, "loading");
        Assert.True(cut.Find("[data-testid=image]").HasAttribute("data-loading"));

        cut.Find("[data-testid=image]").TriggerEvent("onload", new ProgressEventArgs());

        Assert.False(cut.Find("[data-testid=image]").HasAttribute("data-loading"));
        Assert.False(cut.Find("[data-testid=image]").HasAttribute("data-error"));

        cut.Find("[data-testid=image]").TriggerEvent("onerror", new ErrorEventArgs());

        Assert.True(cut.Find("[data-testid=image]").HasAttribute("data-error"));
    }

    [Fact(DisplayName = "keeps the image hidden from assistive technology after an error")]
    [Trait("Requirement", "AVA-ARIA-01")]
    [Trait("Requirement", "AVA-DOM-04")]
    public void Keeps_the_image_hidden_from_assistive_technology_after_an_error()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.KeepMounted, true).Add(x => x.Src, "avatar.png").Add(x => x.ImageAttributes, new Dictionary<string, object> { ["alt"] = "Jane Doe" }));

        cut.Find("[data-testid=image]").TriggerEvent("onerror", new ErrorEventArgs());

        IElement image = cut.Find("[data-testid=image]");
        Assert.True(image.HasAttribute("data-error"));
        Assert.Equal("true", image.GetAttribute("aria-hidden"));
        Assert.Equal("JD", cut.Find("[data-testid=fallback]").TextContent);
    }

    [Fact(DisplayName = "does not apply the not-loaded state attributes without keepMounted")]
    [Trait("Requirement", "AVA-DOM-04")]
    public void Does_not_apply_the_not_loaded_state_attributes_without_keepMounted()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.Src, "avatar.png"));
        interop.ReportImage(cut, "loaded");

        cut.Render(p => p.Add(x => x.Src, null));
        interop.ReportImage(cut, "error");
        interop.ReportTransition(cut, "ending", mounted: true);

        // The image animates out without the kept-mounted status attributes.
        IElement image = cut.Find("[data-testid=image]");
        Assert.False(image.HasAttribute("data-error"));
        Assert.False(image.HasAttribute("data-loading"));
        Assert.False(image.HasAttribute("aria-hidden"));
    }

    [Fact(DisplayName = "does not override source props in a render callback")]
    [Trait("Requirement", "AVA-DOM-06")]
    public void Does_not_override_source_props_in_a_render_callback()
    {
        RenderFragment<RenderProps<AvatarImageState>> callback = props => builder =>
        {
            builder.OpenElement(0, "img");
            builder.AddAttribute(1, "alt", string.Empty);
            builder.AddAttribute(2, "data-testid", "image");
            builder.AddAttribute(3, "sizes", "48px");
            builder.AddAttribute(4, "src", "avatar.png");
            builder.AddAttribute(5, "srcset", "avatar.png 1x");
            builder.AddMultipleAttributes(6, props.Attributes);
            builder.CloseElement();
        };

        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.KeepMounted, true).Add(x => x.ImageRenderFunction, callback));

        IElement image = cut.Find("[data-testid=image]");
        Assert.Equal("48px", image.GetAttribute("sizes"));
        Assert.Equal("avatar.png", image.GetAttribute("src"));
        Assert.Equal("avatar.png 1x", image.GetAttribute("srcset"));
    }

    [Fact(DisplayName = "applies the source props after the ones configuring the request")]
    [Trait("Requirement", "AVA-DOM-06")]
    public void Applies_the_source_props_after_the_ones_configuring_the_request()
    {
        List<string> keys = [];
        RenderFragment<RenderProps<AvatarImageState>> callback = props => builder =>
        {
            keys.Clear();
            keys.AddRange(props.Attributes.Keys);
            builder.OpenElement(0, "img");
            builder.AddAttribute(1, "alt", string.Empty);
            builder.AddMultipleAttributes(2, props.Attributes);
            builder.CloseElement();
        };

        interop.Render(p => p
            .Add(x => x.KeepMounted, true)
            .Add(x => x.Src, "avatar.png")
            .Add(x => x.Sizes, "48px")
            .Add(x => x.SrcSet, "avatar.png 1x, avatar@2x.png 2x")
            .Add(x => x.ImageAttributes, new Dictionary<string, object> { ["loading"] = "lazy" })
            .Add(x => x.ImageRenderFunction, callback));

        Assert.True(keys.IndexOf("src") > keys.IndexOf("loading"));
        Assert.True(keys.IndexOf("src") > keys.IndexOf("sizes"));
        Assert.True(keys.IndexOf("src") > keys.IndexOf("srcset"));
        Assert.True(keys.IndexOf("loading") >= 0);
    }

    [Fact(DisplayName = "hides the image from assistive technology until it loads")]
    [Trait("Requirement", "AVA-ARIA-01")]
    [Trait("Requirement", "AVA-ARIA-02")]
    public void Hides_the_image_from_assistive_technology_until_it_loads()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.KeepMounted, true).Add(x => x.Src, "avatar.png").Add(x => x.ImageAttributes, new Dictionary<string, object> { ["alt"] = "Jane Doe" }));

        Assert.Equal("true", cut.Find("[data-testid=image]").GetAttribute("aria-hidden"));

        cut.Find("[data-testid=image]").TriggerEvent("onload", new ProgressEventArgs());

        IElement image = cut.Find("[data-testid=image]");
        Assert.False(image.HasAttribute("aria-hidden"));
        // The accessible name is the consumer's alt; the part adds no role and no other ARIA attribute.
        Assert.Equal("Jane Doe", image.GetAttribute("alt"));
        Assert.False(image.HasAttribute("role"));
        Assert.DoesNotContain(image.Attributes, attribute => attribute.Name.StartsWith("aria-", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "hides the image from assistive technology again when the source changes")]
    [Trait("Requirement", "AVA-ARIA-01")]
    [Trait("Requirement", "AVA-STA-12")]
    public void Hides_the_image_from_assistive_technology_again_when_the_source_changes()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.KeepMounted, true).Add(x => x.Src, "avatar-1.png"));
        cut.Find("[data-testid=image]").TriggerEvent("onload", new ProgressEventArgs());
        Assert.False(cut.Find("[data-testid=image]").HasAttribute("aria-hidden"));

        cut.Render(p => p.Add(x => x.Src, "avatar-2.png"));
        interop.ReportImage(cut, "loading");

        Assert.Equal("true", cut.Find("[data-testid=image]").GetAttribute("aria-hidden"));
    }

    [Fact(DisplayName = "preserves an explicitly provided aria-hidden value")]
    [Trait("Requirement", "AVA-ARIA-01")]
    public void Preserves_an_explicitly_provided_aria_hidden_value()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p
            .Add(x => x.KeepMounted, true)
            .Add(x => x.Src, "avatar.png")
            .Add(x => x.ImageAttributes, new Dictionary<string, object> { ["alt"] = "Jane Doe", ["aria-hidden"] = "false" }));

        Assert.Equal("false", cut.Find("[data-testid=image]").GetAttribute("aria-hidden"));

        cut.Find("[data-testid=image]").TriggerEvent("onload", new ProgressEventArgs());

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid=fallback]")));
        Assert.Equal("false", cut.Find("[data-testid=image]").GetAttribute("aria-hidden"));

        // The consumer's aria-hidden does not carry the open state, so the transitions module reads it from `update`.
        Assert.DoesNotContain("openSelector", (string)Assert.Single(interop.TransitionsAttaches).Arguments[1]!, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "renders the image in the server HTML and resolves cached images on hydration")]
    [Trait("Requirement", "AVA-EDGE-03")]
    [Trait("Requirement", "AVA-ARIA-01")]
    [Trait("Requirement", "AVA-DOM-02")]
    [Trait("Requirement", "AVA-ANI-04")]
    public void Renders_the_image_in_the_server_HTML_and_resolves_cached_images_on_hydration()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.KeepMounted, true).Add(x => x.Src, "cached.png").Add(x => x.ImageAttributes, new Dictionary<string, object> { ["alt"] = "Jane Doe" }));

        // Before any status is known (what a server render shows): the image with its source, hidden from assistive
        // technology, with no status attribute, and the fallback.
        IElement image = cut.Find("[data-testid=image]");
        Assert.Equal("cached.png", image.GetAttribute("src"));
        Assert.Equal("true", image.GetAttribute("aria-hidden"));
        Assert.False(image.HasAttribute("data-loading"));
        Assert.False(image.HasAttribute("data-error"));
        Assert.Equal("JD", cut.Find("[data-testid=fallback]").TextContent);
        Assert.Empty(interop.TransitionsAttaches);

        // The first derivation finds the image complete.
        interop.ReportImage(cut, "loaded");

        image = cut.Find("[data-testid=image]");
        Assert.False(image.HasAttribute("aria-hidden"));
        Assert.False(image.HasAttribute("data-starting-style"));
        Assert.Empty(cut.FindAll("[data-testid=fallback]"));
        // Attached as already mounted, so the transitions module plays no enter phase.
        Assert.Contains("\"open\":true,\"mounted\":true,\"enableIdleState\"", (string)Assert.Single(interop.TransitionsAttaches).Arguments[1]!, StringComparison.Ordinal);
    }

    // Port's own title: AVA-DOM-07 cites no Base UI test.
    [Fact(DisplayName = "renders an empty alt when the consumer supplies none")]
    [Trait("Requirement", "AVA-DOM-07")]
    public void Renders_an_empty_alt_when_the_consumer_supplies_none()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.KeepMounted, true).Add(x => x.Src, "avatar.png"));

        Assert.Equal(string.Empty, cut.Find("[data-testid=image]").GetAttribute("alt"));
    }

    // Port's own title: AVA-DOM-05 cites no test.
    [Fact(DisplayName = "projects no image loading status attribute on any part")]
    [Trait("Requirement", "AVA-DOM-05")]
    [Trait("Requirement", "AVA-DOM-08")]
    public void Projects_no_image_loading_status_attribute_on_any_part()
    {
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p.Add(x => x.KeepMounted, true).Add(x => x.Src, "avatar.png"));
        interop.ReportImage(cut, "error");

        Assert.DoesNotContain("imageloadingstatus", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("image-loading-status", cut.Markup, StringComparison.OrdinalIgnoreCase);
        // AVA-DOM-08: the root carries the consumer's attributes only.
        Assert.Equal(["data-testid"], cut.Find("[data-testid=root]").Attributes.Select(attribute => attribute.Name).Where(name => !name.StartsWith("blazor:", StringComparison.Ordinal)));
    }

    // Port's own title: AVA-EDGE-01 is untested in Base UI.
    [Fact(DisplayName = "throws when an image or a fallback is rendered outside a root")]
    [Trait("Requirement", "AVA-EDGE-01")]
    [Trait("Requirement", "AVA-STR-02")]
    public void Throws_when_an_image_or_a_fallback_is_rendered_outside_a_root()
    {
        InvalidOperationException image = Assert.Throws<InvalidOperationException>(() => interop.Context.Render<AvatarImage>());
        InvalidOperationException fallback = Assert.Throws<InvalidOperationException>(() => interop.Context.Render<AvatarFallback>());

        Assert.Equal("Blazix: `AvatarImage` must be placed within an `AvatarRoot`. The avatar parts read the image loading status from the root.", image.Message);
        Assert.StartsWith("Blazix: `AvatarFallback` must be placed within an `AvatarRoot`.", fallback.Message, StringComparison.Ordinal);
    }

    // Port's own title: AVA-EDGE-04 is untested in Base UI (§16 Q5).
    [Fact(DisplayName = "takes the latest report of several images and resets the root when one is removed")]
    [Trait("Requirement", "AVA-EDGE-04")]
    public void Takes_the_latest_report_of_several_images_and_resets_the_root_when_one_is_removed()
    {
        List<ImageLoadingStatus> rootStates = [];
        IRenderedComponent<AvatarHarness> cut = interop.Render(p => p
            .Add(x => x.Src, "avatar.png")
            .Add(x => x.ShowSecondImage, true)
            .Add(x => x.RootClassValue, (Func<AvatarRootState, string?>)(state => { rootStates.Add(state.ImageLoadingStatus); return null; })));
        DomTwin first = AvatarInterop.Twin<DomTwin>([interop.DomAttaches[0]]);
        DomTwin second = AvatarInterop.Twin<DomTwin>([interop.DomAttaches[1]]);

        cut.InvokeAsync(() => first.OnImageStatus("""{"status":"loaded"}"""));
        cut.InvokeAsync(() => second.OnImageStatus("""{"status":"error"}"""));
        Assert.Equal(ImageLoadingStatus.Error, rootStates[^1]);
        Assert.NotNull(cut.Find("[data-testid=fallback]"));

        cut.InvokeAsync(() => first.OnImageStatus("""{"status":"loading"}"""));
        cut.InvokeAsync(() => first.OnImageStatus("""{"status":"loaded"}"""));
        Assert.Equal(ImageLoadingStatus.Loaded, rootStates[^1]);

        cut.Render(p => p.Add(x => x.ShowSecondImage, false));

        Assert.Equal(ImageLoadingStatus.Idle, rootStates[^1]);
        Assert.NotNull(cut.Find("[data-testid=fallback]"));
    }

    private static RenderFragment SubstituteImage(string src, string? className = null) => builder =>
    {
        builder.OpenElement(0, "img");
        builder.AddAttribute(1, "alt", string.Empty);
        builder.AddAttribute(2, "data-testid", "image");
        builder.AddAttribute(3, "src", src);
        builder.AddAttribute(4, "class", className);
        builder.CloseElement();
    };

    private static RenderFragment<RenderProps<AvatarImageState>> CallbackImage(string className) => props => builder =>
    {
        builder.OpenElement(0, "img");
        builder.AddAttribute(1, "alt", string.Empty);
        builder.AddAttribute(2, "data-testid", "image");
        builder.AddMultipleAttributes(3, props.Attributes);
        builder.AddAttribute(4, "class", className);
        builder.CloseElement();
    };
}

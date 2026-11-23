using System.Text.Json;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components.Interop;

/// <summary>
/// Twin contract tests of the <c>dom</c> module (ADR 06 §1, L2): the options of its three C#-driven primitives
/// (<c>indeterminate</c>, the live-region marker, the image-load probe) as bUnit's JS interop mock records them, and
/// <see cref="DomTwin"/>'s <c>[JSInvokable]</c> callback.
/// </summary>
public sealed class DomTwinContractTests
{
    [Fact]
    [Trait("Requirement", "CHK-DOM-13")]
    [Trait("Requirement", "UTL-A11Y-01")]
    public async Task Attach_imports_the_dom_module_and_sends_the_primitives_it_turns_on()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule("./_content/Blazix.Primitives/js/dom.js");
        BunitJSModuleInterop handle = SetupHandle(module);
        await using DomTwin twin = new(context.JSInterop.JSRuntime);

        TwinHandle<DomOptions> attached = await twin.AttachAsync("base-ui-4", new DomOptions { Indeterminate = true });
        await attached.UpdateAsync(new DomOptions { Indeterminate = false, LiveRegion = true });
        await attached.DisposeAsync();

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Equal("base-ui-4", attach.Arguments[0]);
        Assert.Equal("""{"indeterminate":true,"liveRegion":false}""", attach.Arguments[1]);
        Assert.IsType<DotNetObjectReference<DomTwin>>(attach.Arguments[2]);

        JSRuntimeInvocation update = Assert.Single(handle.Invocations["update"]);
        Assert.Equal("""{"indeterminate":false,"liveRegion":true}""", update.Arguments[0]);
        Assert.Single(handle.Invocations["dispose"]);
    }

    [Fact]
    [Trait("Requirement", "AVA-ALG-01")]
    [Trait("Requirement", "AVA-API-03")]
    public void The_image_options_carry_every_source_property_the_probe_reads()
    {
        DomOptions options = new()
        {
            Image = new ImageLoadOptions
            {
                Src = "a.png",
                SrcSet = "a.png 1x",
                Sizes = "48px",
                CrossOrigin = "anonymous",
                ReferrerPolicy = "no-referrer",
                HideWhenLoaded = "base-ui-2",
            },
        };

        Assert.Equal(
            """{"liveRegion":false,"image":{"src":"a.png","srcSet":"a.png 1x","sizes":"48px","crossOrigin":"anonymous","referrerPolicy":"no-referrer","keepMounted":false,"hideWhenLoaded":"base-ui-2"}}""",
            JsonSerializer.Serialize(options, BlazixJsonContext.Default.DomOptions));
    }

    [Fact]
    public void An_options_record_without_primitives_sends_only_the_live_region_flag()
    {
        Assert.Equal("""{"liveRegion":false}""", JsonSerializer.Serialize(new DomOptions(), BlazixJsonContext.Default.DomOptions));
        Assert.Equal("""{"liveRegion":false,"image":{"keepMounted":true}}""", JsonSerializer.Serialize(new DomOptions { Image = new ImageLoadOptions { KeepMounted = true } }, BlazixJsonContext.Default.DomOptions));
    }

    [Fact]
    [Trait("Requirement", "SWT-DOM-08")]
    [Trait("Requirement", "SWT-EVT-04")]
    public void The_checked_options_carry_the_value_and_the_restore_count_only_once_raised()
    {
        // ADR 04 Amendments 2026-10-22 item 2: `checked` is the `dom` primitive's; a count C# raises makes an update that
        // leaves the value unchanged still reach the module (the UnmountRequests precedent).
        Assert.Equal("""{"checked":false,"liveRegion":false}""", JsonSerializer.Serialize(new DomOptions { Checked = false }, BlazixJsonContext.Default.DomOptions));
        Assert.Equal(
            """{"checked":true,"checkedRestores":2,"liveRegion":false}""",
            JsonSerializer.Serialize(new DomOptions { Checked = true, CheckedRestores = 2 }, BlazixJsonContext.Default.DomOptions));
        Assert.NotEqual(new DomOptions { Checked = true, CheckedRestores = 1 }, new DomOptions { Checked = true, CheckedRestores = 2 });
    }

    [Fact]
    [Trait("Requirement", "AVA-EVT-02")]
    public void OnImageStatus_raises_ImageStatusChanged_with_the_status_literal()
    {
        using BunitContext context = new();
        DomTwin twin = new(context.JSInterop.JSRuntime);
        List<string> reported = [];
        twin.ImageStatusChanged += reported.Add;

        twin.OnImageStatus("""{"status":"loading"}""");
        twin.OnImageStatus("""{"status":"loaded"}""");
        twin.OnImageStatus("""{"status":"error"}""");

        Assert.Equal(new[] { "loading", "loaded", "error" }, reported);
    }

    [Fact]
    public void The_dom_module_has_no_decision_point_and_answers_run()
    {
        using BunitContext context = new();
        DomTwin twin = new(context.JSInterop.JSRuntime);

        Assert.Equal("run", twin.Decide("activate", """{"kind":"Event","type":"base-ui"}"""));
    }

    [Fact]
    public void A_callback_without_a_subscriber_is_a_no_op_and_a_malformed_payload_is_rejected()
    {
        using BunitContext context = new();
        DomTwin twin = new(context.JSInterop.JSRuntime);

        twin.OnImageStatus("""{"status":"loaded"}""");
        Assert.Throws<JsonException>(() => twin.OnImageStatus("null"));
    }

    /// <summary>The module's <c>attach</c> answers with a handle whose <c>update</c> and <c>dispose</c> complete and are recorded.</summary>
    private static BunitJSModuleInterop SetupHandle(BunitJSModuleInterop module)
    {
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        return handle;
    }
}

using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components.Interop;

/// <summary>
/// Twin contract tests of the <c>scroll-area</c> module (ADR 06 §1, L2; ADR 04 Amendments 2026-11-18, the module as
/// built): bUnit's JS interop mock records every field C# sends on <c>attach</c> and <c>update</c>, and the tests drive
/// <see cref="ScrollAreaTwin"/>'s three <c>[JSInvokable]</c> reports.
/// </summary>
public sealed class ScrollAreaTwinContractTests
{
    private const string ModulePath = "./_content/Blazix.Primitives/js/scroll-area.js";

    [Fact]
    [Trait("Verifies", "ADR06-3")]
    [Trait("Requirement", "SCR-API-01")]
    [Trait("Requirement", "SCR-I18N-01")]
    public async Task Attach_imports_the_scroll_area_module_and_sends_every_field_on_attach_and_update()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(ModulePath);
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        await using ScrollAreaTwin twin = new(context.JSInterop.JSRuntime);

        TwinHandle<ScrollAreaOptions> attached = await twin.AttachAsync("base-ui-1", new ScrollAreaOptions
        {
            Style = "position: relative",
            Rtl = false,
            Thresholds = new ScrollAreaThresholdOptions(0, 0, 0, 0),
        });
        await attached.UpdateAsync(new ScrollAreaOptions
        {
            Part = ScrollAreaTwin.ThumbPart,
            RootRef = 0,
            Orientation = "horizontal",
            Style = "width: var(--scroll-area-thumb-width)",
        });
        await attached.DisposeAsync();

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Equal("base-ui-1", attach.Arguments[0]);
        Assert.Equal(
            """{"part":"root","rtl":false,"thresholds":{"xStart":0,"xEnd":0,"yStart":0,"yEnd":0},"style":"position: relative"}""",
            attach.Arguments[1]);
        Assert.IsType<DotNetObjectReference<ScrollAreaTwin>>(attach.Arguments[2]);
        Assert.Equal(
            """{"part":"thumb","rootRef":0,"orientation":"horizontal","style":"width: var(--scroll-area-thumb-width)"}""",
            Assert.Single(handle.Invocations["update"]).Arguments[0]);
        Assert.Single(handle.Invocations["dispose"]);
        Assert.Equal(["root", "viewport", "content", "scrollbar", "thumb", "corner"], [ScrollAreaTwin.RootPart, ScrollAreaTwin.ViewportPart, ScrollAreaTwin.ContentPart, ScrollAreaTwin.ScrollbarPart, ScrollAreaTwin.ThumbPart, ScrollAreaTwin.CornerPart]);
    }

    [Fact]
    [Trait("Requirement", "SCR-STA-06")]
    [Trait("Requirement", "SCR-STA-09")]
    [Trait("Requirement", "SCR-STA-11")]
    [Trait("Requirement", "SCR-STA-15")]
    public async Task The_three_reports_reach_their_events()
    {
        await using ScrollAreaTwin twin = new(new NoRuntime());
        List<ScrollAreaOverflowReport> overflow = [];
        List<ScrollAreaScrollingReport> scrolling = [];
        List<ScrollAreaHoverReport> hovering = [];
        twin.OverflowReported += overflow.Add;
        twin.ScrollingReported += scrolling.Add;
        twin.HoveringReported += hovering.Add;

        twin.OnOverflow("""{"hasOverflowX":false,"hasOverflowY":true,"xStart":false,"xEnd":false,"yStart":true,"yEnd":false,"measured":true}""");
        twin.OnScrolling("""{"x":false,"y":true}""");
        twin.OnHovering("""{"hovering":true}""");

        Assert.Equal(new ScrollAreaOverflowReport(false, true, false, false, true, false, true), Assert.Single(overflow));
        Assert.Equal(new ScrollAreaScrollingReport(false, true), Assert.Single(scrolling));
        Assert.True(Assert.Single(hovering).Hovering);
    }

    [Fact]
    [Trait("Requirement", "SCR-PTR-26")]
    public async Task The_module_asks_no_decision_point()
    {
        // ADR 04 Amendments 2026-11-18: ScrollArea has no decision point; any point the module could ask runs.
        await using ScrollAreaTwin twin = new(new NoRuntime());

        Assert.Equal("run", twin.Decide("press", """{"kind":"PointerEvent","type":"pointerdown","key":null,"code":null,"altKey":false,"ctrlKey":false,"metaKey":false,"shiftKey":false,"pointerType":"mouse","detail":0,"buttons":1,"target":null,"defaultPrevented":false,"timeStamp":1}"""));
    }

    private sealed class NoRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new NotSupportedException();
    }
}

using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components.Interop;

/// <summary>
/// Twin contract tests of the <c>positioning</c> module (ADR 06 §1, L2; ADR 03; ADR 04 Amendments 2026-10-27): bUnit's
/// JS interop mock records every field C# sends on <c>attach</c> and <c>update</c>, and the tests drive
/// <see cref="PositioningTwin"/>'s <c>[JSInvokable]</c> report. The module asks no decision point.
/// </summary>
public sealed class PositioningTwinContractTests
{
    private const string ModulePath = "./_content/Blazix.Primitives/js/positioning.js";

    [Fact]
    [Trait("Requirement", "FLT-API-17")]
    [Trait("Requirement", "FLT-POS-17")]
    public async Task Attach_imports_the_positioning_module_and_sends_every_field_on_attach_and_update()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(ModulePath);
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        await using PositioningTwin twin = new(context.JSInterop.JSRuntime);

        TwinHandle<PositioningOptions> attached = await twin.AttachAsync("base-ui-1", new PositioningOptions());

        // A repeated attach of the same element is a no-op (ADR 06 Amendments 2026-10-18 rule 2).
        Assert.Same(attached, await twin.AttachAsync("base-ui-1", new PositioningOptions { Sticky = true }));
        await attached.UpdateAsync(new PositioningOptions
        {
            Session = "base-ui-9",
            OpenSelector = "[data-open]",
            KeepMounted = true,
            TransitionStatus = "starting",
            TriggerId = "base-ui-2",
            AnchorId = "anchor",
            VirtualAnchor = new PositioningRect(1, 2, 3.5, 4),
            PositionMethod = "fixed",
            Side = "inline-end",
            Align = "start",
            SideOffset = 8,
            AlignOffset = -2.5,
            CollisionBoundary = "clipping-ancestors",
            BoundaryRect = new PositioningRect(0, 0, 300, 200),
            CollisionPadding = 12,
            CollisionPaddingSides = new PositioningPadding(1, 2, 3, 4),
            Sticky = true,
            ArrowPadding = 0,
            DisableAnchorTracking = true,
            CollisionAvoidance = new PositioningCollisionAvoidance("shift", "none", "start"),
            Shift = new PositioningShift(true, "layoutViewport"),
            LazyFlip = "placement",
            AdaptiveOrigin = true,
            Direction = "rtl",
            ArrowId = "base-ui-3",
            ArrowStyle = "background: red",
            Style = "color: blue",
        });
        await attached.DisposeAsync();
        await attached.DisposeAsync();

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Equal("base-ui-1", attach.Arguments[0]);
        Assert.Equal(
            """{"keepMounted":false,"positionMethod":"absolute","side":"bottom","align":"center","sideOffset":0,"alignOffset":0,"sticky":false,"arrowPadding":5,"disableAnchorTracking":false,"adaptiveOrigin":false,"direction":"ltr"}""",
            attach.Arguments[1]);
        Assert.IsType<DotNetObjectReference<PositioningTwin>>(attach.Arguments[2]);

        JSRuntimeInvocation update = Assert.Single(handle.Invocations["update"]);
        Assert.Equal(
            """{"session":"base-ui-9","openSelector":"[data-open]","keepMounted":true,"transitionStatus":"starting","triggerId":"base-ui-2","anchorId":"anchor","virtualAnchor":{"x":1,"y":2,"width":3.5,"height":4},"positionMethod":"fixed","side":"inline-end","align":"start","sideOffset":8,"alignOffset":-2.5,"collisionBoundary":"clipping-ancestors","boundaryRect":{"x":0,"y":0,"width":300,"height":200},"collisionPadding":12,"collisionPaddingSides":{"top":1,"right":2,"bottom":3,"left":4},"sticky":true,"arrowPadding":0,"disableAnchorTracking":true,"collisionAvoidance":{"side":"shift","align":"none","fallbackAxisSide":"start"},"shift":{"crossAxis":true,"rootBoundary":"layoutViewport"},"lazyFlip":"placement","adaptiveOrigin":true,"direction":"rtl","arrowId":"base-ui-3","arrowStyle":"background: red","style":"color: blue"}""",
            update.Arguments[0]);
        Assert.Single(handle.Invocations["dispose"]);
    }

    [Fact]
    [Trait("Requirement", "FLT-DOM-08")]
    [Trait("Requirement", "FLT-DOM-09")]
    [Trait("Requirement", "FLT-POS-09")]
    public async Task The_placement_report_raises_its_event_once_with_the_mirrored_values()
    {
        await using PositioningTwin twin = new(new NoRuntime());
        List<PositioningPlacement> reported = [];
        twin.PlacementChanged += reported.Add;

        twin.OnPlacementChange("""{"side":"inline-start","align":"end","anchorHidden":true,"uncentered":false}""");
        twin.OnPlacementChange("""{"side":"top","align":"center","anchorHidden":false,"uncentered":true}""");

        Assert.Equal(
            [new PositioningPlacement("inline-start", "end", true, false), new PositioningPlacement("top", "center", false, true)],
            reported);
    }

    [Fact]
    public async Task Every_decision_point_answers_run_because_the_module_asks_none()
    {
        await using PositioningTwin twin = new(new NoRuntime());

        Assert.Equal("run", twin.Decide("escape", """{"kind":"KeyboardEvent","type":"keydown","key":"Escape"}"""));
    }

    private sealed class NoRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new NotSupportedException();
    }
}

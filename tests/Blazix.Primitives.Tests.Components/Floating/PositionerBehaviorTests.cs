using System.Text.Json;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: the C# half of floating positioning on the test-only <see cref="TestPositioner"/> and <see cref="TestPositionerArrow"/>
/// (ADR 12 Amendments 2026-10-27 item 1): the markup rendered before the first positioning pass and frozen at attach
/// (FLT-DOM-01, FLT-DOM-02, FLT-DOM-05, FLT-ANI-05; ADR 04 Amendments 2026-10-18 and 2026-10-27), one <c>update</c> per
/// change, the mirror of the module's report and the parts it renders (FLT-DOM-08), the Arrow's id through the
/// positioner's context, the direction, the inputs as options, the element inputs as references, and the first attach under
/// a portal.
/// </summary>
public sealed class PositionerBehaviorTests : IDisposable
{
    private const string PrePosition = "position: fixed; top: 0px; left: 0px; --available-width: 100vw; --available-height: 100vh; opacity: 0;";

    private readonly PositioningTestContext host = new();

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact(DisplayName = "renders a presentation element with the requested placement and the pre-position style before the first pass")]
    [Trait("Requirement", "FLT-DOM-01")]
    [Trait("Requirement", "FLT-DOM-02")]
    [Trait("Requirement", "FLT-DOM-05")]
    [Trait("Requirement", "FLT-ANI-05")]
    public void Renders_the_pre_position_markup()
    {
        IRenderedComponent<TestPositioner> cut = host.Context.Render<TestPositioner>(p => p
            .Add(x => x.Side, Side.InlineEnd)
            .Add(x => x.Align, Align.Start)
            .Add(x => x.Open, false)
            .Add(x => x.TransitionStatus, TransitionStatus.Starting)
            .AddUnmatched("style", "color: red"));

        var element = cut.Find("div");
        Assert.Equal("presentation", element.GetAttribute("role"));
        Assert.Equal("inline-end", element.GetAttribute("data-side"));
        Assert.Equal("start", element.GetAttribute("data-align"));
        Assert.False(element.HasAttribute("hidden"));
        Assert.False(element.HasAttribute("data-anchor-hidden"));
        Assert.Equal($"{PrePosition} pointer-events: none; transition: none; color: red", element.GetAttribute("style"));

        // A kept-mounted positioner that is not mounted carries `hidden`; an open, settled one has no pointer-events or
        // transition declaration.
        cut.Render(p => p.Add(x => x.Mounted, false).Add(x => x.Open, true).Add(x => x.TransitionStatus, (TransitionStatus?)null).Add(x => x.KeepMounted, true));
        Assert.True(cut.Find("div").HasAttribute("hidden"));
    }

    [Fact(DisplayName = "freezes the style and the placement attributes at attach, and sends the consumer's later style in an update")]
    [Trait("Requirement", "FLT-DOM-01")]
    public void Freezes_the_handed_over_attributes_at_attach()
    {
        IRenderedComponent<TestPositioner> cut = host.Context.Render<TestPositioner>(p => p.Add(x => x.Side, Side.Top).AddUnmatched("style", "color: red"));
        string attached = cut.Find("div").GetAttribute("style")!;
        Assert.Equal($"{PrePosition} color: red", attached);
        Assert.Equal("color: red", PositioningTestContext.Parse(host.AttachOptions).GetProperty("style").GetString());

        cut.Render(p => p.Add(x => x.Side, Side.Bottom).Add(x => x.Open, false).AddUnmatched("style", "color: blue"));

        var element = cut.Find("div");
        Assert.Equal(attached, element.GetAttribute("style"));
        Assert.Equal("top", element.GetAttribute("data-side"));
        string update = Assert.Single(host.UpdateOptions);
        Assert.Equal("color: blue", PositioningTestContext.Parse(update).GetProperty("style").GetString());
        Assert.Equal("bottom", PositioningTestContext.Parse(update).GetProperty("side").GetString());
    }

    [Fact(DisplayName = "sends one update per change of the options and none for an equal re-render")]
    [Trait("Requirement", "FLT-POS-17")]
    public async Task Sends_one_update_per_change()
    {
        IRenderedComponent<TestPositioner> cut = host.Context.Render<TestPositioner>(p => p
            .Add(x => x.SideOffset, new Offset(AnchorWidth: 1) { Overrides = [new OffsetOverride(Side.Top, null, Constant: 4)] })
            .Add(x => x.CollisionBoundary, new CollisionBoundaryValue(new Rect(0, 0, 300, 200))));
        Assert.Single(host.Attaches);

        await cut.InvokeAsync(cut.Instance.Refresh);
        cut.Render(p => p
            .Add(x => x.SideOffset, new Offset(AnchorWidth: 1) { Overrides = [new OffsetOverride(Side.Top, null, Constant: 4)] })
            .Add(x => x.CollisionBoundary, new CollisionBoundaryValue(new Rect(0, 0, 300, 200))));
        Assert.Empty(host.UpdateOptions);

        cut.Render(p => p.Add(x => x.Align, Align.End));
        string update = Assert.Single(host.UpdateOptions);
        Assert.Equal("end", PositioningTestContext.Parse(update).GetProperty("align").GetString());
    }

    [Fact(DisplayName = "mirrors the report into the state, rendering only the parts whose mirrored values changed")]
    [Trait("Requirement", "FLT-DOM-08")]
    [Trait("Requirement", "FLT-DOM-09")]
    public async Task Mirrors_the_report_into_the_state()
    {
        List<TestPositionerState> positionerStates = [];
        List<TestPositionerArrowState> arrowStates = [];
        IRenderedComponent<TestPositioner> cut = host.Context.Render<TestPositioner>(p => p
            .Add(x => x.ClassValue, state =>
            {
                positionerStates.Add(state);
                return null;
            })
            .AddChildContent<TestPositionerArrow>(arrow => arrow.Add(x => x.ClassValue, state =>
            {
                arrowStates.Add(state);
                return null;
            })));
        int positionerRenders = positionerStates.Count;
        int arrowRenders = arrowStates.Count;
        Assert.Equal(new TestPositionerState(true, Side.Bottom, Align.Center, false), positionerStates[^1]);
        Assert.Equal(new TestPositionerArrowState(Side.Bottom, Align.Center, true), arrowStates[^1]);

        // Only uncentered changed: the Arrow renders, the Positioner does not.
        await cut.InvokeAsync(() => host.Twin.OnPlacementChange("""{"side":"bottom","align":"center","anchorHidden":false,"uncentered":false}"""));
        Assert.Equal(positionerRenders, positionerStates.Count);
        Assert.Equal(arrowRenders + 1, arrowStates.Count);
        Assert.False(arrowStates[^1].Uncentered);

        // A flip renders both once.
        await cut.InvokeAsync(() => host.Twin.OnPlacementChange("""{"side":"top","align":"center","anchorHidden":false,"uncentered":false}"""));
        Assert.Equal(positionerRenders + 1, positionerStates.Count);
        Assert.Equal(arrowRenders + 2, arrowStates.Count);
        Assert.Equal(Side.Top, positionerStates[^1].Side);
        Assert.Equal(Side.Top, arrowStates[^1].Side);

        // Only anchorHidden changed: the Positioner renders once. The Arrow's state is unchanged, so the mirror does not
        // render it, but its parent's render does: Blazor renders a child whose parameters include a delegate (here the
        // consumer's ClassValue) whenever the parent renders. An equal report renders nothing.
        await cut.InvokeAsync(() => host.Twin.OnPlacementChange("""{"side":"top","align":"center","anchorHidden":true,"uncentered":false}"""));
        await cut.InvokeAsync(() => host.Twin.OnPlacementChange("""{"side":"top","align":"center","anchorHidden":true,"uncentered":false}"""));
        Assert.Equal(positionerRenders + 2, positionerStates.Count);
        Assert.Equal(arrowRenders + 3, arrowStates.Count);
        Assert.True(positionerStates[^1].AnchorHidden);
        Assert.Equal(arrowStates[^2], arrowStates[^1]);

        // The attributes stay the module's: the mirror never writes data-side or data-anchor-hidden over it.
        Assert.Equal("bottom", cut.Find("[role=presentation]").GetAttribute("data-side"));
        Assert.False(cut.Find("[role=presentation]").HasAttribute("data-anchor-hidden"));
        Assert.Empty(host.UpdateOptions);
    }

    [Fact(DisplayName = "publishes the arrow's id and style through the positioner's context and freezes the arrow when they are sent")]
    [Trait("Requirement", "FLT-DOM-09")]
    [Trait("Requirement", "FLT-DOM-10")]
    public void Publishes_the_arrow_through_the_context()
    {
        IRenderedComponent<TestPositioner> cut = host.Context.Render<TestPositioner>(p => p
            .AddChildContent<TestPositionerArrow>(arrow => arrow.AddUnmatched("style", "background: red")));
        IRenderedComponent<TestPositionerArrow> arrow = cut.FindComponent<TestPositionerArrow>();
        var arrowElement = arrow.Find("div");
        string arrowId = arrowElement.Id!;

        JsonElement attach = PositioningTestContext.Parse(host.AttachOptions);
        Assert.Equal(arrowId, attach.GetProperty("arrowId").GetString());
        Assert.Equal("background: red", attach.GetProperty("arrowStyle").GetString());
        Assert.Equal("true", arrowElement.GetAttribute("aria-hidden"));
        Assert.Equal("position: absolute; background: red", arrowElement.GetAttribute("style"));
        Assert.True(arrowElement.HasAttribute("data-uncentered"));

        // Frozen from the attach that carried the id: a later style travels in the Positioner's update.
        arrow.Render(p => p.AddUnmatched("style", "background: blue"));
        Assert.Equal("position: absolute; background: red", arrow.Find("div").GetAttribute("style"));
        string update = Assert.Single(host.UpdateOptions);
        Assert.Equal("background: blue", PositioningTestContext.Parse(update).GetProperty("arrowStyle").GetString());

        // An Arrow that goes away is sent as gone, so the module returns to its stand-in.
        cut.Render(p => p.AddChildContent((RenderFragment)(_ => { })));
        Assert.Equal(2, host.UpdateOptions.Count);
        Assert.False(PositioningTestContext.Parse(host.UpdateOptions[^1]).TryGetProperty("arrowId", out _));
    }

    [Fact(DisplayName = "sends the reading direction of the nearest DirectionProvider and its changes")]
    [Trait("Requirement", "FLT-I18N-01")]
    public void Sends_the_direction_of_the_nearest_provider()
    {
        IRenderedComponent<DirectionProvider> cut = host.Context.Render<DirectionProvider>(p => p
            .Add(x => x.Direction, TextDirection.Rtl)
            .AddChildContent<TestPositioner>());
        Assert.Equal("rtl", PositioningTestContext.Parse(host.AttachOptions).GetProperty("direction").GetString());

        cut.Render(p => p.Add(x => x.Direction, TextDirection.Ltr));
        string update = Assert.Single(host.UpdateOptions);
        Assert.Equal("ltr", PositioningTestContext.Parse(update).GetProperty("direction").GetString());
    }

    [Fact(DisplayName = "sends every input as its option")]
    [Trait("Requirement", "FLT-API-08")]
    [Trait("Requirement", "FLT-API-09")]
    [Trait("Requirement", "FLT-API-10")]
    [Trait("Requirement", "FLT-API-12")]
    public void Sends_every_input_as_its_option()
    {
        host.Context.Render<TestPositioner>(p => p
            .Add(x => x.Anchor, new AnchorValue(new VirtualAnchor(1, 2, 3, 4)))
            .Add(x => x.PositionMethod, PositionMethod.Fixed)
            .Add(x => x.Side, Side.Left)
            .Add(x => x.SideOffset, new OffsetValue(new Offset(1, PositionerWidth: 0.5) { Overrides = [new OffsetOverride(Side.Right, Align.End, Constant: 9)] }))
            .Add(x => x.Align, Align.End)
            .Add(x => x.AlignOffset, new OffsetValue(-3))
            .Add(x => x.CollisionBoundary, CollisionBoundaryValue.ClippingAncestors)
            .Add(x => x.CollisionPadding, new CollisionPaddingValue(new PaddingSides(Top: 4, Left: 2)))
            .Add(x => x.Sticky, true)
            .Add(x => x.ArrowPadding, 0)
            .Add(x => x.DisableAnchorTracking, true)
            .Add(x => x.CollisionAvoidance, new CollisionAvoidance(CollisionAvoidanceMode.Shift, CollisionAvoidanceMode.None, FallbackAxisSide.Start))
            .Add(x => x.KeepMounted, true)
            .Add(x => x.TransitionStatus, TransitionStatus.Ending)
            .Add(x => x.AdaptiveOrigin, true)
            .Add(x => x.TriggerId, "base-ui-5")
            .Add(x => x.SessionToken, "base-ui-1"));

        Assert.Equal(
            """{"sessionToken":"base-ui-1","keepMounted":true,"transitionStatus":"ending","triggerId":"base-ui-5","virtualAnchor":{"x":1,"y":2,"width":3,"height":4},"positionMethod":"fixed","side":"left","align":"end","sideOffset":0,"sideOffsetFunction":{"constant":1,"anchorWidth":0,"anchorHeight":0,"positionerWidth":0.5,"positionerHeight":0,"overrides":[{"side":"right","align":"end","constant":9,"anchorWidth":0,"anchorHeight":0,"positionerWidth":0,"positionerHeight":0}]},"alignOffset":-3,"collisionBoundary":"clipping-ancestors","collisionPaddingSides":{"top":4,"right":0,"bottom":0,"left":2},"sticky":true,"arrowPadding":0,"disableAnchorTracking":true,"collisionAvoidance":{"side":"shift","align":"none","fallbackAxisSide":"start"},"adaptiveOrigin":true,"direction":"ltr"}""",
            host.AttachOptions);

        // The id cases: an anchor element's id, one padding for every side.
        host.Context.Render<TestPositioner>(p => p
            .Add(x => x.Anchor, new AnchorValue("anchor"))
            .Add(x => x.CollisionPadding, new CollisionPaddingValue(12)));
        JsonElement second = PositioningTestContext.Parse(host.AttachOptions);
        Assert.Equal("anchor", second.GetProperty("anchorId").GetString());
        Assert.Equal(12, second.GetProperty("collisionPadding").GetDouble());
    }

    [Fact(DisplayName = "sends the anchor element and the boundary elements as indexes into the references after the options, an update when an element at the same index changes, and no reference for an element not rendered yet")]
    [Trait("Requirement", "FLT-API-09")]
    [Trait("Requirement", "FLT-API-11")]
    public async Task Sends_the_element_inputs_as_references()
    {
        var anchor = new ElementReference("anchor-element");
        var first = new ElementReference("boundary-1");
        var second = new ElementReference("boundary-2");
        IRenderedComponent<TestPositioner> cut = host.Context.Render<TestPositioner>(p => p
            .Add(x => x.Anchor, new AnchorValue(anchor))
            .Add(x => x.CollisionBoundary, new CollisionBoundaryValue([first, second])));

        JsonElement attached = PositioningTestContext.Parse(host.AttachOptions);
        Assert.Equal(0, attached.GetProperty("anchorRef").GetInt32());
        Assert.Equal([1, 2], attached.GetProperty("boundaryRefs").EnumerateArray().Select(index => index.GetInt32()));
        Assert.Equal([anchor, first, second], Assert.IsType<object[]>(host.Attaches[^1].Arguments[3]));

        // Equal elements in a new list send nothing.
        await cut.InvokeAsync(cut.Instance.Refresh);
        cut.Render(p => p.Add(x => x.CollisionBoundary, new CollisionBoundaryValue(new List<ElementReference> { first, second })));
        Assert.Empty(host.UpdateOptions);

        // Another anchor element at the same index, and one boundary element.
        var other = new ElementReference("anchor-other");
        cut.Render(p => p.Add(x => x.Anchor, new AnchorValue(other)).Add(x => x.CollisionBoundary, new CollisionBoundaryValue(first)));
        JSRuntimeInvocation update = Assert.Single(host.Handle.Invocations["update"]);
        JsonElement updated = PositioningTestContext.Parse((string)update.Arguments[0]!);
        Assert.Equal(0, updated.GetProperty("anchorRef").GetInt32());
        Assert.Equal([1], updated.GetProperty("boundaryRefs").EnumerateArray().Select(index => index.GetInt32()));
        Assert.Equal([other, first], Assert.IsType<object[]>(update.Arguments[1]));

        // An element not rendered yet has no id to send: the trigger and the clipping ancestors apply.
        host.Context.Render<TestPositioner>(p => p
            .Add(x => x.Anchor, new AnchorValue(default(ElementReference)))
            .Add(x => x.CollisionBoundary, new CollisionBoundaryValue(default(ElementReference))));
        JsonElement unrendered = PositioningTestContext.Parse(host.AttachOptions);
        Assert.False(unrendered.TryGetProperty("anchorRef", out _));
        Assert.False(unrendered.TryGetProperty("boundaryRefs", out _));
        Assert.Equal(3, host.Attaches[^1].Arguments.Count);
    }

    [Fact(DisplayName = "attaches a positioner under a portal only once the portal has moved its node")]
    [Trait("Requirement", "FLT-DOM-02")]
    [Trait("Requirement", "FLT-STA-09")]
    public async Task Attaches_under_a_portal_once_moved()
    {
        var portal = new PopupPortalContext();
        IRenderedComponent<CascadingValue<PopupPortalContext>> cut = host.Context.Render<CascadingValue<PopupPortalContext>>(p => p
            .Add(x => x.Value, portal)
            .Add(x => x.IsFixed, true)
            .AddChildContent<TestPositioner>());
        TestPositioner positioner = cut.FindComponent<TestPositioner>().Instance;

        // Until the module has moved the node it is `display: none` in place, where nothing measures.
        await cut.InvokeAsync(positioner.Refresh);
        Assert.False(positioner.TwinAttached.IsCompleted);
        Assert.DoesNotContain(host.Context.JSInterop.Invocations, invocation => invocation.Identifier == "import");

        await cut.InvokeAsync(portal.CompleteMove);
        await positioner.TwinAttached.WaitAsync(TimeSpan.FromSeconds(5), Xunit.TestContext.Current.CancellationToken);
        Assert.Single(host.Attaches);
        Assert.Equal(PrePosition, cut.Find("div").GetAttribute("style"));
    }

    [Fact(DisplayName = "re-attaches when the element is removed and rendered again")]
    public void Reattaches_a_replaced_element()
    {
        IRenderedComponent<TestPositioner> cut = host.Context.Render<TestPositioner>();
        Assert.Single(host.Attaches);

        cut.Render(p => p.Add(x => x.Enabled, false));
        Assert.Equal(1, host.Disposes);

        cut.Render(p => p.Add(x => x.Enabled, true));
        Assert.Equal(2, host.Attaches.Count);
        Assert.Equal(PrePosition, cut.Find("div").GetAttribute("style"));
    }
}

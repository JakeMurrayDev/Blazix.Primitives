using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;

// Family tests use the project's root namespace (tests/Blazix.Primitives.Tests.Components/CONTEXT.md).
namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The <c>[L2]</c> titles of <c>DrawerSwipeArea.test.tsx</c> that need nothing of the <c>drawer</c> module (DRW-API-10,
/// DOM-30, PTR-32's registration), and the port titles of what C# renders on the area (DRW-API-08/09, DOM-25, DOM-27..29,
/// ARIA-01). The gesture titles are the module's (8b, L4).
/// </summary>
public sealed class DrawerSwipeAreaTests : IDisposable
{
    private readonly DialogTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    // Base UI's "works before a generated swipe area id is available" and "registers the swipe area once a generated id
    // becomes available" exercise React 17's useId fallback (RND-EDGE-09), which the port has no counterpart of: a generated
    // id exists at the first render, so the area renders it at once and registers under it.
    [Fact(DisplayName = "port: the swipe area renders a generated id at its first render and registers its element under it")]
    [Trait("Requirement", "DRW-API-10")]
    [Trait("Requirement", "DRW-DOM-30")]
    [Trait("Requirement", "DRW-PTR-32")]
    public void The_swipe_area_renders_a_generated_id_and_registers_under_it()
    {
        IRenderedComponent<DrawerSwipeAreaHarness> cut = test.Context.Render<DrawerSwipeAreaHarness>();

        IElement area = cut.Find("[data-testid=swipe-area]");
        Assert.False(string.IsNullOrEmpty(area.Id));

        DrawerSwipeArea part = cut.FindComponent<DrawerSwipeArea>().Instance;
        PopupTriggerElement? registered = Store(cut).Triggers.GetById(area.Id!);
        Assert.NotNull(registered);
        Assert.Same(part, registered.Value.Owner);
        Assert.Equal(part.Element, registered.Value.Element);
        Assert.Equal(1, Store(cut).Triggers.Count);
    }

    [Fact(DisplayName = "follows a swipe area id change")]
    [Trait("Requirement", "DRW-API-10")]
    [Trait("Requirement", "DRW-PTR-32")]
    public void Follows_a_swipe_area_id_change()
    {
        IRenderedComponent<DrawerSwipeAreaHarness> cut = test.Context.Render<DrawerSwipeAreaHarness>(p => p.Add(x => x.Id, "first"));
        DrawerSwipeArea part = cut.FindComponent<DrawerSwipeArea>().Instance;
        Assert.Same(part, Store(cut).Triggers.GetById("first")?.Owner);

        cut.Render(p => p.Add(x => x.Id, "second"));

        Assert.Null(Store(cut).Triggers.GetById("first"));
        Assert.Same(part, Store(cut).Triggers.GetById("second")?.Owner);
        Assert.Equal(1, Store(cut).Triggers.Count);
        Assert.Equal("second", cut.Find("[data-testid=swipe-area]").Id);
    }

    [Fact(DisplayName = "port: unmounting the swipe area unregisters it")]
    [Trait("Requirement", "DRW-API-10")]
    public void Unmounting_the_swipe_area_unregisters_it()
    {
        IRenderedComponent<DrawerSwipeAreaHarness> cut = test.Context.Render<DrawerSwipeAreaHarness>(p => p.Add(x => x.Id, "area"));
        Assert.NotNull(Store(cut).Triggers.GetById("area"));

        cut.Render(p => p.Add(x => x.Show, false));

        Assert.Null(Store(cut).Triggers.GetById("area"));
        Assert.Equal(0, Store(cut).Triggers.Count);
    }

    [Fact(DisplayName = "port: the swipe area is hidden from assistive technology and is not focusable")]
    [Trait("Requirement", "DRW-ARIA-01")]
    [Trait("Requirement", "DRW-STR-07")]
    public void The_swipe_area_is_hidden_from_assistive_technology()
    {
        IRenderedComponent<DrawerSwipeAreaHarness> cut = test.Context.Render<DrawerSwipeAreaHarness>();

        IElement area = cut.Find("[data-testid=swipe-area]");
        Assert.Equal("DIV", area.TagName);
        Assert.Equal("presentation", area.GetAttribute("role"));
        Assert.Equal("true", area.GetAttribute("aria-hidden"));
        Assert.False(area.HasAttribute("tabindex"));
    }

    [Fact(DisplayName = "port: the swipe area renders data-open while the drawer is open and data-closed otherwise")]
    [Trait("Requirement", "DRW-DOM-25")]
    public void The_swipe_area_renders_data_open_and_data_closed()
    {
        IRenderedComponent<DrawerSwipeAreaHarness> cut = test.Context.Render<DrawerSwipeAreaHarness>();
        IElement area = cut.Find("[data-testid=swipe-area]");
        Assert.True(area.HasAttribute(DrawerSwipeAreaDataAttributes.Closed));
        Assert.False(area.HasAttribute(DrawerSwipeAreaDataAttributes.Open));

        cut.Render(p => p.Add(x => x.Open, true));

        area = cut.Find("[data-testid=swipe-area]");
        Assert.True(area.HasAttribute(DrawerSwipeAreaDataAttributes.Open));
        Assert.False(area.HasAttribute(DrawerSwipeAreaDataAttributes.Closed));
    }

    // DRW-API-09: the opening direction is the area's own, else the opposite of the root's (down by default, so up).
    [Theory(DisplayName = "port: the swipe area renders its opening direction, the opposite of the root's unless it sets its own")]
    [InlineData(null, null, "up")]
    [InlineData(SwipeDirection.Up, null, "down")]
    [InlineData(SwipeDirection.Left, null, "right")]
    [InlineData(SwipeDirection.Right, null, "left")]
    [InlineData(SwipeDirection.Down, SwipeDirection.Left, "left")]
    [Trait("Requirement", "DRW-API-09")]
    [Trait("Requirement", "DRW-DOM-27")]
    public void The_swipe_area_renders_its_opening_direction(SwipeDirection? root, SwipeDirection? area, string expected)
    {
        IRenderedComponent<DrawerSwipeAreaHarness> cut = test.Context.Render<DrawerSwipeAreaHarness>(p => p
            .Add(x => x.RootDirection, root)
            .Add(x => x.AreaDirection, area));

        Assert.Equal(expected, cut.Find("[data-testid=swipe-area]").GetAttribute(DrawerSwipeAreaDataAttributes.SwipeDirection));
    }

    [Fact(DisplayName = "port: the swipe area renders data-disabled and no pointer events while disabled")]
    [Trait("Requirement", "DRW-API-08")]
    [Trait("Requirement", "DRW-DOM-28")]
    [Trait("Requirement", "DRW-DOM-29")]
    public void The_swipe_area_renders_data_disabled_while_disabled()
    {
        IRenderedComponent<DrawerSwipeAreaHarness> cut = test.Context.Render<DrawerSwipeAreaHarness>();
        IElement area = cut.Find("[data-testid=swipe-area]");
        Assert.False(area.HasAttribute(DrawerSwipeAreaDataAttributes.Disabled));
        Assert.Equal("touch-action: pan-x", area.GetAttribute("style"));

        cut.Render(p => p.Add(x => x.Disabled, true));

        area = cut.Find("[data-testid=swipe-area]");
        Assert.True(area.HasAttribute(DrawerSwipeAreaDataAttributes.Disabled));
        Assert.Equal("pointer-events: none; touch-action: pan-x", area.GetAttribute("style"));
    }

    // DRW-PTR-26: enabled iff not disabled and (closed or its own gesture active); the gesture is the module's (8b), so an open
    // drawer here disables the area.
    [Fact(DisplayName = "port: the swipe area takes no pointer events while the drawer is open, so it does not cover the popup")]
    [Trait("Requirement", "DRW-DOM-29")]
    public void The_swipe_area_takes_no_pointer_events_while_the_drawer_is_open()
    {
        IRenderedComponent<DrawerSwipeAreaHarness> cut = test.Context.Render<DrawerSwipeAreaHarness>(p => p.Add(x => x.Open, true));

        Assert.Equal("pointer-events: none; touch-action: pan-x", cut.Find("[data-testid=swipe-area]").GetAttribute("style"));
    }

    [Theory(DisplayName = "port: the swipe area's touch-action pans across its axis, before the consumer's style")]
    [InlineData(SwipeDirection.Left, "touch-action: pan-y; color: green")]
    [InlineData(SwipeDirection.Right, "touch-action: pan-y; color: green")]
    [InlineData(SwipeDirection.Up, "touch-action: pan-x; color: green")]
    [InlineData(SwipeDirection.Down, "touch-action: pan-x; color: green")]
    [Trait("Requirement", "DRW-DOM-29")]
    public void The_swipe_areas_touch_action_pans_across_its_axis(SwipeDirection area, string expected)
    {
        IRenderedComponent<DrawerSwipeAreaHarness> cut = test.Context.Render<DrawerSwipeAreaHarness>(p => p
            .Add(x => x.AreaDirection, area)
            .Add(x => x.Style, "color: green"));

        Assert.Equal(expected, cut.Find("[data-testid=swipe-area]").GetAttribute("style"));
    }

    [Fact(DisplayName = "port: the swipe area's state carries the open state, the opening direction and the disabled flag")]
    [Trait("Requirement", "DRW-STA-01")]
    public void The_swipe_areas_state_carries_its_fields()
    {
        DrawerSwipeAreaState? seen = null;
        IRenderedComponent<IComponent> cut = test.Context.Render(builder =>
        {
            builder.OpenComponent<DrawerRoot<NoPayload>>(0);
            builder.AddComponentParameter(1, nameof(DrawerRoot<NoPayload>.Open), true);
            builder.AddComponentParameter(2, nameof(DrawerRoot<NoPayload>.SwipeDirection), (SwipeDirection?)SwipeDirection.Right);
            builder.AddComponentParameter(3, nameof(DrawerRoot<NoPayload>.ChildContent), (RenderFragment)(root =>
            {
                root.OpenComponent<DrawerSwipeArea>(0);
                root.AddComponentParameter(1, nameof(DrawerSwipeArea.Disabled), true);
                root.AddComponentParameter(2, nameof(DrawerSwipeArea.ClassValue), (Func<DrawerSwipeAreaState, string?>)(state =>
                {
                    seen = state;
                    return null;
                }));
                root.CloseComponent();
            }));
            builder.CloseComponent();
        });

        Assert.Equal(new DrawerSwipeAreaState(true, false, SwipeDirection.Left, true), seen);
    }

    private static DialogStore<NoPayload> Store(IRenderedComponent<DrawerSwipeAreaHarness> cut) =>
        cut.FindComponent<DrawerRoot<NoPayload>>().Instance.Store;
}

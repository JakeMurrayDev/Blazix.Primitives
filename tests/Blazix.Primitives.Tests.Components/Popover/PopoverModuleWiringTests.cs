using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: the C# half of the <c>popup</c> module's roles in Popover (ADR 04 Amendments 2026-10-27; design points 5-8 of
/// M4 track 5, pending the user): what the Viewport, the Positioner and the Popup send, and what their reports change.
/// </summary>
public sealed class PopoverModuleWiringTests : IDisposable
{
    private readonly PopoverTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: the viewport mirrors the module's direction and transitioning flag in its state")]
    [Trait("Requirement", "POP-DOM-38")]
    [Trait("Requirement", "POP-DOM-39")]
    [Trait("Requirement", "POP-STA-13")]
    public async Task The_viewport_mirrors_the_module_direction_and_transitioning_flag()
    {
        List<PopoverViewportState> states = [];
        IRenderedComponent<PopupTestHost> cut = test.Render((builder, _) =>
        {
            builder.OpenComponent<PopoverRoot<NoPayload>>(0);
            builder.AddComponentParameter(1, nameof(PopoverRoot<NoPayload>.DefaultOpen), true);
            builder.AddComponentParameter(2, nameof(PopoverRoot<NoPayload>.ChildContent), (RenderFragment)(b =>
            {
                b.OpenComponent<PopoverPortal>(0);
                b.AddComponentParameter(1, nameof(PopoverPortal.ChildContent), (RenderFragment)(p =>
                {
                    p.OpenComponent<PopoverPositioner>(0);
                    p.AddComponentParameter(1, nameof(PopoverPositioner.ChildContent), (RenderFragment)(q =>
                    {
                        q.OpenComponent<PopoverPopup>(0);
                        q.AddComponentParameter(1, nameof(PopoverPopup.ChildContent), (RenderFragment)(r =>
                        {
                            r.OpenComponent<PopoverViewport>(0);
                            r.AddComponentParameter(1, "data-testid", "viewport");
                            r.AddComponentParameter(2, nameof(PopoverViewport.ClassValue), (Func<PopoverViewportState, string?>)(state =>
                            {
                                states.Add(state);
                                return null;
                            }));
                            r.CloseComponent();
                        }));
                        q.CloseComponent();
                    }));
                    p.CloseComponent();
                }));
                b.CloseComponent();
            }));
            builder.CloseComponent();
        });

        string? options = test.LastPopupOptions("viewport");
        Assert.NotNull(options);
        Assert.Contains("\"open\":true", options, StringComparison.Ordinal);
        Assert.Contains("\"popupId\":", options, StringComparison.Ordinal);

        await cut.InvokeAsync(() => test.PopupTwinOf("viewport").OnViewportChange("""{"activationDirection":"right down","transitioning":true}"""));

        Assert.Equal(new PopoverViewportState("right down", true, null), states[^1]);
        // JavaScript writes the two attributes: C# never renders them.
        IElement viewport = cut.Find("[data-testid=\"viewport\"]");
        Assert.False(viewport.HasAttribute("data-activation-direction"));
        Assert.False(viewport.HasAttribute("data-transitioning"));
        Assert.Single(viewport.Children);
        Assert.True(viewport.Children[0].HasAttribute("data-current"));
    }

    [Fact(DisplayName = "port: the trigger-change report writes the instant on the positioner, the popup and the viewport while open")]
    [Trait("Requirement", "POP-ANI-07")]
    [Trait("Requirement", "POP-DOM-18")]
    [Trait("Requirement", "POP-DOM-28")]
    [Trait("Requirement", "POP-DOM-40")]
    public async Task The_trigger_change_report_writes_the_instant_while_open()
    {
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec { Triggers = 2, Viewport = true });

        await test.PressAsync(cut, "trigger-1", open: true);
        await test.PressAsync(cut, "trigger-2", open: true);
        await cut.InvokeAsync(() => test.PopupTwinOf("positioner").OnTriggerChangeSettled("{}"));

        Assert.Equal("trigger-change", cut.Find("[data-testid=\"positioner\"]").GetAttribute("data-instant"));
        Assert.Equal("trigger-change", cut.Find("[data-testid=\"popup\"]").GetAttribute("data-instant"));
        Assert.Equal("trigger-change", cut.Find("[data-testid=\"viewport\"]").GetAttribute("data-instant"));

        // A move clears it while it animates; a close never shows it.
        await test.PressAsync(cut, "trigger-1", open: true);
        Assert.False(cut.Find("[data-testid=\"popup\"]").HasAttribute("data-instant"));
        await test.PressAsync(cut, "trigger-1", open: false);
        await cut.InvokeAsync(() => test.PopupTwinOf("positioner").OnTriggerChangeSettled("{}"));
        Assert.False(cut.Find("[data-testid=\"popup\"]").HasAttribute("data-instant"));
    }

    [Fact(DisplayName = "port: a keyboard press that moves the popover to another trigger clears the instant type in the render that applies it")]
    [Trait("Requirement", "POP-ANI-07")]
    [Trait("Requirement", "POP-DOM-28")]
    public async Task A_move_between_triggers_clears_the_instant_type_at_commit()
    {
        List<PopoverInstantType?> renders = [];
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec
        {
            Triggers = 2,
            PopupClass = state =>
            {
                renders.Add(state.Instant);
                return null;
            },
        });

        // A keyboard press (detail 0) opens with the `click` instant type (POP-STA-07).
        await test.PressAsync(cut, "trigger-1", open: true, detail: 0);
        Assert.Equal(PopoverInstantType.Click, renders[^1]);
        renders.Clear();

        await test.PressAsync(cut, "trigger-2", open: true, detail: 0);

        // useTriggerSwitchTransition clears it in the commit of the move; no render shows `click` on the new trigger.
        Assert.NotEmpty(renders);
        Assert.All(renders, Assert.Null);
    }

    [Fact(DisplayName = "port: a controlled trigger id that moves the open popover clears the instant type in the render that applies it")]
    [Trait("Requirement", "POP-ANI-07")]
    [Trait("Requirement", "POP-DOM-28")]
    public async Task A_controlled_move_between_triggers_clears_the_instant_type_at_commit()
    {
        List<PopoverInstantType?> renders = [];
        IRenderedComponent<PopupTestHost> cut = test.Render(
            (builder, host) => PopoverTree.Build(builder, new PopoverSpec
            {
                Open = true,
                TriggerIdSupplied = true,
                TriggerId = host.Get<string>("triggerId"),
                Triggers = 2,
                PopupClass = state =>
                {
                    renders.Add(state.Instant);
                    return null;
                },
            }),
            new() { ["triggerId"] = "trigger-1" });
        await cut.InvokeAsync(() => test.PopupTwinOf("positioner").OnTriggerChangeSettled("{}"));
        Assert.Equal(PopoverInstantType.TriggerChange, renders[^1]);
        renders.Clear();

        await cut.Instance.SetAsync("triggerId", "trigger-2");

        Assert.NotEmpty(renders);
        Assert.All(renders, Assert.Null);
    }

    [Theory(DisplayName = "port: the positioner sends the cut-out and the anchored lock for a modal press, not for a hover, and the touch flag")]
    [InlineData("press", "mouse", true, false)]
    [InlineData("press", "touch", true, true)]
    [InlineData("hover", "mouse", false, false)]
    [Trait("Requirement", "POP-STR-13")]
    [Trait("Requirement", "POP-PTR-19")]
    public async Task The_positioner_sends_the_cutout_and_the_anchored_lock(string how, string pointerType, bool modal, bool touch)
    {
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec { Modal = true, OpenOnHover = true });

        if (how == "press")
        {
            await test.PressAsync(cut, "trigger-1", open: true, pointerType);
        }
        else
        {
            await test.HoverAsync(cut, "trigger-1", open: true);
        }

        string options = test.LastPopupOptions("positioner")!;
        Assert.Contains($"\"cutout\":{(modal ? "true" : "false")}", options, StringComparison.Ordinal);
        Assert.Contains($"\"scrollLock\":{(modal ? "true" : "false")}", options, StringComparison.Ordinal);
        Assert.Contains($"\"touchOpen\":{(touch ? "true" : "false")}", options, StringComparison.Ordinal);
        Assert.Contains("\"activeTriggerId\":\"trigger-1\"", options, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: inside a toolbar the popup asks the module to stop the composite keys, outside it does not")]
    [Trait("Requirement", "POP-KEY-12")]
    public void Inside_a_toolbar_the_popup_asks_the_module_to_stop_the_composite_keys()
    {
        IRenderedComponent<PopupTestHost> outside = Render(new PopoverSpec { DefaultOpen = true });
        Assert.Equal("""{"popup":{"stopCompositeKeys":false}}""", test.LastPopupOptions("popup"));
        outside.Dispose();

        test.Render((builder, _) =>
        {
            builder.OpenComponent<ToolbarRoot>(0);
            builder.AddComponentParameter(1, nameof(ToolbarRoot.ChildContent), (RenderFragment)(b => PopoverTree.Build(b, new PopoverSpec { DefaultOpen = true })));
            builder.CloseComponent();
        });

        Assert.Equal("""{"popup":{"stopCompositeKeys":true}}""", test.LastPopupOptions("popup"));
    }

    private IRenderedComponent<PopupTestHost> Render(PopoverSpec spec) => test.Render((builder, _) => PopoverTree.Build(builder, spec));
}

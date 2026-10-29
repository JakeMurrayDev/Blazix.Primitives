using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2: the C# half of the <c>popup</c> module's roles in Popover (ADR 04 Amendments 2026-10-27; design points 5-8 of
/// M4 track 5, settled by the orchestrator 2026-10-28): what the Viewport, the Positioner and the Popup send, and what
/// their reports change.
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
        // Design point 13.13: the module reads the active trigger from the interactions session of this token.
        Assert.Contains("\"sessionToken\":\"base-ui-", options, StringComparison.Ordinal);

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

    [Fact(DisplayName = "port: the popup's transitions handle asks for transition: none while starting")]
    [Trait("Requirement", "POP-DOM-29")]
    public void The_popups_transitions_handle_asks_for_transition_none_while_starting()
    {
        // TRN-DOM-05 (ADR 04 Amendments 2026-10-28): the module writes it from the handover on, on the starting frame.
        BunitJSModuleInterop transitions = test.Context.JSInterop.SetupModule("./_content/Blazix.Primitives/js/transitions.js");
        transitions.SetupModule("attach", _ => true);

        Render(new PopoverSpec { DefaultOpen = true });

        Assert.Contains(
            transitions.Invocations["attach"],
            attach => attach.Arguments[1] is string json && json.Contains("\"disableTransitionWhileStarting\":true", StringComparison.Ordinal));
    }

    // Issue 147 as chosen by the user 2026-10-29 ((B1) with issue 148, ADR 04 Amendments 2026-10-29): the
    // PreventUnmountOnClose() call announces the retained close through the transitions module's export, addressed by the
    // element, ahead of any render the consumer's callback makes, a controlled close that re-renders and awaits included;
    // a close the callback then cancels sends the reset.
    [Fact(DisplayName = "port: the PreventUnmountOnClose() call announces the retained close to the transitions module before any render")]
    [Trait("Requirement", "POP-ANI-06")]
    public async Task The_call_announces_the_retained_close_before_any_render()
    {
        List<bool> announcedAtCall = [];
        IRenderedComponent<PopupTestHost>? cut = null;
        cut = test.Render(
            (builder, host) => PopoverTree.Build(builder, new PopoverSpec
            {
                Open = host.Get<bool>("open"),
                OnOpenChange = EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(host, async args =>
                {
                    if (!args.Value)
                    {
                        ((OpenChangeEventDetails)args.Details).PreventUnmountOnClose();
                        announcedAtCall.Add(RetainExitAnnouncements().SequenceEqual([true]) && cut!.Find("[data-testid=\"popup\"]").HasAttribute("data-open"));
                    }

                    // A controlled consumer that re-renders and then awaits inside its callback.
                    await host.SetAsync("open", args.Value);
                    await Task.Yield();
                }),
            }),
            new() { ["open"] = false });

        await test.PressAsync(cut, "trigger-1", open: true);
        cut.WaitForAssertion(() => Assert.Contains(test.Context.JSInterop.Invocations, invocation => invocation.Identifier == "attach" && invocation.Arguments.Count > 1 && invocation.Arguments[1] is string json && json.Contains("\"openSelector\"", StringComparison.Ordinal)));
        await test.PressAsync(cut, "trigger-1", open: false);

        Assert.Equal([true], announcedAtCall);
        Assert.Equal([true], RetainExitAnnouncements());
    }

    [Fact(DisplayName = "port: a retained close the callback then cancels sends the announcement's reset")]
    [Trait("Requirement", "POP-ANI-06")]
    public async Task A_canceled_retained_close_sends_the_reset()
    {
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec
        {
            OnOpenChange = EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, args =>
            {
                if (!args.Value)
                {
                    ((OpenChangeEventDetails)args.Details).PreventUnmountOnClose();
                    args.Details.Cancel();
                }
            }),
        });

        await test.PressAsync(cut, "trigger-1", open: true);
        cut.WaitForAssertion(() => Assert.Contains(test.Context.JSInterop.Invocations, invocation => invocation.Identifier == "attach" && invocation.Arguments.Count > 1 && invocation.Arguments[1] is string json && json.Contains("\"openSelector\"", StringComparison.Ordinal)));
        await test.PressAsync(cut, "trigger-1", open: false);

        Assert.Equal([true, false], RetainExitAnnouncements());
    }

    // Issue 149's residual (its comment of 2026-10-29): the popup creates its transitions twin after the portal's move, which
    // on Server can complete after the part was disposed; a disposed popup creates none, so no twin outlives it.
    [Fact(DisplayName = "port: a popup disposed while its portal's move is pending creates no transitions twin")]
    [Trait("Requirement", "TRN-ALG-01")]
    public async Task A_popup_disposed_while_its_portals_move_is_pending_creates_no_transitions_twin()
    {
        HeldAttachRuntime runtime = new(test.Context.JSInterop.JSRuntime, "portal.js");
        test.Context.Services.AddSingleton<IJSRuntime>(runtime);
        Render(new PopoverSpec { DefaultOpen = true });
        int before = runtime.Imports.Count(path => path.EndsWith("/transitions.js", StringComparison.Ordinal));

        await test.Context.DisposeComponentsAsync();

        // The held runtime is a test double, not interop: the analyzer reads its members as JS interop calls.
#pragma warning disable BL0016
        runtime.Release();
#pragma warning restore BL0016
        await Task.Delay(100, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(before, runtime.Imports.Count(path => path.EndsWith("/transitions.js", StringComparison.Ordinal)));
    }

    // Issue 147 ((B1), as built, ADR 04 Amendments 2026-10-29): the popup reads its first attach's options once the
    // transitions module is imported, right before the attach is sent, so a close the store retained while the import was in
    // flight rides in the attach itself.
    [Fact(DisplayName = "port: a close retained while the transitions module is imported rides in the popup's first attach")]
    [Trait("Requirement", "POP-ANI-06")]
    public async Task A_close_retained_while_the_module_is_imported_rides_in_the_first_attach()
    {
        HeldAttachRuntime runtime = new(test.Context.JSInterop.JSRuntime, "transitions.js", holdImport: true);
        test.Context.Services.AddSingleton<IJSRuntime>(runtime);
        bool closed = false;
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec
        {
            OnOpenChange = EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, args =>
            {
                if (!args.Value)
                {
                    ((OpenChangeEventDetails)args.Details).PreventUnmountOnClose();
                    closed = true;
                }
            }),
        });

        Task opening = test.PressAsync(cut, "trigger-1", open: true);
        cut.WaitForElement("[data-testid=\"popup\"][data-open]");
        Task closing = test.PressAsync(cut, "trigger-1", open: false);
        cut.WaitForAssertion(() => Assert.True(closed));

        // The held runtime is a test double, not interop: the analyzer reads its members as JS interop calls.
#pragma warning disable BL0016
        runtime.Release();
#pragma warning restore BL0016
        await Task.WhenAll(opening, closing);

        // No render follows the attach, and bUnit's waits check on renders: the record is polled.
        for (int poll = 0; poll < 200 && PopupAttachOptions() is null; poll++)
        {
            await Task.Delay(10, Xunit.TestContext.Current.CancellationToken);
        }

        string attach = Assert.IsType<string>(PopupAttachOptions());
        Assert.Contains("\"open\":false", attach, StringComparison.Ordinal);
        Assert.Contains("\"retainExit\":true", attach, StringComparison.Ordinal);
    }

    // Issue 147 ((B1)) for the backdrop: its own transitions handle keeps a retained close's ending phase as the popup's does,
    // so it announces the call through the export and carries the store's value in its options.
    [Fact(DisplayName = "port: the backdrop announces a retained close and carries it in its transitions options")]
    [Trait("Requirement", "POP-ANI-06")]
    public async Task The_backdrop_announces_a_retained_close_and_carries_it_in_its_options()
    {
        IRenderedComponent<PopupTestHost> cut = Render(new PopoverSpec
        {
            Backdrop = true,
            OnOpenChange = EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, args =>
            {
                if (!args.Value)
                {
                    ((OpenChangeEventDetails)args.Details).PreventUnmountOnClose();
                }
            }),
        });

        await test.PressAsync(cut, "trigger-1", open: true);
        cut.WaitForAssertion(() => Assert.NotNull(LastBackdropTransitionsOptions()));
        string backdrop = test.Context.JSInterop.Invocations
            .Where(invocation => invocation.Identifier == "attach" && invocation.Arguments.Count > 1 && invocation.Arguments[1] is string json && json.Contains("\"reportOpenComplete\":false", StringComparison.Ordinal))
            .Select(invocation => ((ElementReference)invocation.Arguments[0]!).Id)
            .Single();
        await test.PressAsync(cut, "trigger-1", open: false);

        cut.WaitForAssertion(() => Assert.Contains("\"retainExit\":true", LastBackdropTransitionsOptions(), StringComparison.Ordinal));
        Assert.Contains(
            test.Context.JSInterop.Invocations,
            invocation => invocation.Identifier == "retainExit" && invocation.Arguments[0] is ElementReference element && element.Id == backdrop && (bool)invocation.Arguments[1]!);
    }

    // Issue 149's residual for the backdrop: inside the portal it creates its transitions twin after the move, as the popup
    // does, and a backdrop disposed while the move is pending creates none.
    [Fact(DisplayName = "port: a backdrop disposed while its portal's move is pending creates no transitions twin")]
    [Trait("Requirement", "TRN-ALG-01")]
    public async Task A_backdrop_disposed_while_its_portals_move_is_pending_creates_no_transitions_twin()
    {
        HeldAttachRuntime runtime = new(test.Context.JSInterop.JSRuntime, "portal.js");
        test.Context.Services.AddSingleton<IJSRuntime>(runtime);
        Render(new PopoverSpec { DefaultOpen = true, Backdrop = true, BackdropInPortal = true });
        int before = runtime.Imports.Count(path => path.EndsWith("/transitions.js", StringComparison.Ordinal));

        await test.Context.DisposeComponentsAsync();

        // The held runtime is a test double, not interop: the analyzer reads its members as JS interop calls.
#pragma warning disable BL0016
        runtime.Release();
#pragma warning restore BL0016
        await Task.Delay(100, Xunit.TestContext.Current.CancellationToken);

        Assert.Equal(before, runtime.Imports.Count(path => path.EndsWith("/transitions.js", StringComparison.Ordinal)));
    }

    private string? PopupAttachOptions() =>
        test.Context.JSInterop.Invocations
            .Where(invocation => invocation.Identifier == "attach" && invocation.Arguments.Count > 1)
            .Select(invocation => invocation.Arguments[1] as string)
            .SingleOrDefault(json => json is not null && json.Contains("\"reportOpenComplete\":true", StringComparison.Ordinal));

    // The backdrop's options are the only transitions options in these trees that do not report the open's completion.
    private string? LastBackdropTransitionsOptions() =>
        test.Context.JSInterop.Invocations
            .Select(invocation => invocation.Identifier switch
            {
                "attach" when invocation.Arguments.Count > 1 => invocation.Arguments[1] as string,
                "update" => invocation.Arguments[0] as string,
                _ => null,
            })
            .LastOrDefault(json => json is not null && json.Contains("\"openSelector\"", StringComparison.Ordinal) && json.Contains("\"reportOpenComplete\":false", StringComparison.Ordinal));

    private bool[] RetainExitAnnouncements() =>
        [.. test.Context.JSInterop.Invocations.Where(invocation => invocation.Identifier == "retainExit").Select(invocation => (bool)invocation.Arguments[1]!)];

    private IRenderedComponent<PopupTestHost> Render(PopoverSpec spec) => test.Render((builder, _) => PopoverTree.Build(builder, spec));
}

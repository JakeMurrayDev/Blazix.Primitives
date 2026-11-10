using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components.Interop;

/// <summary>
/// Twin contract tests of the <c>interactions</c> module (ADR 06 §1, L2; ADR 04 Amendments 2026-10-27): bUnit's JS
/// interop mock records every field C# sends on <c>attach</c> and <c>update</c> for each role, and the tests drive
/// <see cref="InteractionsTwin"/>'s <c>[JSInvokable]</c> callbacks and its <c>press</c> and <c>escape</c> decision points.
/// </summary>
public sealed class InteractionsTwinContractTests
{
    private const string ModulePath = "./_content/Blazix.Primitives/js/interactions.js";

    private const string ClickJson = """
        {"kind":"PointerEvent","type":"click","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":"mouse","detail":1,"buttons":0,
         "target":{"id":"base-ui-7","tagName":"BUTTON"},"defaultPrevented":false,"timeStamp":3.5}
        """;

    private const string EscapeJson = """
        {"kind":"KeyboardEvent","type":"keydown","key":"Escape","code":"Escape","altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
         "target":{"id":"base-ui-9","tagName":"DIV"},"defaultPrevented":false,"timeStamp":9}
        """;

    [Fact]
    [Trait("Verifies", "ADR06-3")]
    public async Task The_session_handle_attaches_by_token_and_sends_every_session_and_dismissal_field()
    {
        using BunitContext context = new();
        (BunitJSModuleInterop module, BunitJSModuleInterop handle) = Setup(context);
        await using InteractionsTwin twin = new(context.JSInterop.JSRuntime);

        TwinHandle<InteractionsOptions> attached = await twin.AttachTokenAsync("base-ui-1", new InteractionsOptions
        {
            SessionToken = "base-ui-1",
            Session = new InteractionsSessionOptions(),
        });

        Assert.Same(attached, await twin.AttachTokenAsync("base-ui-1", new InteractionsOptions()));
        await attached.UpdateAsync(new InteractionsOptions
        {
            SessionToken = "base-ui-1",
            Session = new InteractionsSessionOptions
            {
                Open = true,
                ActiveTriggerId = "base-ui-2",
                ParentToken = "base-ui-0",
                FloatingId = "base-ui-3",
                Ending = true,
                Answered = 2,
                Dismiss = new InteractionsDismissOptions
                {
                    EscapeKey = false,
                    OutsidePress = true,
                    OutsidePressEvent = new InteractionsPressEvents("intentional", "sloppy"),
                    BubblesEscapeKey = true,
                    BubblesOutsidePress = false,
                    PrimaryButtonOnly = true,
                    SingleTouch = true,
                    OwnBackdropIds = ["base-ui-4", "base-ui-5"],
                    Topmost = true,
                },
            },
        });
        await attached.DisposeAsync();

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Null(attach.Arguments[0]);
        Assert.Equal("""{"sessionToken":"base-ui-1","session":{"open":false}}""", attach.Arguments[1]);
        Assert.IsType<DotNetObjectReference<InteractionsTwin>>(attach.Arguments[2]);

        JSRuntimeInvocation update = Assert.Single(handle.Invocations["update"]);
        Assert.Equal(
            """{"sessionToken":"base-ui-1","session":{"open":true,"activeTriggerId":"base-ui-2","parentToken":"base-ui-0","floatingId":"base-ui-3","ending":true,"answered":2,"dismiss":{"escapeKey":false,"outsidePress":true,"outsidePressEvent":{"mouse":"intentional","touch":"sloppy"},"bubblesEscapeKey":true,"bubblesOutsidePress":false,"primaryButtonOnly":true,"singleTouch":true,"ownBackdropIds":["base-ui-4","base-ui-5"],"topmost":true}}}""",
            update.Arguments[0]);
        Assert.Single(handle.Invocations["dispose"]);
    }

    // ADR 04 Amendments 2026-10-27 (the open-change relay, track 1's record; design point 11, the coalesced close).
    [Fact]
    [Trait("Verifies", "ADR06-3")]
    public async Task The_session_handle_sends_the_relay_with_a_coalesced_close_and_omits_it_while_null()
    {
        using BunitContext context = new();
        (BunitJSModuleInterop module, BunitJSModuleInterop handle) = Setup(context);
        await using InteractionsTwin twin = new(context.JSInterop.JSRuntime);

        TwinHandle<InteractionsOptions> attached = await twin.AttachTokenAsync("base-ui-1", new InteractionsOptions
        {
            SessionToken = "base-ui-1",
            Session = new InteractionsSessionOptions(),
        });
        await attached.UpdateAsync(new InteractionsOptions
        {
            SessionToken = "base-ui-1",
            Relay = new OpenChangeRelay(3, "trigger-press", EventKind.PointerEvent, "click", "mouse", "base-ui-2", 2, "escape-key"),
            Session = new InteractionsSessionOptions { Open = true },
        });

        Assert.Equal("""{"sessionToken":"base-ui-1","session":{"open":false}}""", Assert.Single(module.Invocations["attach"]).Arguments[1]);
        Assert.Equal(
            """{"sessionToken":"base-ui-1","relay":{"revision":3,"reason":"trigger-press","eventKind":"PointerEvent","eventType":"click","pointerType":"mouse","triggerId":"base-ui-2","coalescedCloseRevision":2,"coalescedCloseReason":"escape-key"},"session":{"open":true}}""",
            Assert.Single(handle.Invocations["update"]).Arguments[0]);
    }

    [Fact]
    public async Task The_trigger_handle_sends_every_trigger_field_and_only_the_fields_that_are_set()
    {
        using BunitContext context = new();
        (BunitJSModuleInterop module, BunitJSModuleInterop handle) = Setup(context);
        await using InteractionsTwin twin = new(context.JSInterop.JSRuntime);

        TwinHandle<InteractionsOptions> attached = await twin.AttachAsync("base-ui-2", new InteractionsOptions
        {
            SessionToken = "base-ui-1",
            Trigger = new InteractionsTriggerOptions(),
        });
        await attached.UpdateAsync(new InteractionsOptions
        {
            SessionToken = "base-ui-1",
            Trigger = new InteractionsTriggerOptions
            {
                TriggerId = "custom",
                Click = new InteractionsClickOptions
                {
                    Event = "mousedown",
                    Toggle = false,
                    IgnoreMouse = true,
                    StickIfOpen = false,
                    PatientClick = true,
                    TouchOpenDelay = 100,
                    Reason = "input-press",
                },
                ReferencePress = true,
                CancelPendingOpenOnPress = true,
                Hover = new InteractionsHoverOptions
                {
                    DelayOpen = 600,
                    DelayClose = 0,
                    RestMs = 600,
                    Move = false,
                    MouseOnly = true,
                    SafePolygon = true,
                    BlockPointerEvents = true,
                    GuardStaleOpen = true,
                    NestedTooltipTriggers = true,
                    DelayGroupToken = "base-ui-6",
                },
                Focus = new InteractionsFocusOptions(100),
                ClientPoint = new InteractionsClientPointOptions("x"),
                HoveredLine = true,
            },
        });

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Equal("base-ui-2", attach.Arguments[0]);
        Assert.Equal("""{"sessionToken":"base-ui-1","trigger":{}}""", attach.Arguments[1]);

        JSRuntimeInvocation update = Assert.Single(handle.Invocations["update"]);
        Assert.Equal(
            """{"sessionToken":"base-ui-1","trigger":{"triggerId":"custom","click":{"event":"mousedown","toggle":false,"ignoreMouse":true,"stickIfOpen":false,"patientClick":true,"touchOpenDelay":100,"reason":"input-press"},"referencePress":true,"cancelPendingOpenOnPress":true,"hover":{"delayOpen":600,"delayClose":0,"restMs":600,"move":false,"mouseOnly":true,"safePolygon":true,"blockPointerEvents":true,"guardStaleOpen":true,"nestedTooltipTriggers":true,"delayGroupToken":"base-ui-6"},"focus":{"delay":100},"clientPoint":{"axis":"x"},"hoveredLine":true}}""",
            update.Arguments[0]);
    }

    [Fact]
    public async Task The_floating_and_provider_handles_send_their_fields()
    {
        using BunitContext context = new();
        (BunitJSModuleInterop module, _) = Setup(context);
        await using InteractionsTwin twin = new(context.JSInterop.JSRuntime);

        await twin.AttachAsync("base-ui-3", new InteractionsOptions
        {
            SessionToken = "base-ui-1",
            Floating = new InteractionsFloatingOptions { Node = false, Hover = new InteractionsFloatingHoverOptions(200) },
        });
        await twin.AttachTokenAsync("base-ui-6", new InteractionsOptions
        {
            Provider = new InteractionsProviderOptions { Token = "base-ui-6", DelayOpen = 600, DelayClose = 0, Timeout = 400 },
        });

        Assert.Equal(
            """{"sessionToken":"base-ui-1","floating":{"node":false,"hover":{"closeDelay":200}}}""",
            module.Invocations["attach"][0].Arguments[1]);
        Assert.Null(module.Invocations["attach"][1].Arguments[0]);
        Assert.Equal(
            """{"provider":{"token":"base-ui-6","delayOpen":600,"delayClose":0,"timeout":400}}""",
            module.Invocations["attach"][1].Arguments[1]);
    }

    [Fact]
    public void Options_compare_by_value_so_an_unchanged_render_builds_an_equal_record()
    {
        InteractionsOptions first = new()
        {
            SessionToken = "base-ui-1",
            Session = new InteractionsSessionOptions
            {
                Open = true,
                Dismiss = new InteractionsDismissOptions { OwnBackdropIds = ["base-ui-4"], BubblesOutsidePress = true },
            },
        };
        InteractionsOptions second = new()
        {
            SessionToken = "base-ui-1",
            Session = new InteractionsSessionOptions
            {
                Open = true,
                Dismiss = new InteractionsDismissOptions { OwnBackdropIds = ["base-ui-4"], BubblesOutsidePress = true },
            },
        };

        Assert.Equal(first, second);
        Assert.NotEqual(first, second with { Session = second.Session! with { Open = false } });
        Assert.NotEqual(
            first,
            second with { Session = second.Session! with { Dismiss = second.Session.Dismiss! with { OwnBackdropIds = ["base-ui-5"] } } });
    }

    [Fact]
    public async Task The_request_report_carries_the_state_the_reason_the_event_the_trigger_and_the_instant_phase()
    {
        await using InteractionsTwin twin = new(new NoRuntime());
        List<InteractionsRequest> reported = [];
        twin.RequestReported += reported.Add;

        twin.OnRequest($$"""{"open":true,"reason":"trigger-hover","event":{{ClickJson}},"triggerId":"base-ui-7","instant":true}""");
        twin.OnRequest("""{"open":false,"reason":"none","event":null,"triggerId":null}""");

        Assert.Equal(2, reported.Count);
        Assert.True(reported[0].Open);
        Assert.Equal(EventReason.TriggerHover, InteractionsLiterals.ParseReason(reported[0].Reason));
        Assert.Equal("click", reported[0].Event?.Type);
        Assert.Equal("base-ui-7", reported[0].TriggerId);
        Assert.True(reported[0].Instant);
        Assert.False(reported[1].Open);
        Assert.Null(reported[1].Event);
        Assert.Null(reported[1].TriggerId);
        Assert.False(reported[1].Instant);
    }

    [Fact]
    public async Task The_press_report_carries_the_computed_state_the_trigger_the_interaction_type_and_the_event()
    {
        await using InteractionsTwin twin = new(new NoRuntime());
        List<InteractionsPress> reported = [];
        twin.PressReported += reported.Add;

        twin.OnPress($$"""{"open":true,"reason":"trigger-press","triggerId":"base-ui-7","interactionType":"touch","event":{{ClickJson}}}""");

        InteractionsPress press = Assert.Single(reported);
        Assert.True(press.Open);
        Assert.Equal("trigger-press", press.Reason);
        Assert.Equal("base-ui-7", press.TriggerId);
        Assert.Equal("touch", press.InteractionType);
        Assert.Equal(EventKind.PointerEvent, press.Event.Kind);
    }

    [Fact]
    public async Task The_press_point_runs_the_decider_only_over_a_handed_over_press_and_consumes_it()
    {
        await using InteractionsTwin twin = new(new NoRuntime());
        List<(InteractionsPressing? Pending, EventSnapshot Event)> seen = [];
        twin.PressDecider = snapshot =>
        {
            seen.Add((twin.PendingPress, snapshot));
            return DecisionPolicy.Stop;
        };

        Assert.Equal("run", twin.Decide(InteractionsTwin.PressPoint, ClickJson));
        Assert.Empty(seen);

        twin.OnPressing("""{"open":false,"reason":"trigger-press","triggerId":"base-ui-7","interactionType":"keyboard"}""");
        Assert.Equal("stop", twin.Decide(InteractionsTwin.PressPoint, ClickJson));

        (InteractionsPressing? pending, EventSnapshot e) = Assert.Single(seen);
        Assert.Equal(new InteractionsPressing(false, "trigger-press", "base-ui-7", "keyboard"), pending);
        Assert.Equal("click", e.Type);
        Assert.Null(twin.PendingPress);
        Assert.Equal("run", twin.Decide(InteractionsTwin.PressPoint, ClickJson));
    }

    [Theory]
    [InlineData(nameof(DecisionPolicy.PreventAndStop), "prevent-and-stop")]
    [InlineData(nameof(DecisionPolicy.Prevent), "prevent")]
    [InlineData(nameof(DecisionPolicy.Stop), "stop")]
    [InlineData(nameof(DecisionPolicy.Run), "run")]
    public async Task The_escape_point_returns_the_deciders_verdict(string verdict, string literal)
    {
        await using InteractionsTwin twin = new(new NoRuntime());
        EventSnapshot? seen = null;
        twin.EscapeDecider = snapshot =>
        {
            seen = snapshot;
            return Enum.Parse<DecisionPolicy>(verdict);
        };

        Assert.Equal(literal, twin.Decide(InteractionsTwin.EscapePoint, EscapeJson));
        Assert.Equal("Escape", seen?.Key);
    }

    [Fact]
    public async Task Without_a_decider_escape_answers_the_fallback_and_any_other_point_runs()
    {
        await using InteractionsTwin twin = new(new NoRuntime());

        Assert.Equal("prevent-and-stop", twin.Decide(InteractionsTwin.EscapePoint, EscapeJson));
        Assert.Equal("run", twin.Decide("other", EscapeJson));
    }

    // ADR 04 Amendments 2026-10-29, design point 11.2 of M4 track 7: PreviewCard's root sends its mounted state so the session
    // clears the hovered line at the unmount; false is not sent, so every other root's options are unchanged.
    [Fact(DisplayName = "port: the session's mounted input is written only when true")]
    [Trait("Requirement", "PRV-STA-13")]
    public void The_sessions_mounted_input_is_written_only_when_true()
    {
        string closed = System.Text.Json.JsonSerializer.Serialize(new InteractionsOptions { Session = new InteractionsSessionOptions() }, BlazixJsonContext.Default.InteractionsOptions);
        string mounted = System.Text.Json.JsonSerializer.Serialize(new InteractionsOptions { Session = new InteractionsSessionOptions { Mounted = true } }, BlazixJsonContext.Default.InteractionsOptions);

        Assert.DoesNotContain("mounted", closed, StringComparison.Ordinal);
        Assert.Contains("\"mounted\":true", mounted, StringComparison.Ordinal);
    }

    // ADR 04 Amendments 2026-11-03 (7) (M5 track 2): NavigationMenu's options cross as data, by their module names, and an
    // exempt selector that changes is a changed dismissal, so it is sent again.
    [Fact(DisplayName = "port: NavigationMenu's options are written by their module names, and an exempt selector change is a changed dismissal")]
    [Trait("Requirement", "NAV-PTR-10")]
    [Trait("Requirement", "NAV-PTR-13")]
    [Trait("Requirement", "NAV-PTR-17")]
    public void NavigationMenus_options_are_written_by_their_module_names_and_an_exempt_selector_change_is_a_changed_dismissal()
    {
        InteractionsOptions options = new()
        {
            Trigger = new InteractionsTriggerOptions
            {
                Hover = new InteractionsHoverOptions
                {
                    PointerEventsScopeSelector = "ul",
                    BlockPointerEventsOnEnter = true,
                    InlineCloseContext = new InteractionsInlineCloseContext("viewport-1", "base-ui-3"),
                },
            },
        };

        string json = System.Text.Json.JsonSerializer.Serialize(options, BlazixJsonContext.Default.InteractionsOptions);

        Assert.Contains("\"pointerEventsScopeSelector\":\"ul\"", json, StringComparison.Ordinal);
        Assert.Contains("\"blockPointerEventsOnEnter\":true", json, StringComparison.Ordinal);
        Assert.Contains("\"inlineCloseContext\":{\"floatingId\":\"viewport-1\",\"nodeToken\":\"base-ui-3\"}", json, StringComparison.Ordinal);
        string dismiss = System.Text.Json.JsonSerializer.Serialize(new InteractionsOptions { Session = new InteractionsSessionOptions { Dismiss = new InteractionsDismissOptions { OutsidePressExemptSelector = "[data-trigger]" } } }, BlazixJsonContext.Default.InteractionsOptions);
        Assert.Contains("\"outsidePressExemptSelector\":\"[data-trigger]\"", dismiss, StringComparison.Ordinal);
        Assert.NotEqual(new InteractionsDismissOptions { OutsidePressExemptSelector = "a" }, new InteractionsDismissOptions { OutsidePressExemptSelector = "b" });
    }

    // ADR 04 Amendments 2026-11-03 (7) (M5 track 2): Combobox's exemption list crosses as data by its module name, and the
    // dismissal compares it by sequence, so an equal list in another instance is an unchanged dismissal and sends nothing.
    [Fact(DisplayName = "port: the outside-press exemption list is written by its module name, and the dismissal compares it by sequence")]
    [Trait("Requirement", "CMB-DIS-01")]
    public void The_outside_press_exemption_list_is_written_by_its_module_name_and_the_dismissal_compares_it_by_sequence()
    {
        InteractionsDismissOptions dismiss = new() { OutsidePressExemptIds = ["clear", "chips"] };

        string json = System.Text.Json.JsonSerializer.Serialize(new InteractionsOptions { Session = new InteractionsSessionOptions { Dismiss = dismiss } }, BlazixJsonContext.Default.InteractionsOptions);

        Assert.Contains("\"outsidePressExemptIds\":[\"clear\",\"chips\"]", json, StringComparison.Ordinal);
        InteractionsDismissOptions same = new() { OutsidePressExemptIds = new List<string> { "clear", "chips" } };
        Assert.Equal(dismiss, same);
        Assert.Equal(dismiss.GetHashCode(), same.GetHashCode());
        Assert.NotEqual(dismiss, new InteractionsDismissOptions { OutsidePressExemptIds = ["chips", "clear"] });
        Assert.NotEqual(dismiss, new InteractionsDismissOptions());
    }

    // ADR 04 Amendments 2026-11-03 (7) (M5 track 2): ContextMenu's outside-press grace crosses in milliseconds; 0, its default,
    // is not sent, and a changed grace is a changed dismissal.
    [Fact(DisplayName = "port: the outside-press grace is written in milliseconds by its module name, 0 is not sent, and a change is a changed dismissal")]
    [Trait("Requirement", "CTX-STA-08")]
    [Trait("Requirement", "CTX-DIS-01")]
    public void The_outside_press_grace_is_written_in_milliseconds_by_its_module_name_0_is_not_sent_and_a_change_is_a_changed_dismissal()
    {
        string json = System.Text.Json.JsonSerializer.Serialize(new InteractionsOptions { Session = new InteractionsSessionOptions { Dismiss = new InteractionsDismissOptions { OutsidePressGrace = 500 } } }, BlazixJsonContext.Default.InteractionsOptions);
        string zero = System.Text.Json.JsonSerializer.Serialize(new InteractionsOptions { Session = new InteractionsSessionOptions { Dismiss = new InteractionsDismissOptions { OutsidePressGrace = 0 } } }, BlazixJsonContext.Default.InteractionsOptions);

        Assert.Contains("\"outsidePressGrace\":500", json, StringComparison.Ordinal);
        Assert.DoesNotContain("outsidePressGrace", zero, StringComparison.Ordinal);
        Assert.NotEqual(new InteractionsDismissOptions { OutsidePressGrace = 500 }, new InteractionsDismissOptions { OutsidePressGrace = 250 });
    }

    // ADR 04 Amendments 2026-11-03 (7) (M5 track 2): the mixed toggle's action (PSH-PTR-04) crosses by its module name, and a
    // changed action is a changed click.
    [Fact(DisplayName = "port: the mixed toggle's mousedown action is written by its module name, and a change is a changed click")]
    [Trait("Requirement", "PSH-PTR-04")]
    [Trait("Requirement", "PSH-ALG-20")]
    public void The_mixed_toggles_mousedown_action_is_written_by_its_module_name_and_a_change_is_a_changed_click()
    {
        string json = System.Text.Json.JsonSerializer.Serialize(new InteractionsOptions { Trigger = new InteractionsTriggerOptions { Click = new InteractionsClickOptions { MouseDownAction = "open" } } }, BlazixJsonContext.Default.InteractionsOptions);
        string unset = System.Text.Json.JsonSerializer.Serialize(new InteractionsOptions { Trigger = new InteractionsTriggerOptions { Click = new InteractionsClickOptions() } }, BlazixJsonContext.Default.InteractionsOptions);

        Assert.Contains("\"mouseDownAction\":\"open\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("mouseDownAction", unset, StringComparison.Ordinal);
        Assert.NotEqual(new InteractionsClickOptions { MouseDownAction = "open" }, new InteractionsClickOptions { MouseDownAction = "close" });
    }

    // ADR 04 Amendments 2026-11-04, Menu's interactions additions as built (M5 track 6), item (1): the hover gates cross by
    // their module names, each only when true; an empty menu record still crosses, and a changed gate is a changed option.
    [Fact(DisplayName = "port: Menu's hover gates are written by their module names only when true, and a change is a changed option")]
    [Trait("Requirement", "MNU-STA-11")]
    [Trait("Requirement", "MNU-STA-12")]
    [Trait("Requirement", "MNU-PTR-09")]
    public void Menus_hover_gates_are_written_by_their_module_names_only_when_true_and_a_change_is_a_changed_option()
    {
        string trigger = Serialize(new InteractionsOptions
        {
            Trigger = new InteractionsTriggerOptions
            {
                Hover = new InteractionsHoverOptions { RequireParentMouseEnter = true, RequireHoverEnabled = true },
                Menu = new InteractionsMenuTriggerOptions { TrackMouseEnter = true },
            },
        });
        string floating = Serialize(new InteractionsOptions
        {
            Floating = new InteractionsFloatingOptions
            {
                Hover = new InteractionsFloatingHoverOptions(0, RequireHoverEnabled: true),
                Menu = new InteractionsMenuFloatingOptions { DisableHoverOnMove = true },
            },
        });
        string unset = Serialize(new InteractionsOptions
        {
            Trigger = new InteractionsTriggerOptions { Hover = new InteractionsHoverOptions(), Menu = new InteractionsMenuTriggerOptions() },
            Floating = new InteractionsFloatingOptions { Hover = new InteractionsFloatingHoverOptions(0), Menu = new InteractionsMenuFloatingOptions() },
        });

        Assert.Contains("\"requireParentMouseEnter\":true", trigger, StringComparison.Ordinal);
        Assert.Contains("\"requireHoverEnabled\":true", trigger, StringComparison.Ordinal);
        Assert.Contains("\"trackMouseEnter\":true", trigger, StringComparison.Ordinal);
        Assert.Contains("\"requireHoverEnabled\":true", floating, StringComparison.Ordinal);
        Assert.Contains("\"disableHoverOnMove\":true", floating, StringComparison.Ordinal);
        Assert.DoesNotContain("requireParentMouseEnter", unset, StringComparison.Ordinal);
        Assert.DoesNotContain("requireHoverEnabled", unset, StringComparison.Ordinal);
        Assert.DoesNotContain("trackMouseEnter", unset, StringComparison.Ordinal);
        Assert.DoesNotContain("disableHoverOnMove", unset, StringComparison.Ordinal);
        Assert.Contains("\"menu\":{}", unset, StringComparison.Ordinal);
        Assert.NotEqual(new InteractionsHoverOptions { RequireHoverEnabled = true }, new InteractionsHoverOptions());
        Assert.NotEqual(new InteractionsHoverOptions { RequireParentMouseEnter = true }, new InteractionsHoverOptions());
        Assert.NotEqual(new InteractionsFloatingHoverOptions(0, RequireHoverEnabled: true), new InteractionsFloatingHoverOptions(0));
        Assert.NotEqual(new InteractionsMenuFloatingOptions { DisableHoverOnMove = true }, new InteractionsMenuFloatingOptions());
    }

    // ADR 04 Amendments 2026-11-04, Menu's interactions additions as built (M5 track 6), item (2): the item hover and the
    // sibling close delay cross by their module names; a delay of 0 (close at once) is sent and an absent one is not.
    [Fact(DisplayName = "port: Menu's item hover and sibling close delay are written by their module names, a 0 delay is sent, and a change is a changed option")]
    [Trait("Requirement", "MNU-PTR-11")]
    [Trait("Requirement", "MNU-EDGE-12")]
    public void Menus_item_hover_and_sibling_close_delay_are_written_by_their_module_names_a_0_delay_is_sent_and_a_change_is_a_changed_option()
    {
        string delayed = Serialize(new InteractionsOptions { Floating = new InteractionsFloatingOptions { Menu = new InteractionsMenuFloatingOptions { ItemHover = true, SiblingCloseDelay = 100 } } });
        string atOnce = Serialize(new InteractionsOptions { Floating = new InteractionsFloatingOptions { Menu = new InteractionsMenuFloatingOptions { SiblingCloseDelay = 0 } } });
        string unset = Serialize(new InteractionsOptions { Floating = new InteractionsFloatingOptions { Menu = new InteractionsMenuFloatingOptions() } });

        Assert.Contains("\"itemHover\":true", delayed, StringComparison.Ordinal);
        Assert.Contains("\"siblingCloseDelay\":100", delayed, StringComparison.Ordinal);
        Assert.Contains("\"siblingCloseDelay\":0", atOnce, StringComparison.Ordinal);
        Assert.DoesNotContain("itemHover", unset, StringComparison.Ordinal);
        Assert.DoesNotContain("siblingCloseDelay", unset, StringComparison.Ordinal);
        Assert.NotEqual(new InteractionsMenuFloatingOptions { SiblingCloseDelay = 100 }, new InteractionsMenuFloatingOptions { SiblingCloseDelay = 0 });
        Assert.NotEqual(new InteractionsMenuFloatingOptions { ItemHover = true }, new InteractionsMenuFloatingOptions());
    }

    // ADR 04 Amendments 2026-11-04, Menu's interactions additions as built (M5 track 6), item (3): the release options cross
    // by their module names, each flag only when true, and a change is a changed option.
    [Fact(DisplayName = "port: Menu's release options are written by their module names, each flag only when true, and a change is a changed option")]
    [Trait("Requirement", "MNU-PTR-02")]
    [Trait("Requirement", "MNU-PTR-03")]
    [Trait("Requirement", "MNU-PTR-04")]
    public void Menus_release_options_are_written_by_their_module_names_each_flag_only_when_true_and_a_change_is_a_changed_option()
    {
        string set = Serialize(new InteractionsOptions
        {
            Trigger = new InteractionsTriggerOptions { Menu = new InteractionsMenuTriggerOptions { RootId = "base-ui-1", Release = true } },
            Floating = new InteractionsFloatingOptions { Menu = new InteractionsMenuFloatingOptions { Release = true } },
        });
        string unset = Serialize(new InteractionsOptions
        {
            Trigger = new InteractionsTriggerOptions { Menu = new InteractionsMenuTriggerOptions() },
            Floating = new InteractionsFloatingOptions { Menu = new InteractionsMenuFloatingOptions() },
        });

        Assert.Contains("\"menu\":{\"rootId\":\"base-ui-1\",\"release\":true}", set, StringComparison.Ordinal);
        Assert.Contains("\"menu\":{\"release\":true}", set, StringComparison.Ordinal);
        Assert.DoesNotContain("release", unset, StringComparison.Ordinal);
        Assert.DoesNotContain("rootId", unset, StringComparison.Ordinal);
        Assert.NotEqual(new InteractionsMenuTriggerOptions { RootId = "a" }, new InteractionsMenuTriggerOptions { RootId = "b" });
        Assert.NotEqual(new InteractionsMenuTriggerOptions { Release = true }, new InteractionsMenuTriggerOptions());
        Assert.NotEqual(new InteractionsMenuFloatingOptions { Release = true }, new InteractionsMenuFloatingOptions());
    }

    // ADR 04 Amendments 2026-11-04, Menu's interactions additions as built (M5 track 6), item (4): the guard-return option
    // crosses by its module name, only when true. Written after the option (D3 of PR 183, a88bdc16), so not red first; the
    // interim review of PR 183 (F1) asked for it.
    [Fact(DisplayName = "port: Menu's guard-return option is written by its module name, only when true, and a change is a changed option")]
    [Trait("Requirement", "MNU-FOC-08")]
    public void Menus_guard_return_option_is_written_by_its_module_name_only_when_true_and_a_change_is_a_changed_option()
    {
        string set = Serialize(new InteractionsOptions { Trigger = new InteractionsTriggerOptions { Menu = new InteractionsMenuTriggerOptions { CloseOnGuardReturn = true } } });
        string unset = Serialize(new InteractionsOptions { Trigger = new InteractionsTriggerOptions { Menu = new InteractionsMenuTriggerOptions() } });

        Assert.Contains("\"menu\":{\"closeOnGuardReturn\":true}", set, StringComparison.Ordinal);
        Assert.DoesNotContain("closeOnGuardReturn", unset, StringComparison.Ordinal);
        Assert.NotEqual(new InteractionsMenuTriggerOptions { CloseOnGuardReturn = true }, new InteractionsMenuTriggerOptions());
    }

    private static string Serialize(InteractionsOptions options) => System.Text.Json.JsonSerializer.Serialize(options, BlazixJsonContext.Default.InteractionsOptions);

    private static (BunitJSModuleInterop Module, BunitJSModuleInterop Handle) Setup(BunitContext context)
    {
        BunitJSModuleInterop module = context.JSInterop.SetupModule(ModulePath);
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        return (module, handle);
    }

    private sealed class NoRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new NotSupportedException();
    }
}

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
            """{"sessionToken":"base-ui-1","session":{"open":true,"activeTriggerId":"base-ui-2","parentToken":"base-ui-0","floatingId":"base-ui-3","ending":true,"dismiss":{"escapeKey":false,"outsidePress":true,"outsidePressEvent":{"mouse":"intentional","touch":"sloppy"},"bubblesEscapeKey":true,"bubblesOutsidePress":false,"primaryButtonOnly":true,"singleTouch":true,"ownBackdropIds":["base-ui-4","base-ui-5"],"topmost":true}}}""",
            update.Arguments[0]);
        Assert.Single(handle.Invocations["dispose"]);
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

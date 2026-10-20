using System.Text.Json;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components.Interop;

/// <summary>
/// Twin contract tests of the <c>button</c> module (ADR 06 §1, L2): bUnit's JS interop mock records the options C# sends
/// on <c>attach</c> and <c>update</c>, and the tests drive <see cref="ButtonTwin"/>'s <c>[JSInvokable]</c> callbacks.
/// </summary>
public sealed class ButtonTwinContractTests
{
    private const string KeydownJson = """
        {"kind":"KeyboardEvent","type":"keydown","key":"Enter","code":"Enter","altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":true,"pointerType":null,"detail":0,"buttons":0,
         "target":{"id":"base-ui-1","tagName":"SPAN"},"defaultPrevented":false,"timeStamp":12.5}
        """;

    private const string PointerdownJson = """
        {"kind":"PointerEvent","type":"pointerdown","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":true,"pointerType":"mouse","detail":1,"buttons":1,
         "target":{"id":null,"tagName":"BUTTON"},"defaultPrevented":false,"timeStamp":40}
        """;

    [Fact]
    [Trait("Verifies", "ADR06-3")]
    [Trait("Requirement", "UBT-API-01")]
    public async Task Attach_imports_the_button_module_and_sends_the_options_on_attach_and_update()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule("./_content/Blazix.Primitives/js/button.js");
        BunitJSModuleInterop handle = SetupHandle(module);
        await using ButtonTwin twin = new(context.JSInterop.JSRuntime);

        TwinHandle<ButtonOptions> attached = await twin.AttachAsync("base-ui-1", new ButtonOptions
        {
            Disabled = true,
            FocusableWhenDisabled = true,
            Native = false,
            Composite = true,
            ReportInteractionType = true,
            DevelopmentChecks = false,
        });
        await attached.UpdateAsync(new ButtonOptions { Native = false, PressAndHold = new PressAndHoldOptions(), DevelopmentChecks = true });
        await attached.DisposeAsync();

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Equal("base-ui-1", attach.Arguments[0]);
        Assert.Equal(
            """{"disabled":true,"focusableWhenDisabled":true,"native":false,"composite":true,"reportInteractionType":true,"developmentChecks":false}""",
            attach.Arguments[1]);
        Assert.IsType<DotNetObjectReference<ButtonTwin>>(attach.Arguments[2]);

        JSRuntimeInvocation update = Assert.Single(handle.Invocations["update"]);
        Assert.Equal(
            """{"disabled":false,"native":false,"composite":false,"pressAndHold":{"tickDelay":60,"startDelay":400,"scrollDistance":8},"reportInteractionType":false,"developmentChecks":true}""",
            update.Arguments[0]);
        Assert.Single(handle.Invocations["dispose"]);
    }

    [Fact]
    [Trait("Requirement", "UBT-API-01")]
    public void The_options_default_to_the_mechanism_defaults_and_omit_an_unspecified_focusableWhenDisabled()
    {
        ButtonOptions options = new() { DevelopmentChecks = false };

        Assert.Equal(
            """{"disabled":false,"native":true,"composite":false,"reportInteractionType":false,"developmentChecks":false}""",
            JsonSerializer.Serialize(options, BlazixJsonContext.Default.ButtonOptions));
        Assert.Null(options.FocusableWhenDisabled);
        Assert.Null(options.PressAndHold);
        Assert.Null(options.Style);
    }

    [Fact]
    [Trait("Requirement", "UBT-ALG-07")]
    public void The_press_and_hold_options_default_to_a_400_ms_start_a_60_ms_tick_and_an_8_px_scroll_distance()
    {
        PressAndHoldOptions options = new();

        Assert.Equal((400, 60, 8), (options.StartDelay, options.TickDelay, options.ScrollDistance));
    }

    [Fact]
    [Trait("Requirement", "UBT-EDGE-03")]
    public void Development_checks_follow_the_development_warnings_switch_unless_set()
    {
        Assert.Equal(DevelopmentWarnings.IsEnabled, new ButtonOptions().DevelopmentChecks);
        Assert.True(new ButtonOptions { DevelopmentChecks = true }.DevelopmentChecks);
    }

    [Fact]
    [Trait("Requirement", "UBT-KEY-06")]
    public void Decide_on_the_activate_point_runs_the_activation_decider_with_the_event_and_returns_its_verdict()
    {
        using BunitContext context = new();
        List<EventSnapshot> seen = [];
        ButtonTwin twin = new(context.JSInterop.JSRuntime)
        {
            ActivationDecider = snapshot =>
            {
                seen.Add(snapshot);
                return DecisionPolicy.Stop;
            },
        };

        string verdict = twin.Decide(ButtonTwin.ActivatePoint, KeydownJson);

        Assert.Equal("activate", ButtonTwin.ActivatePoint);
        Assert.Equal("stop", verdict);
        EventSnapshot snapshot = Assert.Single(seen);
        Assert.Equal(EventKind.KeyboardEvent, snapshot.Kind);
        Assert.Equal("keydown", snapshot.Type);
        Assert.Equal("Enter", snapshot.Key);
        Assert.True(snapshot.ShiftKey);
        Assert.Equal("base-ui-1", snapshot.Target?.Id);
    }

    [Fact]
    [Trait("Requirement", "UBT-KEY-06")]
    public void Without_a_decider_or_on_another_point_the_verdict_is_run()
    {
        using BunitContext context = new();
        ButtonTwin undecided = new(context.JSInterop.JSRuntime);
        ButtonTwin decided = new(context.JSInterop.JSRuntime) { ActivationDecider = _ => DecisionPolicy.Stop };

        Assert.Equal("run", undecided.Decide(ButtonTwin.ActivatePoint, KeydownJson));
        Assert.Equal("run", decided.Decide("escape", KeydownJson));
    }

    [Fact]
    [Trait("Requirement", "UBT-EVT-08")]
    public void OnInteraction_raises_InteractionReported_with_the_classification_and_the_event()
    {
        using BunitContext context = new();
        ButtonTwin twin = new(context.JSInterop.JSRuntime);
        List<ButtonInteraction> reported = [];
        twin.InteractionReported += reported.Add;

        twin.OnInteraction($$"""{"interactionType":"mouse","event":{{PointerdownJson}}}""");
        twin.OnInteraction($$"""{"interactionType":"","event":{{PointerdownJson}}}""");

        Assert.Equal(new[] { "mouse", "" }, reported.Select(r => r.InteractionType));
        Assert.Equal(EventKind.PointerEvent, reported[0].Event.Kind);
        Assert.Equal("mouse", reported[0].Event.PointerType);
    }

    [Fact]
    [Trait("Requirement", "UBT-EVT-10")]
    public void The_hold_callbacks_raise_their_events_with_the_event_snapshot()
    {
        using BunitContext context = new();
        ButtonTwin twin = new(context.JSInterop.JSRuntime);
        List<string> log = [];
        twin.HoldTicked += e => log.Add($"tick:{e.Type}:{e.ShiftKey}");
        twin.HoldStopped += e => log.Add($"stop:{e.Type}");
        twin.HoldClicked += e => log.Add($"click:{e.Detail}");

        twin.OnHoldTick($$"""{"event":{{PointerdownJson}}}""");
        twin.OnHoldTick($$"""{"event":{{PointerdownJson}}}""");
        twin.OnHoldStop("""{"event":{"kind":"PointerEvent","type":"pointerup"}}""");
        twin.OnHoldClick("""{"event":{"kind":"PointerEvent","type":"click","detail":0}}""");

        Assert.Equal(new[] { "tick:pointerdown:True", "tick:pointerdown:True", "stop:pointerup", "click:0" }, log);
    }

    [Fact]
    [Trait("Requirement", "UBT-EDGE-01")]
    [Trait("Requirement", "UBT-EDGE-02")]
    public void OnNativeButtonMismatch_raises_NativeButtonMismatched_with_the_declared_mode()
    {
        using BunitContext context = new();
        ButtonTwin twin = new(context.JSInterop.JSRuntime);
        List<bool> reported = [];
        twin.NativeButtonMismatched += reported.Add;

        twin.OnNativeButtonMismatch("""{"expectedNative":true}""");
        twin.OnNativeButtonMismatch("""{"expectedNative":false}""");

        Assert.Equal(new[] { true, false }, reported);
    }

    [Fact]
    public void A_callback_without_a_subscriber_is_a_no_op()
    {
        using BunitContext context = new();
        ButtonTwin twin = new(context.JSInterop.JSRuntime);

        twin.OnInteraction($$"""{"interactionType":"pen","event":{{PointerdownJson}}}""");
        twin.OnHoldTick($$"""{"event":{{PointerdownJson}}}""");
        twin.OnHoldStop($$"""{"event":{{PointerdownJson}}}""");
        twin.OnHoldClick($$"""{"event":{{PointerdownJson}}}""");
        twin.OnNativeButtonMismatch("""{"expectedNative":true}""");
    }

    [Fact]
    public void A_malformed_payload_is_rejected()
    {
        using BunitContext context = new();
        ButtonTwin twin = new(context.JSInterop.JSRuntime);

        Assert.Throws<JsonException>(() => twin.OnHoldTick("null"));
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

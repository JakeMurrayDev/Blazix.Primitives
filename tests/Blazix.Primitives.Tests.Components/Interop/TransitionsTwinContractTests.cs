using System.Text.Json;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components.Interop;

/// <summary>
/// Twin contract tests of the <c>transitions</c> module (ADR 06 §1, L2): bUnit's JS interop mock records the options
/// C# sends on <c>attach</c> and <c>update</c> (the machine options, <c>keepMounted</c>, <c>hiddenUntilFound</c>, the
/// handed-over style), and the tests drive <see cref="TransitionsTwin"/>'s <c>[JSInvokable]</c> callbacks. The
/// document's L2 rows that need a part (TRN-STA-01/02/03/13, TRN-DOM-03, TRN-ANI-02/14, TRN-EDGE-03/04/05, TRN-ALG-01,
/// TRN-API-01, TRN-DOM-07) are translated by Collapsible and Avatar on their parts.
/// </summary>
public sealed class TransitionsTwinContractTests
{
    private const string ModulePath = "./_content/Blazix.Primitives/js/transitions.js";

    private const string BeforeMatchJson = """
        {"kind":"Event","type":"beforematch","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
         "target":{"id":"base-ui-7","tagName":"DIV"},"defaultPrevented":false,"timeStamp":31.25}
        """;

    [Fact]
    [Trait("Requirement", "TRN-ALG-01")]
    [Trait("Requirement", "TRN-API-03")]
    public async Task Attach_imports_the_transitions_module_and_sends_the_options_on_attach_and_update()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(ModulePath);
        BunitJSModuleInterop handle = SetupHandle(module);
        await using TransitionsTwin twin = new(context.JSInterop.JSRuntime);

        TwinHandle<TransitionsOptions> attached = await twin.AttachAsync("base-ui-7", new TransitionsOptions
        {
            Open = true,
            Mounted = true,
            Status = TransitionStatus.Starting,
            EnableIdleState = true,
            DeferEndingState = true,
            KeepMounted = true,
            HiddenUntilFound = true,
            HiddenWhenUnmounted = true,
            RevealOnBeforeMatch = true,
            OpenSelector = "[data-open]",
            Style = "--collapsible-panel-height: auto",
        });
        await attached.UpdateAsync(new TransitionsOptions
        {
            Open = false,
            Mounted = true,
            EnableIdleState = true,
            DeferEndingState = true,
            KeepMounted = true,
            HiddenUntilFound = true,
            HiddenWhenUnmounted = true,
            RevealOnBeforeMatch = true,
            UnmountRequests = 1,
            Style = "color: red",
        });
        await attached.DisposeAsync();

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Equal("base-ui-7", attach.Arguments[0]);
        Assert.Equal(
            """{"open":true,"mounted":true,"status":"starting","enableIdleState":true,"deferEndingState":true,"keepMounted":true,"hiddenUntilFound":true,"hiddenWhenUnmounted":true,"suppressEndingStyle":false,"reportOpenComplete":false,"revealOnBeforeMatch":true,"unmountRequests":0,"openSelector":"[data-open]","style":"--collapsible-panel-height: auto"}""",
            attach.Arguments[1]);
        Assert.IsType<DotNetObjectReference<TransitionsTwin>>(attach.Arguments[2]);

        JSRuntimeInvocation update = Assert.Single(handle.Invocations["update"]);
        Assert.Equal(
            """{"open":false,"mounted":true,"enableIdleState":true,"deferEndingState":true,"keepMounted":true,"hiddenUntilFound":true,"hiddenWhenUnmounted":true,"suppressEndingStyle":false,"reportOpenComplete":false,"revealOnBeforeMatch":true,"unmountRequests":1,"style":"color: red"}""",
            update.Arguments[0]);
        Assert.Single(handle.Invocations["dispose"]);
    }

    [Fact]
    [Trait("Requirement", "TRN-STA-01")]
    public void The_status_travels_as_its_literal_and_the_unset_status_is_left_out()
    {
        Assert.Equal(
            """{"open":false,"mounted":false,"enableIdleState":false,"deferEndingState":false,"keepMounted":false,"hiddenUntilFound":false,"hiddenWhenUnmounted":false,"suppressEndingStyle":false,"reportOpenComplete":false,"revealOnBeforeMatch":false,"unmountRequests":0}""",
            JsonSerializer.Serialize(new TransitionsOptions(), BlazixJsonContext.Default.TransitionsOptions));

        foreach ((TransitionStatus status, string literal) in new[] { (TransitionStatus.Starting, "starting"), (TransitionStatus.Ending, "ending"), (TransitionStatus.Idle, "idle") })
        {
            string json = JsonSerializer.Serialize(new TransitionsOptions { Status = status }, BlazixJsonContext.Default.TransitionsOptions);
            Assert.Contains($"\"status\":\"{literal}\"", json, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Verifies", "ADR06-3")]
    [Trait("Requirement", "COL-EDGE-09")]
    public async Task A_repeated_attach_of_the_same_element_and_a_repeated_dispose_send_nothing_more()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(ModulePath);
        BunitJSModuleInterop handle = SetupHandle(module);
        await using TransitionsTwin twin = new(context.JSInterop.JSRuntime);

        TwinHandle<TransitionsOptions> first = await twin.AttachAsync("base-ui-7", new TransitionsOptions());
        TwinHandle<TransitionsOptions> second = await twin.AttachAsync("base-ui-7", new TransitionsOptions { Open = true });
        await first.DisposeAsync();
        await first.DisposeAsync();
        await first.UpdateAsync(new TransitionsOptions { Open = true });

        Assert.Same(first, second);
        Assert.Single(module.Invocations["attach"]);
        Assert.Single(handle.Invocations["dispose"]);
        Assert.Empty(handle.Invocations["update"]);
    }

    [Fact]
    [Trait("Requirement", "TRN-API-07")]
    public void OnStatusChange_raises_StatusChanged_once_per_message_with_the_typed_status()
    {
        using BunitContext context = new();
        TransitionsTwin twin = new(context.JSInterop.JSRuntime);
        List<TransitionStatusChange> reported = [];
        twin.StatusChanged += reported.Add;

        twin.OnStatusChange("""{"status":"starting","mounted":true}""");
        twin.OnStatusChange("""{"status":"idle","mounted":true}""");
        twin.OnStatusChange("""{"status":"ending","mounted":true}""");
        twin.OnStatusChange("""{"status":null,"mounted":false}""");

        Assert.Equal(
            new TransitionStatusChange[]
            {
                new(TransitionStatus.Starting, true),
                new(TransitionStatus.Idle, true),
                new(TransitionStatus.Ending, true),
                new(null, false),
            },
            reported);
    }

    [Fact]
    [Trait("Requirement", "TRN-ANI-03")]
    public void OnTransitionComplete_raises_TransitionCompleted_with_the_side_that_finished()
    {
        using BunitContext context = new();
        TransitionsTwin twin = new(context.JSInterop.JSRuntime);
        List<bool> reported = [];
        twin.TransitionCompleted += reported.Add;

        twin.OnTransitionComplete("""{"open":false}""");
        twin.OnTransitionComplete("""{"open":true}""");

        Assert.Equal(new[] { false, true }, reported);
    }

    [Fact]
    [Trait("Requirement", "TRN-EVT-09")]
    public void OnRevealRequest_raises_RevealRequested_with_the_beforematch_snapshot()
    {
        using BunitContext context = new();
        TransitionsTwin twin = new(context.JSInterop.JSRuntime);
        List<EventSnapshot> reported = [];
        twin.RevealRequested += reported.Add;

        twin.OnRevealRequest($$"""{"event":{{BeforeMatchJson}}}""");

        EventSnapshot snapshot = Assert.Single(reported);
        Assert.Equal(EventKind.Event, snapshot.Kind);
        Assert.Equal("beforematch", snapshot.Type);
        Assert.Equal("base-ui-7", snapshot.Target?.Id);
    }

    [Fact]
    [Trait("Requirement", "TRN-ANI-33")]
    public void Decide_on_the_reveal_point_runs_the_reveal_decider_with_the_event_and_returns_its_verdict()
    {
        using BunitContext context = new();
        List<EventSnapshot> seen = [];
        TransitionsTwin twin = new(context.JSInterop.JSRuntime)
        {
            RevealDecider = snapshot =>
            {
                seen.Add(snapshot);
                return DecisionPolicy.Stop;
            },
        };

        string verdict = twin.Decide(TransitionsTwin.RevealPoint, BeforeMatchJson);

        Assert.Equal("reveal", TransitionsTwin.RevealPoint);
        Assert.Equal("stop", verdict);
        Assert.Equal("beforematch", Assert.Single(seen).Type);
    }

    [Fact]
    public void Without_a_decider_or_on_another_point_the_verdict_is_run()
    {
        using BunitContext context = new();
        TransitionsTwin undecided = new(context.JSInterop.JSRuntime);
        TransitionsTwin decided = new(context.JSInterop.JSRuntime) { RevealDecider = _ => DecisionPolicy.Stop };

        Assert.Equal("run", undecided.Decide(TransitionsTwin.RevealPoint, BeforeMatchJson));
        Assert.Equal("run", decided.Decide("activate", BeforeMatchJson));
    }

    [Fact]
    public void A_callback_without_a_subscriber_is_a_no_op_and_a_malformed_payload_is_rejected()
    {
        using BunitContext context = new();
        TransitionsTwin twin = new(context.JSInterop.JSRuntime);

        twin.OnStatusChange("""{"status":"idle","mounted":true}""");
        twin.OnTransitionComplete("""{"open":false}""");
        twin.OnRevealRequest($$"""{"event":{{BeforeMatchJson}}}""");

        Assert.Throws<JsonException>(() => twin.OnStatusChange("""{"status":"opening","mounted":true}"""));
        Assert.Throws<JsonException>(() => twin.OnStatusChange("""{"status":1,"mounted":true}"""));
        Assert.Throws<JsonException>(() => twin.OnTransitionComplete("null"));
        Assert.Throws<JsonException>(() => twin.OnRevealRequest("null"));
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

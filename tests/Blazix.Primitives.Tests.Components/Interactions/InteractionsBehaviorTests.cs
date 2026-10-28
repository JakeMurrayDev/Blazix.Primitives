using System.Text.Json;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// L2 (ADR 06 §1): the C# half of the <c>interactions</c> module on test-only hosts (<see cref="TestInteractionsRoot"/>,
/// <see cref="TestInteractionsTrigger"/>): the session's token and its options, the Escape chain's verdicts, the press with
/// the consumer's onclick run once on both modes, and an unchanged render that sends nothing.
/// </summary>
public sealed class InteractionsBehaviorTests : IDisposable
{
    private const string ClickJson = """
        {"kind":"PointerEvent","type":"click","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":"mouse","detail":1,"buttons":0,
         "target":{"id":"t","tagName":"BUTTON"},"defaultPrevented":false,"timeStamp":3.5}
        """;

    private const string EscapeJson = """
        {"kind":"KeyboardEvent","type":"keydown","key":"Escape","code":"Escape","altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
         "target":null,"defaultPrevented":false,"timeStamp":9}
        """;

    private readonly InteractionsTestContext host = new();

    /// <inheritdoc />
    public void Dispose() => host.Dispose();

    [Fact]
    public async Task The_root_draws_a_token_attaches_the_session_by_it_and_cascades_it_to_the_trigger()
    {
        IRenderedComponent<TestInteractionsRoot> cut = RenderPopup();

        string token = cut.Instance.Session.Token;
        Assert.StartsWith("base-ui-", token, StringComparison.Ordinal);
        Assert.Empty(cut.Markup.Split(token)[1..]);
        JSRuntimeInvocation session = host.Attaches.Single(attach => attach.Arguments[0] is null);
        Assert.Equal(token, JsonDocument.Parse((string)session.Arguments[1]!).RootElement.GetProperty("sessionToken").GetString());
        JSRuntimeInvocation trigger = host.Attaches.Single(attach => attach.Arguments[0] is not null);
        Assert.Equal(token, JsonDocument.Parse((string)trigger.Arguments[1]!).RootElement.GetProperty("sessionToken").GetString());
    }

    [Theory]
    [InlineData(false, false, "prevent-and-stop", true)]
    [InlineData(false, true, "prevent", true)]
    [InlineData(true, false, "stop", false)]
    [InlineData(true, true, "run", false)]
    public async Task The_escape_chain_maps_cancel_and_allow_propagation_to_the_four_verdicts(bool cancel, bool allowPropagation, string verdict, bool closes)
    {
        IRenderedComponent<TestInteractionsRoot> cut = RenderPopup(onOpenChange: (_, details) =>
        {
            if (details.Reason != EventReason.EscapeKey)
            {
                return;
            }

            if (cancel)
            {
                details.Cancel();
            }

            if (allowPropagation)
            {
                details.AllowPropagation();
            }
        });
        await cut.InvokeAsync(() => cut.Instance.RequestOpenChange(new OpenChangeRequest(true, new OpenChangeEventDetails(EventReason.TriggerPress), false, null)));

        string answer = host.SessionTwin.Decide(InteractionsTwin.EscapePoint, EscapeJson);

        Assert.Equal(verdict, answer);
        Assert.Equal(EventReason.EscapeKey, cut.Instance.Requests[^1].Details.Reason);
        Assert.Equal("Escape", cut.Instance.Requests[^1].Details.Event?.Key);
        Assert.Equal(!closes, cut.Instance.Open);
    }

    [Fact]
    public async Task A_reported_request_reaches_the_store_with_its_reason_event_trigger_and_instant_phase()
    {
        IRenderedComponent<TestInteractionsRoot> cut = RenderPopup();

        await cut.InvokeAsync(() => host.TriggerTwin.OnRequest($$"""{"open":true,"reason":"trigger-hover","event":{{ClickJson}},"triggerId":"t","instant":true}"""));

        OpenChangeRequest request = Assert.Single(cut.Instance.Requests);
        Assert.True(request.Open);
        Assert.Equal(EventReason.TriggerHover, request.Details.Reason);
        Assert.Equal("click", request.Details.Event?.Type);
        Assert.Equal("t", request.Details.Trigger?.Id);
        Assert.True(request.Instant);
        Assert.True(cut.Instance.Open);
        Assert.Contains("\"open\":true", host.UpdateOptions[^1], StringComparison.Ordinal);
    }

    // ADR 04 Amendments 2026-10-27: a popup root's session joins the token its popup store drew (PSH-STR-01), so every
    // module of the popup shares one session, and carries the store's relay; a moved revision is an update.
    [Fact]
    public async Task A_session_joins_the_token_it_is_given_and_sends_the_relay_and_its_changes()
    {
        OpenChangeRelay relay = new(1, "trigger-press", EventKind.PointerEvent, "click", "mouse", "base-ui-5");
        await using InteractionsSessionBehavior session = new(host.Context.JSInterop.JSRuntime, "base-ui-9", _ => Task.CompletedTask, _ => Task.CompletedTask)
        {
            Open = true,
            Relay = relay,
        };

        await session.AfterRenderAsync();
        await session.AfterRenderAsync();
        session.Relay = relay with { Revision = 2, Reason = "escape-key", EventKind = EventKind.KeyboardEvent, EventType = "keydown", PointerType = null };
        await session.AfterRenderAsync();

        Assert.Equal("base-ui-9", session.Token);
        JSRuntimeInvocation attach = Assert.Single(host.Attaches);
        Assert.Null(attach.Arguments[0]);
        JsonElement options = JsonDocument.Parse((string)attach.Arguments[1]!).RootElement;
        Assert.Equal("base-ui-9", options.GetProperty("sessionToken").GetString());
        Assert.Equal(1, options.GetProperty("relay").GetProperty("revision").GetInt32());
        JsonElement update = JsonDocument.Parse(Assert.Single(host.UpdateOptions)).RootElement;
        Assert.Equal(2, update.GetProperty("relay").GetProperty("revision").GetInt32());
        Assert.Equal("escape-key", update.GetProperty("relay").GetProperty("reason").GetString());
    }

    [Fact]
    public async Task On_WebAssembly_the_press_decider_runs_the_declined_consumer_onclick_once_then_the_request()
    {
        int consumerClicks = 0;
        IRenderedComponent<TestInteractionsRoot> cut = RenderPopup(onClick: _ => consumerClicks++);
        IRenderedComponent<TestInteractionsTrigger> trigger = cut.FindComponent<TestInteractionsTrigger>();

        string verdict = await cut.InvokeAsync(() =>
        {
            host.TriggerTwin.OnPressing($$"""{"open":true,"reason":"trigger-press","triggerId":"t","interactionType":"mouse"}""");
            return host.TriggerTwin.Decide(InteractionsTwin.PressPoint, ClickJson);
        });
        trigger.Find("button").Click(new MouseEventArgs { Detail = 1 });

        Assert.Equal("run", verdict);
        Assert.Equal(1, consumerClicks);
        OpenChangeRequest request = Assert.Single(cut.Instance.Requests);
        Assert.True(request.Open);
        Assert.Equal(EventReason.TriggerPress, request.Details.Reason);
        Assert.Equal(InteractionType.Mouse, request.InteractionType);
        Assert.True(cut.Instance.Open);
    }

    [Fact]
    public async Task A_consumer_veto_of_the_press_holds_on_WebAssembly_and_on_Server()
    {
        IRenderedComponent<TestInteractionsRoot> cut = RenderPopup(onClick: e => e.PreventPartHandler());
        IRenderedComponent<TestInteractionsTrigger> trigger = cut.FindComponent<TestInteractionsTrigger>();

        string verdict = await cut.InvokeAsync(() =>
        {
            host.TriggerTwin.OnPressing("""{"open":true,"reason":"trigger-press","triggerId":"t","interactionType":"mouse"}""");
            return host.TriggerTwin.Decide(InteractionsTwin.PressPoint, ClickJson);
        });
        trigger.Find("button").Click(new MouseEventArgs { Detail = 1 });
        await cut.InvokeAsync(() => host.TriggerTwin.OnPress($$"""{"open":true,"reason":"trigger-press","triggerId":"t","interactionType":"mouse","event":{{ClickJson.Replace("\"detail\":1", "\"detail\":2", StringComparison.Ordinal)}}}"""));

        Assert.Equal("stop", verdict);
        Assert.Empty(cut.Instance.Requests);
        Assert.False(cut.Instance.Open);
    }

    [Fact]
    public async Task On_Server_the_press_report_runs_the_consumer_onclick_once_then_the_request()
    {
        int consumerClicks = 0;
        IRenderedComponent<TestInteractionsRoot> cut = RenderPopup(onClick: _ => consumerClicks++);
        IRenderedComponent<TestInteractionsTrigger> trigger = cut.FindComponent<TestInteractionsTrigger>();

        // The module's report precedes Blazor's dispatch of the same click on one circuit (ADR-04/1).
        await cut.InvokeAsync(() => host.TriggerTwin.OnPress($$"""{"open":true,"reason":"trigger-press","triggerId":"t","interactionType":"touch","event":{{ClickJson}}}"""));
        trigger.Find("button").Click(new MouseEventArgs { Detail = 1 });
        Assert.Equal(1, consumerClicks);
        Assert.True(cut.Instance.Open);
        Assert.Equal(InteractionType.Touch, cut.Instance.Requests[^1].InteractionType);
    }

    [Fact]
    public async Task A_click_that_is_not_a_press_leaves_no_verdict_behind_so_the_next_press_runs_the_consumer_and_its_veto_holds()
    {
        bool veto = false;
        int consumerClicks = 0;
        IRenderedComponent<TestInteractionsRoot> cut = RenderPopup(onClick: e =>
        {
            consumerClicks++;
            if (veto)
            {
                e.PreventPartHandler();
            }
        });
        IRenderedComponent<TestInteractionsTrigger> trigger = cut.FindComponent<TestInteractionsTrigger>();

        // A click the module did not turn into a press (for example the click after a mousedown press) reaches only Blazor.
        trigger.Find("button").Click(new MouseEventArgs { Detail = 1 });
        Assert.Equal(1, consumerClicks);

        veto = true;
        await cut.InvokeAsync(() => host.TriggerTwin.OnPress($$"""{"open":true,"reason":"trigger-press","triggerId":"t","interactionType":"mouse","event":{{ClickJson}}}"""));
        trigger.Find("button").Click(new MouseEventArgs { Detail = 1 });

        Assert.Equal(2, consumerClicks);
        Assert.Empty(cut.Instance.Requests);
        Assert.False(cut.Instance.Open);
    }

    [Fact]
    public async Task The_trigger_declines_the_consumer_onclick_only_while_a_press_toggles()
    {
        IRenderedComponent<TestInteractionsRoot> cut = RenderPopup(onClick: _ => { });
        TestInteractionsTrigger trigger = cut.FindComponent<TestInteractionsTrigger>().Instance;

        Assert.False(trigger.Behavior.ForwardsConsumerHandler("onclick"));
        Assert.True(trigger.Behavior.ForwardsConsumerHandler("onmouseenter"));

        trigger.Behavior.Options = trigger.Behavior.Options with { Click = null };
        Assert.True(trigger.Behavior.ForwardsConsumerHandler("onclick"));
    }

    [Fact]
    public async Task A_rerender_with_unchanged_inputs_sends_no_update_and_renders_the_trigger_once_per_render()
    {
        int renders = 0;
        IRenderedComponent<TestInteractionsRoot> cut = RenderPopup(classValue: () => renders++);
        IRenderedComponent<TestInteractionsTrigger> trigger = cut.FindComponent<TestInteractionsTrigger>();
        int sessionSends = cut.Instance.Session.Sends;
        int triggerSends = trigger.Instance.Behavior.Sends;
        int before = renders;

        await cut.InvokeAsync(cut.Instance.Refresh);
        await cut.InvokeAsync(trigger.Instance.Refresh);

        // One render per refresh: the root's re-renders the trigger (its class function is a new parameter value), and the
        // trigger's own; neither renders twice and neither sends options.
        Assert.Empty(host.UpdateOptions);
        Assert.Equal(sessionSends, cut.Instance.Session.Sends);
        Assert.Equal(triggerSends, trigger.Instance.Behavior.Sends);
        Assert.Equal(before + 2, renders);
    }

    [Fact]
    public async Task A_committed_press_sends_one_session_update_and_renders_the_trigger_once()
    {
        int renders = 0;
        IRenderedComponent<TestInteractionsRoot> cut = RenderPopup(classValue: () => renders++);
        int before = renders;

        await cut.InvokeAsync(() => host.TriggerTwin.OnPress($$"""{"open":true,"reason":"trigger-press","triggerId":"t","interactionType":"mouse","event":{{ClickJson}}}"""));

        string update = Assert.Single(host.UpdateOptions);
        Assert.Contains("\"open\":true", update, StringComparison.Ordinal);
        Assert.Contains("\"activeTriggerId\":\"t\"", update, StringComparison.Ordinal);
        Assert.Equal(before + 1, renders);
    }

    [Fact]
    public async Task A_canceled_session_request_sends_one_update_with_the_answered_counter_and_renders_nothing()
    {
        int renders = 0;
        IRenderedComponent<TestInteractionsRoot> cut = RenderPopup(onOpenChange: (_, details) => details.Cancel(), classValue: () => renders++);
        int before = renders;

        await cut.InvokeAsync(() => host.SessionTwin.OnRequest($$"""{"open":true,"reason":"outside-press","event":{{ClickJson}},"triggerId":null}"""));

        string update = Assert.Single(host.UpdateOptions);
        Assert.Contains("\"answered\":1", update, StringComparison.Ordinal);
        Assert.Contains("\"open\":false", update, StringComparison.Ordinal);
        Assert.Equal(before, renders);
        Assert.False(cut.Instance.Open);
    }

    [Fact]
    public async Task An_accepted_session_request_sends_one_update_with_the_committed_state_and_the_answered_counter()
    {
        IRenderedComponent<TestInteractionsRoot> cut = RenderPopup();

        await cut.InvokeAsync(() => host.SessionTwin.OnRequest($$"""{"open":true,"reason":"trigger-hover","event":{{ClickJson}},"triggerId":"t"}"""));

        string update = Assert.Single(host.UpdateOptions);
        Assert.Contains("\"open\":true", update, StringComparison.Ordinal);
        Assert.Contains("\"activeTriggerId\":\"t\"", update, StringComparison.Ordinal);
        Assert.Contains("\"answered\":1", update, StringComparison.Ordinal);
    }

    // The module keys a trigger handle by the session it attached under (a fidelity correction of track 3's behaviour,
    // found by track 5 on detached triggers; settled by the orchestrator 2026-10-28).
    [Fact]
    public async Task A_trigger_handle_attaches_once_its_session_has_a_token_again_under_another_and_keeps_it_through_a_missing_one()
    {
        await using InteractionsTriggerBehavior behavior = new(host.Context.JSInterop.JSRuntime, _ => Task.CompletedTask, _ => Task.CompletedTask, EventCallback.Empty);
        behavior.Options = new InteractionsTriggerOptions { Click = new InteractionsClickOptions() };

        // A detached trigger in its handle's fallback store has no session yet: attaching would key it under none.
        await behavior.AfterRenderAsync(true, null, "t");
        Assert.Empty(host.Module.Invocations["attach"]);

        behavior.SessionToken = "base-ui-1";
        await behavior.AfterRenderAsync(true, null, "t");
        JSRuntimeInvocation first = Assert.Single(host.Attaches);
        Assert.Equal("base-ui-1", SessionTokenOf((string)first.Arguments[1]!));

        // A batch without a token (a recreated handle before its root attaches) keeps the live handle and sends nothing.
        behavior.SessionToken = null;
        await behavior.AfterRenderAsync(true, null, "t");
        Assert.Empty(host.Handle.Invocations["dispose"]);
        Assert.Empty(host.Handle.Invocations["update"]);

        // Another root's session: the handle attaches again under its token.
        behavior.SessionToken = "base-ui-2";
        await behavior.AfterRenderAsync(true, null, "t");
        Assert.Single(host.Handle.Invocations["dispose"]);
        Assert.Equal(2, host.Attaches.Count);
        Assert.Equal("base-ui-2", SessionTokenOf((string)host.Attaches[1].Arguments[1]!));
    }

    private static string? SessionTokenOf(string options) => JsonDocument.Parse(options).RootElement.GetProperty("sessionToken").GetString();

    private IRenderedComponent<TestInteractionsRoot> RenderPopup(
        Action<bool, OpenChangeEventDetails>? onOpenChange = null,
        Action<MouseEventArgs>? onClick = null,
        Action? classValue = null)
    {
        return host.Context.Render<TestInteractionsRoot>(p => p
            .Add(x => x.OnOpenChange, onOpenChange)
            .Add(x => x.Dismiss, new InteractionsDismissOptions())
            .AddChildContent<TestInteractionsTrigger>(t =>
            {
                if (onClick is not null)
                {
                    t.AddUnmatched("onclick", EventCallback.Factory.Create(this, onClick));
                }

                if (classValue is not null)
                {
                    t.Add(x => x.ClassValue, _ =>
                    {
                        classValue();
                        return null;
                    });
                }
            }));
    }
}

using System.Text.Json;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components.Interop;

/// <summary>
/// Twin contract tests of the <c>context-menu</c> module (ADR 06 §1, L2; ADR 04 Amendments 2026-10-18 and 2026-11-03
/// (3), (4)): bUnit's JS interop mock records every field C# sends on <c>attach</c> and <c>update</c>, the Root's handle
/// by token with its elements as references, and the tests drive <see cref="ContextMenuTwin"/>'s <c>[JSInvokable]</c>
/// callbacks and its <c>press</c> decision point.
/// </summary>
public sealed class ContextMenuTwinContractTests
{
    private const string ModulePath = "./_content/Blazix.Primitives/js/context-menu.js";

    private const string ContextMenuJson = """
        {"kind":"MouseEvent","type":"contextmenu","key":null,"code":null,"altKey":false,"ctrlKey":true,
         "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":2,
         "target":{"id":"base-ui-3","tagName":"DIV"},"defaultPrevented":false,"timeStamp":4.5}
        """;

    private const string TouchStartJson = """
        {"kind":"TouchEvent","type":"touchstart","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
         "target":{"id":"base-ui-3","tagName":"DIV"},"defaultPrevented":false,"timeStamp":8}
        """;

    [Fact]
    [Trait("Verifies", "ADR06-3")]
    [Trait("Requirement", "CTX-STA-04")]
    [Trait("Requirement", "CTX-PTR-03")]
    public async Task The_root_attaches_by_token_and_sends_its_elements_as_references()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(ModulePath);
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        await using ContextMenuTwin twin = new(context.JSInterop.JSRuntime);
        ElementReference backdrop = new("backdrop-ref");
        ElementReference positioner = new("positioner-ref");

        TwinHandle<ContextMenuOptions> attached = await twin.AttachTokenAsync("base-ui-1", new ContextMenuOptions { Token = "base-ui-1" });
        Assert.Same(attached, await twin.AttachTokenAsync("base-ui-1", new ContextMenuOptions { Token = "base-ui-1" }));
        TwinReferences references = new();
        references.Add(backdrop);
        references.Add(positioner);
        await attached.UpdateAsync(new ContextMenuOptions { Token = "base-ui-1", Backdrop = 0, Positioner = 1 }, references);
        await attached.DisposeAsync();

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Null(attach.Arguments[0]);
        Assert.Equal("""{"part":"root","token":"base-ui-1"}""", attach.Arguments[1]);
        Assert.IsType<DotNetObjectReference<ContextMenuTwin>>(attach.Arguments[2]);
        JSRuntimeInvocation update = Assert.Single(handle.Invocations["update"]);
        Assert.Equal("""{"part":"root","token":"base-ui-1","backdrop":0,"positioner":1}""", update.Arguments[0]);
        object[] sent = Assert.IsType<object[]>(update.Arguments[1]);
        Assert.Equal([backdrop, positioner], sent);
        Assert.Single(handle.Invocations["dispose"]);
    }

    [Fact]
    [Trait("Verifies", "ADR06-3")]
    [Trait("Requirement", "CTX-PTR-01")]
    [Trait("Requirement", "CTX-API-06")]
    public async Task A_trigger_attaches_on_its_element_with_the_token_the_root_id_the_disabled_flag_and_the_consumer_handlers()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(ModulePath);
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        await using ContextMenuTwin twin = new(context.JSInterop.JSRuntime);

        TwinHandle<ContextMenuOptions> attached = await twin.AttachAsync(new ElementReference("trigger-ref"), new ContextMenuOptions
        {
            Part = ContextMenuTwin.TriggerPart,
            Token = "base-ui-1",
            RootId = "base-ui-2",
            Disabled = false,
            Handlers = ["contextmenu", "touchstart"],
        });
        await attached.UpdateAsync(new ContextMenuOptions { Part = ContextMenuTwin.TriggerPart, Token = "base-ui-1", RootId = "base-ui-2", Disabled = true });
        await attached.DisposeAsync();

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Equal(
            """{"part":"trigger","token":"base-ui-1","rootId":"base-ui-2","disabled":false,"handlers":["contextmenu","touchstart"]}""",
            attach.Arguments[1]);
        JSRuntimeInvocation update = Assert.Single(handle.Invocations["update"]);
        Assert.Equal("""{"part":"trigger","token":"base-ui-1","rootId":"base-ui-2","disabled":true}""", update.Arguments[0]);
    }

    [Fact]
    [Trait("Requirement", "CTX-API-06")]
    public void Options_with_the_same_handlers_are_equal_whatever_the_list_instance()
    {
        ContextMenuOptions first = new() { Part = ContextMenuTwin.TriggerPart, Handlers = ["contextmenu"] };
        ContextMenuOptions second = new() { Part = ContextMenuTwin.TriggerPart, Handlers = ["contextmenu"] };
        ContextMenuOptions third = new() { Part = ContextMenuTwin.TriggerPart, Handlers = ["touchstart"] };

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, third);
        Assert.NotEqual(first, new ContextMenuOptions { Part = ContextMenuTwin.TriggerPart });
    }

    [Fact]
    [Trait("Requirement", "CTX-PTR-01")]
    [Trait("Requirement", "CTX-PTR-06")]
    [Trait("Requirement", "CTX-PTR-05")]
    [Trait("Requirement", "CTX-EVT-02")]
    [Trait("Requirement", "CTX-EVT-05")]
    public async Task Each_report_raises_its_event_once_with_its_payload()
    {
        await using ContextMenuTwin twin = new(new NoRuntime());
        List<ContextMenuOpenRequest> opens = [];
        List<ContextMenuCancelOpen> cancels = [];
        List<ContextMenuPress> presses = [];
        twin.OpenRequested += opens.Add;
        twin.CancelOpenRequested += cancels.Add;
        twin.Pressed += presses.Add;

        twin.OnOpen($$"""{"x":120,"y":80.5,"pointerType":"mouse","event":{{ContextMenuJson}}}""");
        twin.OnOpen($$"""{"x":10,"y":20,"pointerType":"touch","event":{{TouchStartJson}}}""");
        twin.OnCancelOpen($$"""{"event":{{ContextMenuJson.Replace("\"contextmenu\"", "\"mouseup\"", StringComparison.Ordinal)}}}""");
        twin.OnPress($$"""{"clientX":1,"clientY":2,"screenX":3,"screenY":4,"pageX":5,"pageY":6,"offsetX":7,"offsetY":8,"button":2,"buttons":2,"touches":[],"targetTouches":[],"changedTouches":[],"event":{{ContextMenuJson}}}""");

        Assert.Equal(2, opens.Count);
        Assert.Equal((120, 80.5, "mouse", "contextmenu"), (opens[0].X, opens[0].Y, opens[0].PointerType, opens[0].Event!.Type));
        Assert.Equal((10, 20, "touch", "touchstart"), (opens[1].X, opens[1].Y, opens[1].PointerType, opens[1].Event!.Type));
        Assert.Equal("mouseup", Assert.Single(cancels).Event!.Type);
        ContextMenuPress press = Assert.Single(presses);
        Assert.Equal((1, 2, 2L), (press.ClientX, press.ClientY, press.Button));
        Assert.True(press.Event!.CtrlKey);
    }

    [Fact]
    [Trait("Requirement", "CTX-API-06")]
    [Trait("Requirement", "EVD-EVT-26")]
    public async Task The_press_decision_hands_the_pressing_payload_to_the_decider_once_and_runs_otherwise()
    {
        await using ContextMenuTwin twin = new(new NoRuntime());
        List<ContextMenuPress> decided = [];

        // Without a decider, or without a hand-over, a press runs.
        Assert.Equal("run", twin.Decide(ContextMenuTwin.PressPoint, ContextMenuJson));
        twin.PressDecider = press =>
        {
            decided.Add(press);
            return DecisionPolicy.Stop;
        };
        Assert.Equal("run", twin.Decide(ContextMenuTwin.PressPoint, ContextMenuJson));

        twin.OnPressing("""{"clientX":0,"clientY":0,"screenX":0,"screenY":0,"pageX":0,"pageY":0,"offsetX":0,"offsetY":0,"button":0,"buttons":0,"touches":[{"identifier":3,"clientX":10,"clientY":11,"screenX":0,"screenY":0,"pageX":10,"pageY":11}],"targetTouches":[],"changedTouches":[],"event":null}""");
        Assert.Equal("stop", twin.Decide(ContextMenuTwin.PressPoint, TouchStartJson));
        Assert.Equal("run", twin.Decide(ContextMenuTwin.PressPoint, TouchStartJson));
        Assert.Equal("run", twin.Decide("escape", TouchStartJson));

        ContextMenuPress press = Assert.Single(decided);
        Assert.Equal(3, Assert.Single(press.Touches!).Identifier);
        // The decision's own snapshot is the event the decider reads.
        Assert.Equal("touchstart", press.Event!.Type);
    }

    [Fact]
    [Trait("Requirement", "CTX-PTR-01")]
    public void Every_payload_round_trips_through_the_source_generated_context()
    {
        Assert.Equal(
            """{"x":1,"y":2,"pointerType":"touch"}""",
            JsonSerializer.Serialize(new ContextMenuOpenRequest(1, 2, "touch", null), BlazixJsonContext.Default.ContextMenuOpenRequest));
        Assert.Equal("{}", JsonSerializer.Serialize(new ContextMenuCancelOpen(null), BlazixJsonContext.Default.ContextMenuCancelOpen));
        Assert.Equal(
            """{"identifier":1,"clientX":2,"clientY":3,"screenX":4,"screenY":5,"pageX":6,"pageY":7}""",
            JsonSerializer.Serialize(new ContextMenuTouch(1, 2, 3, 4, 5, 6, 7), BlazixJsonContext.Default.ContextMenuTouch));
        Assert.Equal(
            """{"clientX":0,"clientY":0,"screenX":0,"screenY":0,"pageX":0,"pageY":0,"offsetX":0,"offsetY":0,"button":2,"buttons":0}""",
            JsonSerializer.Serialize(new ContextMenuPress(0, 0, 0, 0, 0, 0, 0, 0, 2, 0, null, null, null, null), BlazixJsonContext.Default.ContextMenuPress));
        Assert.Equal("""{"part":"root"}""", JsonSerializer.Serialize(new ContextMenuOptions(), BlazixJsonContext.Default.ContextMenuOptions));
    }

    /// <summary>A runtime the callback tests never reach: they call the twin's methods directly.</summary>
    private sealed class NoRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new NotSupportedException();
    }
}

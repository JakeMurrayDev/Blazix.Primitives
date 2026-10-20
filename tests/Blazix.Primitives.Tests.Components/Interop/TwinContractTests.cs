using System.Text.Json;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components.Interop;

/// <summary>
/// Twin contract tests (ADR 06 §1, L2): bUnit's JS interop mock records what C# asked the module to do, and the
/// tests drive the twin's <c>[JSInvokable]</c> callbacks directly. No JavaScript runs.
/// </summary>
public sealed class TwinContractTests
{
    [Fact]
    [Trait("Verifies", "ADR06-3")]
    public async Task Attach_imports_the_module_once_and_returns_a_handle_whose_update_and_dispose_are_recorded()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(TestTwin.ModulePath);
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true);
        handle.SetupVoid("dispose", _ => true);
        await using TestTwin twin = new(context.JSInterop.JSRuntime);

        TwinHandle<TestTwinOptions> attached = await twin.AttachAsync("base-ui-1", new TestTwinOptions { Style = "color: red", Threshold = 3 });
        await attached.UpdateAsync(new TestTwinOptions { Style = "color: blue", Threshold = 4 });
        await attached.DisposeAsync();

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Equal("base-ui-1", attach.Arguments[0]);
        Assert.Equal("""{"style":"color: red","threshold":3}""", attach.Arguments[1]);
        Assert.IsType<DotNetObjectReference<TestTwin>>(attach.Arguments[2]);

        JSRuntimeInvocation update = Assert.Single(handle.Invocations["update"]);
        Assert.Equal("""{"style":"color: blue","threshold":4}""", update.Arguments[0]);

        Assert.Single(handle.Invocations["dispose"]);
    }

    [Fact]
    public async Task The_module_is_imported_lazily_on_the_first_attach_and_only_once()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(TestTwin.ModulePath);
        module.SetupModule("attach", _ => true);
        await using TestTwin twin = new(context.JSInterop.JSRuntime);

        Assert.Empty(context.JSInterop.Invocations);

        await twin.AttachAsync("base-ui-1", new TestTwinOptions());
        await twin.AttachAsync("base-ui-2", new TestTwinOptions());

        Assert.Single(context.JSInterop.Invocations["import"]);
        Assert.Equal(2, module.Invocations["attach"].Count);
    }

    [Fact]
    public async Task A_repeated_attach_of_the_same_element_is_a_no_op_that_returns_the_same_handle()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(TestTwin.ModulePath);
        module.SetupModule("attach", _ => true);
        await using TestTwin twin = new(context.JSInterop.JSRuntime);

        TwinHandle<TestTwinOptions> first = await twin.AttachAsync("base-ui-1", new TestTwinOptions());
        TwinHandle<TestTwinOptions> second = await twin.AttachAsync("base-ui-1", new TestTwinOptions { Style = "ignored" });

        Assert.Same(first, second);
        Assert.Single(module.Invocations["attach"]);
    }

    [Fact]
    public async Task Dispose_is_idempotent_on_the_handle_and_frees_the_element_for_a_new_attach()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(TestTwin.ModulePath);
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("dispose", _ => true);
        await using TestTwin twin = new(context.JSInterop.JSRuntime);

        TwinHandle<TestTwinOptions> first = await twin.AttachAsync("base-ui-1", new TestTwinOptions());
        await first.DisposeAsync();
        await first.DisposeAsync();
        TwinHandle<TestTwinOptions> again = await twin.AttachAsync("base-ui-1", new TestTwinOptions());

        Assert.Single(handle.Invocations["dispose"]);
        Assert.NotSame(first, again);
        Assert.Equal(2, module.Invocations["attach"].Count);
    }

    [Fact]
    public async Task Disposing_the_twin_disposes_every_live_handle_once()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(TestTwin.ModulePath);
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("dispose", _ => true);
        TestTwin twin = new(context.JSInterop.JSRuntime);
        TwinHandle<TestTwinOptions> first = await twin.AttachAsync("base-ui-1", new TestTwinOptions());
        await twin.AttachAsync("base-ui-2", new TestTwinOptions());
        await first.DisposeAsync();

        await twin.DisposeAsync();
        await twin.DisposeAsync();

        Assert.Equal(2, handle.Invocations["dispose"].Count);
    }

    [Fact]
    public async Task Attach_accepts_an_ElementReference_and_passes_it_as_the_element()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(TestTwin.ModulePath);
        module.SetupModule("attach", _ => true);
        await using TestTwin twin = new(context.JSInterop.JSRuntime);
        Microsoft.AspNetCore.Components.ElementReference element = new("element-1");

        await twin.AttachAsync(element, new TestTwinOptions());
        await twin.AttachAsync(element, new TestTwinOptions());

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Equal(element, attach.Arguments[0]);
    }

    [Fact]
    public void Decide_deserialises_the_event_info_runs_the_chain_and_returns_the_verdict_as_its_literal()
    {
        using BunitContext context = new();
        TestTwin twin = new(context.JSInterop.JSRuntime) { NextVerdict = DecisionPolicy.PreventAndStop };
        const string eventJson = """
            {"kind":"keyboard","type":"keydown","key":"Escape","code":"Escape","altKey":false,"ctrlKey":false,
             "metaKey":false,"shiftKey":true,"detail":0,"buttons":0,"button":-1,
             "target":{"tagName":"button","id":"base-ui-1"},"defaultPrevented":false,"timeStamp":1234.5}
            """;

        string verdict = twin.Decide("escape", eventJson);

        Assert.Equal("prevent-and-stop", verdict);
        (string point, JsEventInfo e) = Assert.Single(twin.Decisions);
        Assert.Equal("escape", point);
        Assert.Equal("keyboard", e.Kind);
        Assert.Equal("keydown", e.Type);
        Assert.Equal("Escape", e.Key);
        Assert.True(e.ShiftKey);
        Assert.False(e.AltKey);
        Assert.Equal("button", e.Target?.TagName);
        Assert.Equal("base-ui-1", e.Target?.Id);
        Assert.Equal(1234.5, e.TimeStamp);
    }

    [Theory]
    [InlineData(DecisionPolicy.Run, "run")]
    [InlineData(DecisionPolicy.Stop, "stop")]
    [InlineData(DecisionPolicy.Start, "start")]
    [InlineData(DecisionPolicy.PreventAndStop, "prevent-and-stop")]
    public void Every_decision_policy_has_the_kebab_case_literal_the_JavaScript_helper_expects(DecisionPolicy policy, string literal)
    {
        using BunitContext context = new();
        TestTwin twin = new(context.JSInterop.JSRuntime) { NextVerdict = policy };

        Assert.Equal(literal, twin.Decide("any", """{"kind":"base-ui","type":"base-ui"}"""));
    }

    [Fact]
    public void Ping_answers_true_so_JavaScript_can_detect_synchronous_invocation_support_at_run_time()
    {
        using BunitContext context = new();
        TestTwin twin = new(context.JSInterop.JSRuntime);

        Assert.True(twin.Ping());
    }

    [Fact]
    public void A_callback_payload_arrives_as_JSON_and_the_twin_exposes_it_typed()
    {
        using BunitContext context = new();
        TestTwin twin = new(context.JSInterop.JSRuntime);

        twin.Report("""{"name":"flip","count":2}""");

        TestTwinPayload payload = Assert.Single(twin.Reported);
        Assert.Equal(new TestTwinPayload("flip", 2), payload);
    }

    [Fact]
    public void The_library_JSON_context_covers_the_event_info_record()
    {
        JsEventInfo roundTrip = JsonSerializer.Deserialize(
            JsonSerializer.Serialize(new JsEventInfo { Kind = "pointer", Type = "pointerdown", PointerType = "mouse", Buttons = 1 }, BlazixJsonContext.Default.JsEventInfo),
            BlazixJsonContext.Default.JsEventInfo)!;

        Assert.Equal("pointer", roundTrip.Kind);
        Assert.Equal("mouse", roundTrip.PointerType);
        Assert.Equal(1, roundTrip.Buttons);
    }
}

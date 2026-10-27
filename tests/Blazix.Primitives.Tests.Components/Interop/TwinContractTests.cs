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
        BunitJSModuleInterop handle = SetupHandle(module);
        await using TestTwin twin = new(context.JSInterop.JSRuntime);

        TwinHandle<TestTwinOptions> attached = await twin.AttachAsync("base-ui-1", new TestTwinOptions { Style = "color: red", Threshold = 3 });
        await attached.UpdateAsync(new TestTwinOptions { Style = "color: blue", Threshold = 4 });
        await attached.DisposeAsync();

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Equal("base-ui-1", attach.Arguments[0]);
        Assert.Equal("""{"threshold":3,"style":"color: red"}""", attach.Arguments[1]);
        Assert.IsType<DotNetObjectReference<TestTwin>>(attach.Arguments[2]);

        JSRuntimeInvocation update = Assert.Single(handle.Invocations["update"]);
        Assert.Equal("""{"threshold":4,"style":"color: blue"}""", update.Arguments[0]);

        Assert.Single(handle.Invocations["dispose"]);
    }

    [Fact]
    public async Task The_module_is_imported_lazily_on_the_first_attach_and_only_once()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(TestTwin.ModulePath);
        SetupHandle(module);
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
        SetupHandle(module);
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
        BunitJSModuleInterop handle = SetupHandle(module);
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
        BunitJSModuleInterop handle = SetupHandle(module);
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
        SetupHandle(module);
        await using TestTwin twin = new(context.JSInterop.JSRuntime);
        Microsoft.AspNetCore.Components.ElementReference element = new("element-1");

        await twin.AttachAsync(element, new TestTwinOptions());
        await twin.AttachAsync(element, new TestTwinOptions());

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Equal(element, attach.Arguments[0]);
    }

    // ADR 04 Amendments 2026-10-27 (element-typed option values cross as interop arguments, settled by the orchestrator
    // 2026-10-27): the references follow the options string as one array, which Blazor revives, and an element-typed
    // option is the index of its reference.
    [Fact]
    [Trait("Requirement", "ADR04-element-typed-options")]
    public async Task Attach_and_update_pass_the_references_after_the_options_and_an_option_names_its_reference_by_index()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(TestTwin.ModulePath);
        BunitJSModuleInterop handle = SetupHandle(module);
        ScriptHeldReference scriptHeld = new();
        await using TestTwin twin = new(context.JSInterop.JSRuntime);
        Microsoft.AspNetCore.Components.ElementReference target = new("element-7");

        TwinReferences first = new();
        int targetIndex = first.Add(target);
        int scriptHeldIndex = first.Add(scriptHeld);
        TwinHandle<TestTwinOptions> attached = await twin.AttachAsync("base-ui-1", new TestTwinOptions { TargetRef = targetIndex }, first);
        TwinReferences second = new();
        await attached.UpdateAsync(new TestTwinOptions { TargetRef = second.Add(scriptHeld) }, second);
        await twin.AttachAsync("base-ui-2", new TestTwinOptions(), new TwinReferences());

        Assert.Equal(0, targetIndex);
        Assert.Equal(1, scriptHeldIndex);
        JSRuntimeInvocation attach = module.Invocations["attach"][0];
        Assert.Equal(4, attach.Arguments.Count);
        Assert.Equal("""{"threshold":0,"targetRef":0}""", attach.Arguments[1]);
        object[] sent = Assert.IsType<object[]>(attach.Arguments[3]);
        Assert.Equal(2, sent.Length);
        Assert.Equal(target, sent[0]);
        Assert.Same(scriptHeld, sent[1]);

        JSRuntimeInvocation update = Assert.Single(handle.Invocations["update"]);
        Assert.Equal(2, update.Arguments.Count);
        Assert.Equal("""{"threshold":0,"targetRef":0}""", update.Arguments[0]);
        Assert.Same(scriptHeld, Assert.Single(Assert.IsType<object[]>(update.Arguments[1])));

        // An empty list sends no array: the module's references argument is then absent.
        Assert.Equal(3, module.Invocations["attach"][1].Arguments.Count);
    }

    [Fact]
    [Trait("Requirement", "ADR04-element-typed-options")]
    public async Task An_update_without_references_sends_only_the_options_after_an_attach_that_sent_some()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(TestTwin.ModulePath);
        BunitJSModuleInterop handle = SetupHandle(module);
        await using TestTwin twin = new(context.JSInterop.JSRuntime);
        TwinReferences references = new();
        TwinHandle<TestTwinOptions> attached = await twin.AttachAsync("base-ui-1", new TestTwinOptions { TargetRef = references.Add(new Microsoft.AspNetCore.Components.ElementReference("element-7")) }, references);

        await attached.UpdateAsync(new TestTwinOptions { Threshold = 2 });

        Assert.Equal(4, Assert.Single(module.Invocations["attach"]).Arguments.Count);
        JSRuntimeInvocation update = Assert.Single(handle.Invocations["update"]);
        Assert.Equal("""{"threshold":2}""", Assert.Single(update.Arguments));
    }

    [Fact]
    public void Decide_deserialises_the_event_info_runs_the_chain_and_returns_the_verdict_as_its_literal()
    {
        using BunitContext context = new();
        TestTwin twin = new(context.JSInterop.JSRuntime) { NextVerdict = DecisionPolicy.PreventAndStop };
        const string eventJson = """
            {"kind":"KeyboardEvent","type":"keydown","key":"Escape","code":"Escape","altKey":false,"ctrlKey":false,
             "metaKey":false,"shiftKey":true,"detail":0,"buttons":0,
             "target":{"id":"base-ui-1","tagName":"BUTTON"},"defaultPrevented":false,"timeStamp":1234.5}
            """;

        string verdict = twin.Decide("escape", eventJson);

        Assert.Equal("prevent-and-stop", verdict);
        (string point, EventSnapshot e) = Assert.Single(twin.Decisions);
        Assert.Equal("escape", point);
        Assert.Equal(EventKind.KeyboardEvent, e.Kind);
        Assert.True(e.IsInstanceOf(EventKind.UIEvent));
        Assert.Equal("keydown", e.Type);
        Assert.Equal("Escape", e.Key);
        Assert.True(e.ShiftKey);
        Assert.False(e.AltKey);
        Assert.Equal("BUTTON", e.Target?.TagName);
        Assert.Equal("base-ui-1", e.Target?.Id);
        Assert.Equal(1234.5, e.TimeStamp);
    }

    [Theory]
    [InlineData(nameof(DecisionPolicy.Run), "run")]
    [InlineData(nameof(DecisionPolicy.Stop), "stop")]
    [InlineData(nameof(DecisionPolicy.Start), "start")]
    [InlineData(nameof(DecisionPolicy.PreventAndStop), "prevent-and-stop")]
    public void Every_decision_policy_has_the_kebab_case_literal_the_JavaScript_helper_expects(string policy, string literal)
    {
        using BunitContext context = new();
        TestTwin twin = new(context.JSInterop.JSRuntime) { NextVerdict = Enum.Parse<DecisionPolicy>(policy) };

        Assert.Equal(literal, twin.Decide("any", """{"kind":"Event","type":"base-ui"}"""));
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
    public void The_library_JSON_context_covers_the_event_snapshot()
    {
        EventSnapshot roundTrip = JsonSerializer.Deserialize(
            JsonSerializer.Serialize(new EventSnapshot { Kind = EventKind.PointerEvent, Type = "pointerdown", PointerType = "mouse", Buttons = 1 }, BlazixJsonContext.Default.EventSnapshot),
            BlazixJsonContext.Default.EventSnapshot)!;

        Assert.Equal(EventKind.PointerEvent, roundTrip.Kind);
        Assert.Equal("mouse", roundTrip.PointerType);
        Assert.Equal(1, roundTrip.Buttons);
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

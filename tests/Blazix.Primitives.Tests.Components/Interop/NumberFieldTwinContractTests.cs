using System.Text.Json;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components.Interop;

/// <summary>
/// Twin contract tests of the <c>number-field</c> module, 6a's surface (ADR 06 §1, L2; issue 277, design points 1, 2 and 7,
/// settled by the orchestrator 2026-11-18): the options C# sends on <c>attach</c> and <c>update</c>, the focus and blur
/// reports, and the <c>input-focus</c> and <c>input-blur</c> decision points.
/// </summary>
public sealed class NumberFieldTwinContractTests
{
    private const string BlurJson = """
        {"kind":"FocusEvent","type":"blur","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
         "target":{"id":"base-ui-2","tagName":"INPUT"},"defaultPrevented":false,"timeStamp":30}
        """;

    [Fact(DisplayName = "port: attach imports the number-field module and sends the text and the echoed revision")]
    [Trait("Requirement", "NUM-STA-05")]
    [Trait("Requirement", "NUM-STA-06")]
    public async Task Attach_imports_the_module_and_sends_the_text_and_the_echoed_revision()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule("./_content/Blazix.Primitives/js/number-field.js");
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        await using NumberFieldTwin twin = new(context.JSInterop.JSRuntime);

        TwinHandle<NumberFieldOptions> attached = await twin.AttachAsync("base-ui-2", new NumberFieldOptions { Value = "1,234" });
        await attached.UpdateAsync(new NumberFieldOptions { Value = "1,235", Revision = 3 });

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Equal("""{"value":"1,234","revision":0}""", attach.Arguments[1]);
        Assert.IsType<DotNetObjectReference<NumberFieldTwin>>(attach.Arguments[2]);
        Assert.Equal("""{"value":"1,235","revision":3}""", Assert.Single(handle.Invocations["update"]).Arguments[0]);
    }

    [Fact(DisplayName = "port: the focus and blur reports raise their events with the text, the revision and the event")]
    [Trait("Requirement", "NUM-FOC-05")]
    [Trait("Requirement", "NUM-EVT-06")]
    public void The_focus_and_blur_reports_raise_their_events()
    {
        using BunitContext context = new();
        NumberFieldTwin twin = new(context.JSInterop.JSRuntime);
        List<string> log = [];
        twin.InputFocused += e => log.Add($"focus:{e.Type}");
        twin.InputBlurred += r => log.Add($"blur:{r.Text}:{r.Revision}:{r.Event.Type}");

        twin.OnInputFocus("""{"event":{"kind":"FocusEvent","type":"focus"}}""");
        twin.OnInputBlur($$"""{"text":"7","revision":1,"event":{{BlurJson}}}""");

        Assert.Equal(["focus:focus", "blur:7:1:blur"], log);
        Assert.Throws<JsonException>(() => twin.OnInputBlur("null"));
    }

    [Fact(DisplayName = "port: the input-focus and input-blur points run their deciders, and every other point runs")]
    [Trait("Requirement", "NUM-EVT-14")]
    public void The_input_focus_and_input_blur_points_run_their_deciders()
    {
        using BunitContext context = new();
        List<string> seen = [];
        NumberFieldTwin twin = new(context.JSInterop.JSRuntime)
        {
            FocusDecider = e => { seen.Add(e.Type); return DecisionPolicy.Stop; },
            BlurDecider = e => { seen.Add(e.Type); return DecisionPolicy.Run; },
        };

        Assert.Equal("input-focus", NumberFieldTwin.InputFocusPoint);
        Assert.Equal("input-blur", NumberFieldTwin.InputBlurPoint);
        Assert.Equal("stop", twin.Decide(NumberFieldTwin.InputFocusPoint, """{"kind":"FocusEvent","type":"focus"}"""));
        Assert.Equal("run", twin.Decide(NumberFieldTwin.InputBlurPoint, BlurJson));
        Assert.Equal("run", twin.Decide("escape", BlurJson));
        Assert.Equal(["focus", "blur"], seen);
    }
}

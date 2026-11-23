using System.Text.Json;
using Iodine.Headless.Interop;
using Bunit;
using Microsoft.JSInterop;

namespace Iodine.Headless.Tests.Components.Interop;

/// <summary>
/// Twin contract tests of the <c>otp-field</c> module (ADR 06 §1, L2; issue 278, M6 track 7; design points 1–7, chosen by the
/// user 2026-11-19): the options C# sends on <c>attach</c> and <c>update</c>, the edit, focus and blur reports, the edit's
/// synchronous handover, and the five decision points <c>edit</c>, <c>key</c>, <c>press</c>, <c>focus</c> and <c>blur</c>.
/// </summary>
public sealed class OtpFieldTwinContractTests
{
    private const string ModulePath = "./_content/Iodine.Headless/js/otp-field.js";

    private const string InputJson = """
        {"kind":"InputEvent","type":"input","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
         "target":{"id":"code-3","tagName":"INPUT"},"defaultPrevented":false,"timeStamp":30}
        """;

    [Fact(DisplayName = "port: attach imports the otp-field module and sends the slots, the length, the value, the revision and the active slot")]
    [Trait("Requirement", "OTP-DOM-08")]
    [Trait("Requirement", "OTP-FOC-01")]
    public async Task Attach_sends_the_slots_the_length_the_value_the_revision_and_the_active_slot()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(ModulePath);
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        await using OtpFieldTwin twin = new(context.JSInterop.JSRuntime);

        TwinHandle<OtpFieldOptions> attached = await twin.AttachAsync("root", new OtpFieldOptions { Slots = ["code", "code-2"], Length = 2, Value = "1", ActiveIndex = 1 });
        await attached.UpdateAsync(new OtpFieldOptions { Slots = ["code", "code-2"], Length = 2, Value = "12", Revision = 3, ActiveIndex = 1 });

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Equal("""{"slots":["code","code-2"],"length":2,"value":"1","revision":0,"activeIndex":1,"submitRevision":0}""", attach.Arguments[1]);
        Assert.IsType<DotNetObjectReference<OtpFieldTwin>>(attach.Arguments[2]);
        Assert.Equal("""{"slots":["code","code-2"],"length":2,"value":"12","revision":3,"activeIndex":1,"submitRevision":0}""", Assert.Single(handle.Invocations["update"]).Arguments[0]);
    }

    [Fact(DisplayName = "port: the rollback, the queued focus, the states, the optimistic pattern, the submit and the decided points are sent only when set")]
    [Trait("Requirement", "OTP-FOC-10")]
    [Trait("Requirement", "OTP-EVT-13")]
    [Trait("Requirement", "OTP-I18N-01")]
    public async Task The_optional_options_are_sent_only_when_set()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(ModulePath);
        module.SetupModule("attach", _ => true).SetupVoid("dispose", _ => true).SetVoidResult();
        await using OtpFieldTwin twin = new(context.JSInterop.JSRuntime);

        await twin.AttachAsync("root", new OtpFieldOptions
        {
            Slots = ["code"],
            Length = 1,
            Value = string.Empty,
            Revision = 2,
            Rollback = true,
            ActiveIndex = 0,
            Focus = new OtpFieldFocusRequest(0, 4),
            Disabled = true,
            ReadOnly = true,
            Rtl = true,
            Optimistic = true,
            SlotPattern = @"\d{1}",
            SubmitRevision = 1,
            Form = "checkout",
            Points = [OtpFieldTwin.KeyPoint, OtpFieldTwin.PressPoint],
            DevelopmentChecks = true,
        });

        Assert.Equal(
            """{"slots":["code"],"length":1,"value":"","revision":2,"rollback":true,"activeIndex":0,"focus":{"index":0,"revision":4},"disabled":true,"readOnly":true,"rtl":true,"optimistic":true,"slotPattern":"\\d{1}","submitRevision":1,"form":"checkout","points":["key","press"],"developmentChecks":true}""",
            Assert.Single(module.Invocations["attach"]).Arguments[1]);
    }

    [Fact(DisplayName = "port: the edit, focus, blur and unreadable-paste reports raise their events")]
    [Trait("Requirement", "OTP-KEY-14")]
    [Trait("Requirement", "OTP-FOC-07")]
    [Trait("Requirement", "OTP-EDGE-05")]
    public void The_edit_focus_blur_and_unreadable_paste_reports_raise_their_events()
    {
        using BunitContext context = new();
        OtpFieldTwin twin = new(context.JSInterop.JSRuntime);
        List<string> log = [];
        twin.Edited += r => log.Add($"edit:{r.Index}:{r.Kind}:{r.Raw}:{r.Advanced}:{r.Revision}:{r.Event.Type}");
        twin.SlotFocused += r => log.Add($"focus:{r.Index}:{r.Event.Type}");
        twin.SlotBlurred += r => log.Add($"blur:{r.Index}:{r.Outside}:{r.Event.Type}");
        twin.PasteUnreadable += r => log.Add($"unreadable:{r.Index}");

        twin.OnEdit($$"""{"index":2,"kind":"input","raw":"7","advanced":true,"revision":5,"event":{{InputJson}}}""");
        twin.OnFocus("""{"index":1,"event":{"kind":"FocusEvent","type":"focus"}}""");
        twin.OnBlur("""{"index":1,"outside":true,"event":{"kind":"FocusEvent","type":"blur"}}""");
        twin.OnPasteUnreadable("""{"index":0}""");

        Assert.Equal(["edit:2:input:7:True:5:input", "focus:1:focus", "blur:1:True:blur", "unreadable:0"], log);
        Assert.Throws<JsonException>(() => twin.OnEdit("null"));
    }

    [Fact(DisplayName = "port: the edit handover is held for the edit point, and the five points run their deciders")]
    [Trait("Requirement", "OTP-KEY-01")]
    [Trait("Requirement", "OTP-KEY-13")]
    [Trait("Requirement", "OTP-PTR-02")]
    [Trait("Requirement", "OTP-FOC-08")]
    [Trait("Requirement", "OTP-EVT-16")]
    public void The_edit_handover_is_held_for_the_edit_point()
    {
        using BunitContext context = new();
        List<string> seen = [];
        OtpFieldTwin twin = null!;
        twin = new OtpFieldTwin(context.JSInterop.JSRuntime)
        {
            EditDecider = e => { seen.Add($"edit:{twin.PendingEdit?.Index}:{e.Type}"); return DecisionPolicy.Run; },
            KeyDecider = e => { seen.Add("key"); return DecisionPolicy.Stop; },
            PressDecider = e => { seen.Add("press"); return DecisionPolicy.Stop; },
            FocusDecider = e => { seen.Add("focus"); return DecisionPolicy.Run; },
            BlurDecider = e => { seen.Add("blur"); return DecisionPolicy.Stop; },
        };

        Assert.Equal(["edit", "key", "press", "focus", "blur"], [OtpFieldTwin.EditPoint, OtpFieldTwin.KeyPoint, OtpFieldTwin.PressPoint, OtpFieldTwin.FocusPoint, OtpFieldTwin.BlurPoint]);

        twin.OnEditHandover($$"""{"index":2,"kind":"input","raw":"7","advanced":false,"revision":6,"event":{{InputJson}}}""");
        Assert.Equal("run", twin.Decide(OtpFieldTwin.EditPoint, InputJson));
        Assert.Null(twin.PendingEdit);
        Assert.Equal("stop", twin.Decide(OtpFieldTwin.KeyPoint, InputJson));
        Assert.Equal("stop", twin.Decide(OtpFieldTwin.PressPoint, InputJson));
        Assert.Equal("run", twin.Decide(OtpFieldTwin.FocusPoint, InputJson));
        Assert.Equal("stop", twin.Decide(OtpFieldTwin.BlurPoint, InputJson));
        Assert.Equal("run", twin.Decide("escape", InputJson));
        Assert.Equal(["edit:2:input", "key", "press", "focus", "blur"], seen);
    }
}

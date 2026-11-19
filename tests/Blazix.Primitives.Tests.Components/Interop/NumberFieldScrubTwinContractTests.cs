using System.Text.Json;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components.Interop;

/// <summary>
/// Twin contract tests of the <c>number-field</c> module's scrub role (ADR 06 §1, L2; issue 277, 6b; design points 1 (a)
/// and 8 (a)): the scrub area's and the cursor's options, the four scrub reports and the <c>scrub-start</c> point.
/// </summary>
public sealed class NumberFieldScrubTwinContractTests
{
    private const string PointerJson = """
        {"kind":"PointerEvent","type":"pointermove","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":true,"pointerType":"mouse","detail":0,"buttons":1,
         "target":{"id":null,"tagName":"SPAN"},"defaultPrevented":false,"timeStamp":30}
        """;

    [Fact(DisplayName = "port: attach imports the number-field module with the scrub area's part and its unset options omitted")]
    [Trait("Requirement", "NUM-API-24")]
    [Trait("Requirement", "NUM-STR-05")]
    public async Task Attach_sends_the_scrub_areas_part_and_omits_unset_options()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule("./_content/Blazix.Primitives/js/number-field.js");
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        await using NumberFieldScrubTwin twin = new(context.JSInterop.JSRuntime);

        TwinHandle<NumberFieldScrubOptions> attached = await twin.AttachAsync("scrub", new NumberFieldScrubOptions { Part = NumberFieldScrubTwin.ScrubPart });
        await attached.UpdateAsync(new NumberFieldScrubOptions
        {
            Part = NumberFieldScrubTwin.ScrubPart,
            Vertical = true,
            PixelSensitivity = 5,
            TeleportDistance = 100,
            ReadOnly = true,
        });

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Equal("""{"part":"scrub"}""", attach.Arguments[1]);
        Assert.IsType<DotNetObjectReference<NumberFieldScrubTwin>>(attach.Arguments[2]);
        Assert.Equal(
            """{"part":"scrub","vertical":true,"pixelSensitivity":5,"teleportDistance":100,"readOnly":true}""",
            Assert.Single(handle.Invocations["update"]).Arguments[0]);
    }

    [Fact(DisplayName = "port: the cursor's options carry its part and the consumer style it hands over")]
    [Trait("Requirement", "NUM-DOM-13")]
    public async Task The_cursors_options_carry_its_part_and_style()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule("./_content/Blazix.Primitives/js/number-field.js");
        module.SetupModule("attach", _ => true);
        await using NumberFieldScrubTwin twin = new(context.JSInterop.JSRuntime);

        await twin.AttachAsync("cursor", new NumberFieldScrubOptions { Part = NumberFieldScrubTwin.CursorPart, Style = "position: fixed" });

        Assert.Equal("""{"style":"position: fixed","part":"cursor"}""", Assert.Single(module.Invocations["attach"]).Arguments[1]);
    }

    [Fact(DisplayName = "port: the scrub reports raise their events with the cursor flag, the denial, the delta and the revision")]
    [Trait("Requirement", "NUM-PTR-10")]
    [Trait("Requirement", "NUM-PTR-11")]
    [Trait("Requirement", "NUM-PTR-13")]
    [Trait("Requirement", "NUM-EDGE-09")]
    public void The_scrub_reports_raise_their_events()
    {
        using BunitContext context = new();
        NumberFieldScrubTwin twin = new(context.JSInterop.JSRuntime);
        List<string> log = [];
        twin.ScrubStarted += r => log.Add($"start:{r.Cursor}:{r.Event.Type}");
        twin.LockSettled += r => log.Add($"lock:{r.Denied}");
        twin.ScrubStepped += r => log.Add($"step:{r.Delta}:{r.Revision}:{r.Event.ShiftKey}");
        twin.ScrubEnded += r => log.Add($"end:{r.Event.Type}");

        twin.OnScrubStart("""{"cursor":true,"event":{"kind":"PointerEvent","type":"pointerdown"}}""");
        twin.OnScrubLock("""{"denied":true}""");
        twin.OnScrubStep($$"""{"delta":-4,"revision":3,"event":{{PointerJson}}}""");
        twin.OnScrubEnd("""{"event":{"kind":"PointerEvent","type":"pointerup"}}""");

        Assert.Equal(["start:True:pointerdown", "lock:True", "step:-4:3:True", "end:pointerup"], log);
        Assert.Throws<JsonException>(() => twin.OnScrubStep("null"));
    }

    [Fact(DisplayName = "port: the scrub-start point runs its decider, and every other point runs")]
    [Trait("Requirement", "NUM-PTR-19")]
    public void The_scrub_start_point_runs_its_decider()
    {
        using BunitContext context = new();
        List<string> seen = [];
        NumberFieldScrubTwin twin = new(context.JSInterop.JSRuntime)
        {
            StartDecider = e => { seen.Add(e.Type); return DecisionPolicy.Stop; },
        };

        Assert.Equal("scrub-start", NumberFieldScrubTwin.ScrubStartPoint);
        Assert.Equal("stop", twin.Decide(NumberFieldScrubTwin.ScrubStartPoint, PointerJson));
        Assert.Equal("run", twin.Decide("input-key", PointerJson));
        Assert.Equal(["pointermove"], seen);
    }
}

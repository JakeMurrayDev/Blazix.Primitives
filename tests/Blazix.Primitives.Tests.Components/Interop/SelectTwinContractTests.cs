using System.Text.Json;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components.Interop;

/// <summary>
/// Twin contract tests of the <c>select</c> module (ADR 06 §1, L2; ADR 04 §1's <c>select</c> row, M5 track 9): bUnit's JS
/// interop mock records every field C# sends on <c>attach</c> and <c>update</c>, and the tests drive
/// <see cref="SelectTwin"/>'s <c>[JSInvokable]</c> callbacks and its <c>activate</c> decision point.
/// </summary>
public sealed class SelectTwinContractTests
{
    private const string ModulePath = "./_content/Blazix.Primitives/js/select.js";

    private const string ClickJson = """
        {"kind":"PointerEvent","type":"click","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":"mouse","detail":1,"buttons":0,
         "target":{"id":"base-ui-7","tagName":"DIV"},"defaultPrevented":false,"timeStamp":3.5}
        """;

    [Fact]
    [Trait("Requirement", "SEL-POS-07")]
    [Trait("Requirement", "SEL-PTR-11")]
    public async Task Attach_imports_the_select_module_and_sends_every_field_on_attach_and_update()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(ModulePath);
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        await using SelectTwin twin = new(context.JSInterop.JSRuntime);

        TwinHandle<SelectOptions> attached = await twin.AttachAsync("base-ui-1", new SelectOptions());
        await attached.UpdateAsync(new SelectOptions
        {
            Part = SelectTwin.PositionerPart,
            Open = true,
            Mounted = true,
            Ending = true,
            AlignItemWithTrigger = true,
            TouchOpened = true,
            HighlightItemOnHover = true,
            Rtl = true,
            HasSelectedValue = true,
            SelectedTextId = "base-ui-8",
            FirstTextId = "base-ui-9",
            ItemsRevision = 2,
            Trigger = 0,
            Popup = 1,
            List = 2,
            Value = 3,
            Decides = true,
        });
        await attached.UpdateAsync(new SelectOptions { Part = SelectTwin.ScrollArrowPart, Direction = "down", Positioner = 0 });
        await attached.DisposeAsync();

        // Null fields are not sent (the context's default), so an anchored positioner sends only its part and state.
        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Equal("base-ui-1", attach.Arguments[0]);
        Assert.Equal(
            """{"part":"positioner","open":false,"mounted":false}""",
            attach.Arguments[1]);
        Assert.IsType<DotNetObjectReference<SelectTwin>>(attach.Arguments[2]);

        IReadOnlyList<JSRuntimeInvocation> updates = handle.Invocations["update"];
        Assert.Equal(2, updates.Count);
        Assert.Equal(
            """{"part":"positioner","open":true,"mounted":true,"ending":true,"alignItemWithTrigger":true,"touchOpened":true,"highlightItemOnHover":true,"rtl":true,"hasSelectedValue":true,"selectedTextId":"base-ui-8","firstTextId":"base-ui-9","itemsRevision":2,"trigger":0,"popup":1,"list":2,"value":3,"decides":true}""",
            updates[0].Arguments[0]);
        Assert.Equal(
            """{"part":"scroll-arrow","open":false,"mounted":false,"positioner":0,"direction":"down"}""",
            updates[1].Arguments[0]);
        Assert.Single(handle.Invocations["dispose"]);
    }

    [Fact]
    [Trait("Requirement", "SEL-PTR-11")]
    [Trait("Requirement", "SEL-EVT-07")]
    public async Task The_activate_decision_reads_the_handed_over_item_once_and_answers_the_deciders_verdict()
    {
        await using SelectTwin twin = new(new NoRuntime());
        List<(string? ItemId, string Type)> seen = [];
        twin.ActivationDecider = snapshot =>
        {
            seen.Add((twin.PendingActivation?.ItemId, snapshot.Type));
            return snapshot.Type == "click" ? DecisionPolicy.Stop : DecisionPolicy.Run;
        };

        // Without a hand-over the decision lets the dispatch run and asks nothing.
        Assert.Equal("run", twin.Decide(SelectTwin.ActivatePoint, ClickJson));
        Assert.Empty(seen);

        twin.OnActivating("""{"itemId":"base-ui-7"}""");
        Assert.Equal("base-ui-7", twin.PendingActivation!.ItemId);
        Assert.Equal("stop", twin.Decide(SelectTwin.ActivatePoint, ClickJson));
        Assert.Null(twin.PendingActivation);

        twin.OnActivating("""{"itemId":"base-ui-8"}""");
        Assert.Equal("run", twin.Decide(SelectTwin.ActivatePoint, ClickJson.Replace("\"click\"", "\"mouseup\"", StringComparison.Ordinal)));

        // Another point answers run and consumes nothing.
        twin.OnActivating("""{"itemId":"base-ui-9"}""");
        Assert.Equal("run", twin.Decide("select", ClickJson));
        Assert.Equal("base-ui-9", twin.PendingActivation!.ItemId);

        Assert.Equal([("base-ui-7", "click"), ("base-ui-8", "mouseup")], seen);
    }

    [Fact]
    [Trait("Requirement", "SEL-POS-07")]
    [Trait("Requirement", "SEL-STA-29")]
    [Trait("Requirement", "SEL-PTR-04")]
    [Trait("Requirement", "SEL-DIS-06")]
    public async Task Each_report_raises_its_event_once_with_its_payload()
    {
        await using SelectTwin twin = new(new NoRuntime());
        List<SelectActivation> activations = [];
        List<bool> aligned = [];
        List<SelectArrowsVisibility> arrows = [];
        List<EventSnapshot> canceled = [];
        List<EventSnapshot> resized = [];
        List<bool> blurs = [];
        int placed = 0;
        int cleared = 0;
        twin.ActivationReported += activations.Add;
        twin.AlignedChanged += aligned.Add;
        twin.ArrowsChanged += arrows.Add;
        twin.OpenCanceled += canceled.Add;
        twin.WindowResized += resized.Add;
        twin.TriggerBlurred += blurs.Add;
        twin.Placed += () => placed++;
        twin.HighlightCleared += () => cleared++;

        twin.OnActivate($$"""{"itemId":"base-ui-7","event":{{ClickJson}}}""");
        twin.OnAlignedChange("""{"aligned":false}""");
        twin.OnArrowsChange("""{"up":true,"down":false}""");
        twin.OnPlaced();
        twin.OnClearHighlight();
        twin.OnCancelOpen(ClickJson.Replace("\"click\"", "\"mouseup\"", StringComparison.Ordinal));
        twin.OnWindowResize(ClickJson.Replace("\"click\"", "\"resize\"", StringComparison.Ordinal));
        twin.OnTriggerBlur("""{"intoPopup":true}""");

        SelectActivation activation = Assert.Single(activations);
        Assert.Equal("base-ui-7", activation.ItemId);
        Assert.Equal("click", activation.Event!.Type);
        Assert.Equal(3.5, activation.Event.TimeStamp);
        Assert.Equal([false], aligned);
        Assert.Equal([new SelectArrowsVisibility(true, false)], arrows);
        Assert.Equal(1, placed);
        Assert.Equal(1, cleared);
        Assert.Equal("mouseup", Assert.Single(canceled).Type);
        Assert.Equal("resize", Assert.Single(resized).Type);
        Assert.Equal([true], blurs);
    }

    [Fact]
    [Trait("Requirement", "SEL-POS-07")]
    public void Every_payload_round_trips_through_the_source_generated_context()
    {
        Assert.Equal("""{"itemId":"i"}""", JsonSerializer.Serialize(new SelectActivation("i"), BlazixJsonContext.Default.SelectActivation));
        Assert.Equal("""{"aligned":true}""", JsonSerializer.Serialize(new SelectAlignedChange(true), BlazixJsonContext.Default.SelectAlignedChange));
        Assert.Equal("""{"up":false,"down":true}""", JsonSerializer.Serialize(new SelectArrowsVisibility(false, true), BlazixJsonContext.Default.SelectArrowsVisibility));
        Assert.Equal("""{"intoPopup":false}""", JsonSerializer.Serialize(new SelectTriggerBlur(false), BlazixJsonContext.Default.SelectTriggerBlur));
        SelectArrowsVisibility arrows = new(true, true);
        Assert.Equal(arrows, JsonSerializer.Deserialize(JsonSerializer.Serialize(arrows, BlazixJsonContext.Default.SelectArrowsVisibility), BlazixJsonContext.Default.SelectArrowsVisibility));
    }

    /// <summary>A runtime the callback tests never reach: they call the twin's methods directly.</summary>
    private sealed class NoRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new NotSupportedException();
    }
}

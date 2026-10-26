using System.Text.Json;
using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components.Interop;

/// <summary>
/// Twin contract tests of the <c>tabs</c> module (ADR 06 §1, L2; ADR 04 Amendments 2026-10-26): bUnit's JS interop mock
/// records every field C# sends on <c>attach</c> and <c>update</c>, and the tests drive <see cref="TabsTwin"/>'s
/// <c>[JSInvokable]</c> callbacks and its <c>select</c> decision point.
/// </summary>
public sealed class TabsTwinContractTests
{
    private const string ModulePath = "./_content/Blazix.Primitives/js/tabs.js";

    private const string ClickJson = """
        {"kind":"PointerEvent","type":"click","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":"mouse","detail":1,"buttons":0,
         "target":{"id":"base-ui-7","tagName":"BUTTON"},"defaultPrevented":false,"timeStamp":3.5}
        """;

    [Fact]
    [Trait("Verifies", "ADR06-3")]
    [Trait("Requirement", "TAB-API-05")]
    [Trait("Requirement", "TAB-ANI-05")]
    public async Task Attach_imports_the_tabs_module_and_sends_every_field_on_attach_and_update()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(ModulePath);
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        await using TabsTwin twin = new(context.JSInterop.JSRuntime);

        TwinHandle<TabsOptions> attached = await twin.AttachAsync("base-ui-1", new TabsOptions());

        // A repeated attach of the same element is a no-op (ADR 06 Amendments 2026-10-18 rule 2; TAB-ANI-05).
        Assert.Same(attached, await twin.AttachAsync("base-ui-1", new TabsOptions { ActivateOnFocus = true }));
        await attached.UpdateAsync(new TabsOptions
        {
            Part = TabsTwin.IndicatorPart,
            Orientation = "vertical",
            ActivateOnFocus = true,
            Controlled = true,
            ReportClicks = true,
            ReportFocus = true,
            Hidden = false,
            Style = "color: red",
        });
        await attached.DisposeAsync();
        await attached.DisposeAsync();

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Equal("base-ui-1", attach.Arguments[0]);
        Assert.Equal(
            """{"part":"list","orientation":"horizontal","activateOnFocus":false,"controlled":false,"reportClicks":false,"reportFocus":false}""",
            attach.Arguments[1]);
        Assert.IsType<DotNetObjectReference<TabsTwin>>(attach.Arguments[2]);

        JSRuntimeInvocation update = Assert.Single(handle.Invocations["update"]);
        Assert.Equal(
            """{"part":"indicator","orientation":"vertical","activateOnFocus":true,"controlled":true,"reportClicks":true,"reportFocus":true,"hidden":false,"style":"color: red"}""",
            update.Arguments[0]);
        Assert.Single(handle.Invocations["dispose"]);
    }

    [Fact]
    [Trait("Requirement", "TAB-PTR-01")]
    [Trait("Requirement", "TAB-EVT-02")]
    public async Task The_selection_report_carries_the_tab_the_direction_and_the_event()
    {
        await using TabsTwin twin = new(new NoRuntime());
        List<TabsSelection> reported = [];
        twin.SelectionReported += reported.Add;

        twin.OnSelect($$"""{"tabId":"base-ui-7","direction":"right","event":{{ClickJson}}}""");

        TabsSelection selection = Assert.Single(reported);
        Assert.Equal("base-ui-7", selection.TabId);
        Assert.Equal("right", selection.Direction);
        Assert.Equal("click", selection.Event!.Type);
        Assert.Equal("base-ui-7", selection.Event.Target!.Id);
        Assert.Equal(3.5, selection.Event.TimeStamp);
    }

    [Fact]
    [Trait("Requirement", "TAB-PTR-09")]
    [Trait("Requirement", "TAB-KEY-10")]
    public async Task The_select_decision_reads_the_handed_over_selection_once_and_answers_the_deciders_verdict()
    {
        await using TabsTwin twin = new(new NoRuntime());
        List<(string? TabId, string Type)> seen = [];
        twin.SelectionDecider = snapshot =>
        {
            seen.Add((twin.PendingSelection?.TabId, snapshot.Type));
            return snapshot.Type == "click" ? DecisionPolicy.Stop : DecisionPolicy.Run;
        };

        // Without a hand-over the decision lets the dispatch run and asks nothing.
        Assert.Equal("run", twin.Decide(TabsTwin.SelectPoint, ClickJson));
        Assert.Empty(seen);

        twin.OnSelecting("""{"tabId":"base-ui-7","direction":"left"}""");
        Assert.Equal("left", twin.PendingSelection!.Direction);
        Assert.Equal("stop", twin.Decide(TabsTwin.SelectPoint, ClickJson));
        Assert.Null(twin.PendingSelection);

        twin.OnSelecting("""{"tabId":"base-ui-8","direction":"none"}""");
        Assert.Equal("run", twin.Decide(TabsTwin.SelectPoint, ClickJson.Replace("\"click\"", "\"focusin\"", StringComparison.Ordinal)));
        Assert.Equal("run", twin.Decide("activate", ClickJson));

        Assert.Equal([("base-ui-7", "click"), ("base-ui-8", "focusin")], seen);
    }

    [Fact]
    [Trait("Requirement", "TAB-STA-14")]
    [Trait("Requirement", "TAB-STA-20")]
    [Trait("Requirement", "TAB-PTR-09")]
    public async Task Each_report_raises_its_event_once_with_its_payload()
    {
        await using TabsTwin twin = new(new NoRuntime());
        List<TabsActivationDirection> directions = [];
        List<TabsIndicatorMeasurement> measurements = [];
        List<TabsTabEvent> tabEvents = [];
        twin.DirectionChanged += directions.Add;
        twin.MeasurementChanged += measurements.Add;
        twin.TabEventReported += tabEvents.Add;

        twin.OnDirectionChange("""{"direction":"down"}""");
        twin.OnDirectionChange("""{"direction":"sideways"}""");
        twin.OnMeasure("""{"position":{"left":1.5,"right":2,"top":3,"bottom":4},"size":{"width":10,"height":20.25}}""");
        twin.OnMeasure("""{"position":null,"size":null}""");
        twin.OnTabEvent($$"""{"tabId":"base-ui-9","event":{{ClickJson}}}""");

        Assert.Equal([TabsActivationDirection.Down, TabsActivationDirection.None], directions);
        Assert.Equal(2, measurements.Count);
        Assert.Equal(new TabsTabPosition(1.5, 2, 3, 4), measurements[0].Position);
        Assert.Equal(new TabsTabSize(10, 20.25), measurements[0].Size);
        Assert.Null(measurements[1].Position);
        Assert.Null(measurements[1].Size);
        Assert.Equal("base-ui-9", Assert.Single(tabEvents).TabId);
    }

    [Fact]
    [Trait("Requirement", "TAB-STA-20")]
    public void Every_payload_round_trips_through_the_source_generated_context()
    {
        TabsIndicatorMeasurement measurement = new(new TabsTabPosition(1, 2, 3, 4), new TabsTabSize(5, 6));
        string json = JsonSerializer.Serialize(measurement, BlazixJsonContext.Default.TabsIndicatorMeasurement);
        Assert.Equal("""{"position":{"left":1,"right":2,"top":3,"bottom":4},"size":{"width":5,"height":6}}""", json);
        Assert.Equal(measurement, JsonSerializer.Deserialize(json, BlazixJsonContext.Default.TabsIndicatorMeasurement));
        Assert.Equal(
            """{"tabId":"t","direction":"up"}""",
            JsonSerializer.Serialize(new TabsSelection("t", "up", null), BlazixJsonContext.Default.TabsSelection));
        Assert.Equal("""{"direction":"none"}""", JsonSerializer.Serialize(new TabsDirectionChange("none"), BlazixJsonContext.Default.TabsDirectionChange));
    }

    /// <summary>A runtime the callback tests never reach: they call the twin's methods directly.</summary>
    private sealed class NoRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new NotSupportedException();
    }
}

using Iodine.Headless.Interop;
using Bunit;
using Microsoft.JSInterop;

namespace Iodine.Headless.Tests.Components.Interop;

/// <summary>
/// Twin contract tests of the <c>slider</c> module (ADR 06 §1, L2; ADR 04 Amendments 2026-11-18, the module as built):
/// bUnit's JS interop mock records every field C# sends on <c>attach</c> and <c>update</c>, and the tests drive
/// <see cref="SliderTwin"/>'s <c>[JSInvokable]</c> callbacks and its two decision points.
/// </summary>
public sealed class SliderTwinContractTests
{
    private const string ModulePath = "./_content/Iodine.Headless/js/slider.js";

    private const string KeyJson = """
        {"kind":"KeyboardEvent","type":"keydown","key":"ArrowRight","code":"ArrowRight","altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":null,"detail":0,"buttons":0,
         "target":{"id":"base-ui-7","tagName":"INPUT"},"defaultPrevented":false,"timeStamp":3.5}
        """;

    [Fact]
    [Trait("Verifies", "ADR06-3")]
    [Trait("Requirement", "SLD-API-06")]
    [Trait("Requirement", "SLD-API-07")]
    public async Task Attach_imports_the_slider_module_and_sends_every_field_on_attach_and_update()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(ModulePath);
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        await using SliderTwin twin = new(context.JSInterop.JSRuntime);

        TwinHandle<SliderOptions> attached = await twin.AttachAsync("base-ui-1", new SliderOptions { Values = [25] });

        Assert.Same(attached, await twin.AttachAsync("base-ui-1", new SliderOptions()));
        await attached.UpdateAsync(new SliderOptions
        {
            Values = [20, 50],
            Range = true,
            Min = 10,
            Max = 90,
            Step = 5,
            LargeStep = 20,
            MinStepsBetweenValues = 2,
            Vertical = true,
            Rtl = true,
            Disabled = true,
            Inset = true,
            Collision = "swap",
            Thumbs = [new SliderThumbOptions { InputId = "a", Disabled = true, Style = "width: 10px" }, new SliderThumbOptions()],
            ControlRef = 0,
            Indicators = [new SliderIndicatorOptions { Ref = 1, Style = "color: red" }],
            Revision = 3,
            Rollback = true,
            RawDiffers = true,
            KeyHandlers = true,
            PressHandler = true,
        });
        await attached.DisposeAsync();
        await attached.DisposeAsync();

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Equal("base-ui-1", attach.Arguments[0]);
        Assert.Equal(
            """{"values":[25],"range":false,"min":0,"max":100,"step":1,"largeStep":10,"minStepsBetweenValues":0,"vertical":false,"rtl":false,"disabled":false,"inset":false,"collision":"push","thumbs":[],"indicators":[],"revision":0,"rollback":false,"rawDiffers":false,"keyHandlers":false,"pressHandler":false}""",
            attach.Arguments[1]);
        Assert.IsType<DotNetObjectReference<SliderTwin>>(attach.Arguments[2]);

        JSRuntimeInvocation update = Assert.Single(handle.Invocations["update"]);
        Assert.Equal(
            """{"values":[20,50],"range":true,"min":10,"max":90,"step":5,"largeStep":20,"minStepsBetweenValues":2,"vertical":true,"rtl":true,"disabled":true,"inset":true,"collision":"swap","thumbs":[{"inputId":"a","disabled":true,"style":"width: 10px"},{"disabled":false}],"controlRef":0,"indicators":[{"ref":1,"style":"color: red"}],"revision":3,"rollback":true,"rawDiffers":true,"keyHandlers":true,"pressHandler":true}""",
            update.Arguments[0]);
        Assert.Single(handle.Invocations["dispose"]);
    }

    [Fact]
    [Trait("Requirement", "SLD-EVT-01")]
    [Trait("Requirement", "SLD-EVT-11")]
    public async Task The_value_request_carries_the_reason_the_thumb_the_values_the_event_and_the_revision()
    {
        await using SliderTwin twin = new(new NoRuntime());
        List<SliderValueRequest> reported = [];
        twin.ValueRequested += reported.Add;

        twin.OnValueRequest($$"""{"reason":"keyboard","activeThumbIndex":1,"values":[20,51],"event":{{KeyJson}},"revision":4}""");

        SliderValueRequest request = Assert.Single(reported);
        Assert.Equal("keyboard", request.Reason);
        Assert.Equal(1, request.ActiveThumbIndex);
        Assert.Equal([20, 51], request.Values);
        Assert.Equal("keydown", request.Event.Type);
        Assert.Equal(4, request.Revision);
    }

    [Fact]
    [Trait("Requirement", "SLD-STA-08")]
    [Trait("Requirement", "SLD-FOC-04")]
    [Trait("Requirement", "SLD-API-22")]
    public async Task The_gesture_focus_and_key_reports_reach_their_events()
    {
        await using SliderTwin twin = new(new NoRuntime());
        List<SliderGestureReport> gestures = [];
        List<SliderFocusReport> focuses = [];
        List<SliderKeyReport> keys = [];
        twin.GestureChanged += gestures.Add;
        twin.FocusChanged += focuses.Add;
        twin.KeyReported += keys.Add;

        twin.OnGesture("""{"active":false,"dragging":false,"commit":true,"event":null}""");
        twin.OnFocusChange($$"""{"index":0,"focused":false,"outside":true,"event":{{KeyJson}}}""");
        twin.OnKeyDown($$"""{"index":2,"event":{{KeyJson}}}""");

        Assert.True(Assert.Single(gestures).Commit);
        Assert.True(Assert.Single(focuses).Outside);
        Assert.Equal(2, Assert.Single(keys).Index);
    }

    [Fact]
    [Trait("Requirement", "SLD-KEY-01")]
    [Trait("Requirement", "SLD-PTR-16")]
    public async Task The_key_and_press_points_answer_through_their_deciders_and_any_other_point_runs()
    {
        await using SliderTwin twin = new(new NoRuntime())
        {
            KeyDecider = _ => DecisionPolicy.Stop,
            PressDecider = _ => DecisionPolicy.Stop,
        };

        Assert.Equal("stop", twin.Decide(SliderTwin.KeyPoint, KeyJson));
        Assert.Equal("stop", twin.Decide(SliderTwin.PressPoint, KeyJson));
        Assert.Equal("run", twin.Decide("select", KeyJson));
        Assert.Equal("key", SliderTwin.KeyPoint);
        Assert.Equal("press", SliderTwin.PressPoint);
    }

    private sealed class NoRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new NotSupportedException();
    }
}

using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components.Interop;

/// <summary>
/// Twin contract tests of the <c>popup</c> module (ADR 06 §1, L2; ADR 04 Amendments 2026-10-27, the <c>popup</c> row;
/// M4 track 5): bUnit's JS interop mock records every field C# sends on <c>attach</c> and <c>update</c>, per role, and
/// the tests drive <see cref="PopupTwin"/>'s <c>[JSInvokable]</c> reports.
/// </summary>
public sealed class PopupTwinContractTests
{
    private const string ModulePath = "./_content/Blazix.Primitives/js/popup.js";

    [Fact]
    [Trait("Requirement", "PSH-ALG-14")]
    [Trait("Requirement", "PSH-ALG-17")]
    [Trait("Requirement", "PSH-ALG-21")]
    public async Task Attach_imports_the_popup_module_and_sends_every_field_of_each_role()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(ModulePath);
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        await using PopupTwin twin = new(context.JSInterop.JSRuntime);

        TwinHandle<PopupOptions> viewport = await twin.AttachAsync("viewport", new PopupOptions
        {
            Viewport = new PopupViewportOptions { Open = true, Mounted = true, PopupId = "base-ui-1", ActiveTriggerId = "trigger-1", ContentVersion = 2, Direction = "rtl" },
        });
        Assert.Same(viewport, await twin.AttachAsync("viewport", new PopupOptions()));
        TwinHandle<PopupOptions> positioner = await twin.AttachAsync("positioner", new PopupOptions
        {
            Positioner = new PopupPositionerOptions { Open = true, Cutout = true, ScrollLock = true, TouchOpen = true, ActiveTriggerId = "trigger-1" },
        });
        TwinHandle<PopupOptions> popup = await twin.AttachAsync("popup", new PopupOptions { Popup = new PopupElementOptions { StopCompositeKeys = true } });
        await viewport.UpdateAsync(new PopupOptions { SessionToken = "base-ui-2", Viewport = new PopupViewportOptions() });
        await popup.DisposeAsync();
        await popup.DisposeAsync();

        IReadOnlyList<JSRuntimeInvocation> attaches = module.Invocations["attach"];
        Assert.Equal(3, attaches.Count);
        Assert.Equal("viewport", attaches[0].Arguments[0]);
        Assert.Equal(
            """{"viewport":{"open":true,"mounted":true,"popupId":"base-ui-1","activeTriggerId":"trigger-1","contentVersion":2,"direction":"rtl"}}""",
            attaches[0].Arguments[1]);
        Assert.IsType<DotNetObjectReference<PopupTwin>>(attaches[0].Arguments[2]);
        Assert.Equal(
            """{"positioner":{"open":true,"cutout":true,"scrollLock":true,"touchOpen":true,"activeTriggerId":"trigger-1"}}""",
            attaches[1].Arguments[1]);
        Assert.Equal("""{"popup":{"stopCompositeKeys":true}}""", attaches[2].Arguments[1]);
        JSRuntimeInvocation update = Assert.Single(handle.Invocations["update"]);
        Assert.Equal(
            """{"sessionToken":"base-ui-2","viewport":{"open":false,"mounted":false,"contentVersion":0}}""",
            update.Arguments[0]);
        Assert.Single(handle.Invocations["dispose"]);
    }

    [Fact]
    [Trait("Requirement", "PSH-ALG-13")]
    [Trait("Requirement", "PSH-DOM-07")]
    [Trait("Requirement", "PSH-DOM-08")]
    public async Task The_viewport_report_raises_its_event_with_the_mirrored_values()
    {
        await using PopupTwin twin = new(new NoRuntime());
        List<PopupViewportReport> reported = [];
        twin.ViewportChanged += reported.Add;

        twin.OnViewportChange("""{"activationDirection":"right down","transitioning":true}""");
        twin.OnViewportChange("""{"activationDirection":null,"transitioning":false}""");

        Assert.Equal([new PopupViewportReport("right down", true), new PopupViewportReport(null, false)], reported);
    }

    [Fact]
    [Trait("Requirement", "POP-ANI-07")]
    public async Task The_trigger_change_report_raises_its_event_once_per_report()
    {
        await using PopupTwin twin = new(new NoRuntime());
        int settled = 0;
        twin.TriggerChangeSettled += () => settled++;

        twin.OnTriggerChangeSettled("{}");

        Assert.Equal(1, settled);
    }

    [Fact]
    public async Task Every_decision_point_answers_run_while_no_guard_asks()
    {
        await using PopupTwin twin = new(new NoRuntime());

        Assert.Equal("run", twin.Decide("activate", """{"kind":"FocusEvent","type":"focus"}"""));
    }

    private sealed class NoRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new NotSupportedException();
    }
}

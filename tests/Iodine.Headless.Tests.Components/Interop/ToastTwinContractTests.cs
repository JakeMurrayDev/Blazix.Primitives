using Iodine.Headless.Interop;
using Bunit;
using Microsoft.JSInterop;

namespace Iodine.Headless.Tests.Components.Interop;

/// <summary>
/// Twin contract tests of the <c>toast</c> module (ADR 06 §1, L2; ADR 04 Amendments 2026-11-18 and 2026-11-19, the module
/// as built): bUnit's JS interop mock records every field C# sends on <c>attach</c> and <c>update</c>, and the tests drive
/// <see cref="ToastTwin"/>'s one <c>[JSInvokable]</c> report.
/// </summary>
public sealed class ToastTwinContractTests
{
    private const string ModulePath = "./_content/Iodine.Headless/js/toast.js";

    [Fact]
    [Trait("Verifies", "ADR06-3")]
    [Trait("Requirement", "TST-API-14")]
    [Trait("Requirement", "TST-FOC-14")]
    public async Task Attach_imports_the_toast_module_and_sends_every_field_on_attach_and_update()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(ModulePath);
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        await using ToastTwin twin = new(context.JSInterop.JSRuntime);

        TwinHandle<ToastOptions> attached = await twin.AttachAsync("base-ui-1", new ToastOptions
        {
            Style = "--toast-index: 0; --toast-offset-y: 0px",
            Revision = 0,
            Directions = ["down", "right"],
            Lifecycle = 0,
        });
        await attached.UpdateAsync(new ToastOptions
        {
            Part = ToastTwin.ViewportPart,
            Revision = 3,
            Empty = false,
            Hovering = true,
            Focused = false,
            HasEnding = true,
            FocusTargetRef = 0,
            HandOff = new ToastHandOffOptions(2, false, 1),
        });
        await attached.UpdateAsync(new ToastOptions { Part = ToastTwin.ContentPart, RootRef = 0 });
        await attached.DisposeAsync();

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Equal("base-ui-1", attach.Arguments[0]);
        Assert.Equal(
            """{"part":"root","revision":0,"directions":["down","right"],"lifecycle":0,"style":"--toast-index: 0; --toast-offset-y: 0px"}""",
            attach.Arguments[1]);
        Assert.IsType<DotNetObjectReference<ToastTwin>>(attach.Arguments[2]);
        Assert.Equal(
            [
                """{"part":"viewport","revision":3,"empty":false,"hovering":true,"focused":false,"hasEnding":true,"focusTargetRef":0,"handOff":{"revision":2,"toPrevious":false,"targetRef":1}}""",
                """{"part":"content","rootRef":0}""",
            ],
            handle.Invocations["update"].Select(invocation => invocation.Arguments[0]));
        Assert.Single(handle.Invocations["dispose"]);
        Assert.Equal(["viewport", "root", "content"], [ToastTwin.ViewportPart, ToastTwin.RootPart, ToastTwin.ContentPart]);
    }

    [Fact]
    [Trait("Requirement", "TST-ANI-02")]
    [Trait("Requirement", "TST-PTR-17")]
    [Trait("Requirement", "TST-FOC-12")]
    public async Task A_report_reaches_the_event_with_every_field()
    {
        await using ToastTwin twin = new(new NoRuntime());
        List<ToastReport> reports = [];
        twin.Reported += reports.Add;

        twin.OnReport("""{"revision":1,"kind":"height","height":48,"lifecycle":2}""");
        twin.OnReport("""{"revision":2,"kind":"release","direction":"right","dismiss":true}""");
        twin.OnReport("""{"revision":3,"kind":"window-focus","resume":true}""");

        Assert.Equal(
            [
                new ToastReport(1, "height") { Height = 48, Lifecycle = 2 },
                new ToastReport(2, "release") { Direction = "right", Dismiss = true },
                new ToastReport(3, "window-focus") { Resume = true },
            ],
            reports);
    }

    [Fact]
    [Trait("Requirement", "TST-PTR-08")]
    public async Task The_module_asks_no_decision_point()
    {
        // Issue 280 (no decision point: no Toast row is a consumer veto of a module-owned dispatch); any point runs.
        await using ToastTwin twin = new(new NoRuntime());

        Assert.Equal("run", twin.Decide("press", """{"kind":"PointerEvent","type":"pointerdown","key":null,"code":null,"altKey":false,"ctrlKey":false,"metaKey":false,"shiftKey":false,"pointerType":"mouse","detail":0,"buttons":1,"target":null,"defaultPrevented":false,"timeStamp":1}"""));
    }

    private sealed class NoRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new NotSupportedException();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new NotSupportedException();
    }
}

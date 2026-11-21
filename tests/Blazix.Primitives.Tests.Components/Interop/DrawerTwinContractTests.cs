using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components.Interop;

/// <summary>
/// Twin contract tests of the <c>drawer</c> module (ADR 06 §1, L2; M6 track 8b, issue 279): bUnit's JS interop mock
/// records every field C# sends on <c>attach</c> and <c>update</c>, and the tests drive <see cref="DrawerTwin"/>'s
/// <c>[JSInvokable]</c> callbacks and its <c>dismiss</c> decision point.
/// </summary>
public sealed class DrawerTwinContractTests
{
    private const string ModulePath = "./_content/Blazix.Primitives/js/drawer.js";

    private const string ReleaseJson = """
        {"kind":"PointerEvent","type":"pointerup","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":"mouse","detail":0,"buttons":0,
         "target":{"id":"base-ui-7","tagName":"DIV"},"defaultPrevented":false,"timeStamp":3.5}
        """;

    [Fact(DisplayName = "port: attach imports the drawer module and sends every field on attach and update")]
    [Trait("Verifies", "ADR06-3")]
    [Trait("Requirement", "DRW-PTR-01")]
    public async Task Attach_imports_the_drawer_module_and_sends_every_field_on_attach_and_update()
    {
        using BunitContext context = new();
        BunitJSModuleInterop module = context.JSInterop.SetupModule(ModulePath);
        BunitJSModuleInterop handle = module.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
        await using DrawerTwin twin = new(context.JSInterop.JSRuntime);

        TwinHandle<DrawerOptions> attached = await twin.AttachAsync("base-ui-1", new DrawerOptions());
        await attached.UpdateAsync(new DrawerOptions
        {
            Open = true,
            Mounted = true,
            Nested = true,
            NestedOpenDrawerCount = 2,
            SwipeDirection = "right",
            AnsweredDismiss = 3,
            SnapPoints = [0.5, "200px"],
            ActiveSnapPoint = "200px",
            SnapToSequentialPoints = true,
            AnsweredSnap = 4,
            CanceledSnap = 2,
            Provider = "blazix-drawer-provider-7",
            FrontmostHeight = 240.5,
            HasParent = true,
            AncestorPopups = [2, 3],
            HasNestedDrawer = true,
            Ending = true,
            Disabled = true,
            SessionToken = "blazix-session-3",
            PopupId = "base-ui-9",
            AnsweredOpen = 5,
            Popup = 0,
            Backdrop = 1,
        });
        await attached.DisposeAsync();

        JSRuntimeInvocation attach = Assert.Single(module.Invocations["attach"]);
        Assert.Equal("base-ui-1", attach.Arguments[0]);
        Assert.Equal(
            """{"role":"viewport","open":false,"mounted":false,"nested":false,"nestedOpenDrawerCount":0,"swipeDirection":"down","answeredDismiss":0,"snapToSequentialPoints":false,"answeredSnap":0,"canceledSnap":0,"frontmostHeight":0,"hasParent":false,"hasNestedDrawer":false,"ending":false,"disabled":false,"answeredOpen":0}""",
            attach.Arguments[1]);
        Assert.IsType<DotNetObjectReference<DrawerTwin>>(attach.Arguments[2]);

        JSRuntimeInvocation update = Assert.Single(handle.Invocations["update"]);
        Assert.Equal(
            """{"role":"viewport","open":true,"mounted":true,"nested":true,"nestedOpenDrawerCount":2,"swipeDirection":"right","answeredDismiss":3,"snapPoints":[0.5,"200px"],"activeSnapPoint":"200px","snapToSequentialPoints":true,"answeredSnap":4,"canceledSnap":2,"provider":"blazix-drawer-provider-7","frontmostHeight":240.5,"hasParent":true,"ancestorPopups":[2,3],"hasNestedDrawer":true,"ending":true,"disabled":true,"sessionToken":"blazix-session-3","popupId":"base-ui-9","answeredOpen":5,"popup":0,"backdrop":1}""",
            update.Arguments[0]);
    }

    [Fact(DisplayName = "port: the swiping report raises the swiping change")]
    [Trait("Requirement", "DRW-DOM-07")]
    public async Task The_swiping_report_raises_the_swiping_change()
    {
        await using DrawerTwin twin = new(new NoRuntime());
        List<bool> reported = [];
        twin.SwipingChanged += reported.Add;

        twin.OnSwipingChange("""{"swiping":true}""");
        twin.OnSwipingChange("""{"swiping":false}""");

        Assert.Equal([true, false], reported);
    }

    [Fact(DisplayName = "port: the dismiss report carries the request number and the release event")]
    [Trait("Requirement", "DRW-PTR-14")]
    public async Task The_dismiss_report_carries_the_request_number_and_the_release_event()
    {
        await using DrawerTwin twin = new(new NoRuntime());
        List<DrawerDismissRequest> reported = [];
        twin.DismissRequested += reported.Add;

        twin.OnDismissRequest($$"""{"request":4,"event":{{ReleaseJson}}}""");

        DrawerDismissRequest request = Assert.Single(reported);
        Assert.Equal(4, request.Request);
        Assert.Equal("pointerup", request.Event.Type);
    }

    [Fact(DisplayName = "port: the dismiss point answers with the decider's verdict, and run without one or for another point")]
    [Trait("Requirement", "DRW-PTR-14")]
    public async Task The_dismiss_point_answers_with_the_deciders_verdict()
    {
        await using DrawerTwin twin = new(new NoRuntime());
        Assert.Equal("run", twin.Decide(DrawerTwin.DismissPoint, ReleaseJson));

        List<string> seen = [];
        twin.DismissDecider = snapshot =>
        {
            seen.Add(snapshot.Type);
            return DecisionPolicy.Stop;
        };

        Assert.Equal("stop", twin.Decide(DrawerTwin.DismissPoint, ReleaseJson));
        Assert.Equal("run", twin.Decide("snap-point", ReleaseJson));
        Assert.Equal(["pointerup"], seen);
    }

    private sealed class NoRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => throw new InvalidOperationException();

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => throw new InvalidOperationException();
    }
}

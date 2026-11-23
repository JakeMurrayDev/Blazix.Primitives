using Iodine.Headless.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

// Family tests use the project's root namespace (tests/Iodine.Headless.Tests.Components/CONTEXT.md).
namespace Iodine.Headless.Tests.Components;

/// <summary>
/// M6 track 8b slice 4 (issue 279): the C# half of the SwipeArea's opening gesture. The area attaches the `drawer` module
/// (role `swipe-area`) with its opening direction, the Root's interactions session and the Popup's id; an open report opens
/// or closes the drawer with reason `swipe` and the area as the trigger, and is answered in the next update (DRW-PTR-28,
/// PTR-30); the area's swiping report keeps it enabled over the drawer it is opening (PTR-26). The gesture itself is the
/// module's (js/drawer.swipe-area.test.ts at L3, tests/browser/drawer.spec.ts at L4).
/// </summary>
public sealed class DrawerSwipeAreaGestureTests : IDisposable
{
    private const string MoveJson = """
        {"kind":"PointerEvent","type":"pointerdown","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":"mouse","detail":0,"buttons":1,
         "target":{"id":"area","tagName":"DIV"},"defaultPrevented":false,"timeStamp":3.5}
        """;

    private readonly DialogTestContext test = new();
    private readonly BunitJSModuleInterop drawerModule;
    private readonly List<(bool Open, EventReason Reason, string? Trigger)> changes = [];

    public DrawerSwipeAreaGestureTests()
    {
        drawerModule = test.Context.JSInterop.SetupModule("./_content/Iodine.Headless/js/drawer.js");
        BunitJSModuleInterop handle = drawerModule.SetupModule("attach", _ => true);
        handle.SetupVoid("update", _ => true).SetVoidResult();
        handle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: the swipe area attaches the drawer module with its opening direction, the root's session and the popup's id")]
    [Trait("Requirement", "DRW-PTR-25")]
    [Trait("Requirement", "DRW-DIS-02")]
    public void The_swipe_area_attaches_the_drawer_module()
    {
        IRenderedComponent<IComponent> cut = test.Context.Render(Drawer());

        cut.WaitForAssertion(() =>
        {
            string options = Options(Assert.Single(Attaches("swipe-area")));
            Assert.Contains("\"swipeDirection\":\"up\"", options, StringComparison.Ordinal);
            Assert.Contains("\"open\":false", options, StringComparison.Ordinal);
            Assert.Contains("\"disabled\":false", options, StringComparison.Ordinal);
            Assert.Matches("\"sessionToken\":\"[^\"]+\"", options);
            Assert.Matches("\"popupId\":\"[^\"]+\"", options);
        });
    }

    [Fact(DisplayName = "port: an open report opens the drawer with reason swipe and the area as its trigger, and is answered")]
    [Trait("Requirement", "DRW-PTR-28")]
    [Trait("Requirement", "DRW-PTR-32")]
    public async Task An_open_report_opens_the_drawer()
    {
        IRenderedComponent<IComponent> cut = test.Context.Render(Drawer());
        cut.WaitForAssertion(() => Assert.Single(Attaches("swipe-area")));

        await cut.InvokeAsync(() => Twin().OnOpenRequest($$"""{"request":1,"open":true,"event":{{MoveJson}}}"""));

        Assert.Equal([(true, EventReason.Swipe, "area")], changes);
        cut.WaitForAssertion(() => Assert.True(AnySent("swipe-area", "\"answeredOpen\":1") && AnySent("swipe-area", "\"open\":true")));
        cut.WaitForAssertion(() => Assert.True(AnySent("swipe-area", "\"popup\":")));
    }

    [Fact(DisplayName = "port: a close report closes the drawer the gesture opened")]
    [Trait("Requirement", "DRW-PTR-30")]
    public async Task A_close_report_closes_the_drawer()
    {
        IRenderedComponent<IComponent> cut = test.Context.Render(Drawer());
        cut.WaitForAssertion(() => Assert.Single(Attaches("swipe-area")));

        await cut.InvokeAsync(() => Twin().OnOpenRequest($$"""{"request":1,"open":true,"event":{{MoveJson}}}"""));
        await cut.InvokeAsync(() => Twin().OnOpenRequest($$"""{"request":2,"open":false,"event":{{MoveJson}}}"""));

        Assert.Equal([(true, EventReason.Swipe, "area"), (false, EventReason.Swipe, "area")], changes);
        cut.WaitForAssertion(() => Assert.True(AnySent("swipe-area", "\"answeredOpen\":2")));
    }

    [Fact(DisplayName = "port: the swiping report keeps the area enabled over the drawer it is opening")]
    [Trait("Requirement", "DRW-PTR-26")]
    [Trait("Requirement", "DRW-DOM-29")]
    public async Task The_swiping_report_keeps_the_area_enabled()
    {
        IRenderedComponent<IComponent> cut = test.Context.Render(Drawer());
        cut.WaitForAssertion(() => Assert.Single(Attaches("swipe-area")));

        await cut.InvokeAsync(() => Twin().OnSwipingChange("""{"swiping":true}"""));
        await cut.InvokeAsync(() => Twin().OnOpenRequest($$"""{"request":1,"open":true,"event":{{MoveJson}}}"""));
        cut.WaitForAssertion(() => Assert.DoesNotContain("pointer-events: none", cut.Find("#area").GetAttribute("style"), StringComparison.Ordinal));

        await cut.InvokeAsync(() => Twin().OnSwipingChange("""{"swiping":false}"""));
        cut.WaitForAssertion(() => Assert.Contains("pointer-events: none", cut.Find("#area").GetAttribute("style"), StringComparison.Ordinal));
    }

    private List<JSRuntimeInvocation> Attaches(string role) =>
        drawerModule.Invocations["attach"].Where(attach => Options(attach).Contains($"\"role\":\"{role}\"", StringComparison.Ordinal)).ToList();

    private static string Options(JSRuntimeInvocation attach) => attach.Arguments[1] as string ?? string.Empty;

    private DrawerTwin Twin() => ((DotNetObjectReference<DrawerTwin>)Attaches("swipe-area")[^1].Arguments[2]!).Value;

    private bool AnySent(string role, string fragment) =>
        drawerModule.Invocations["attach"].Select(Options)
            .Concat(test.Context.JSInterop.Invocations["update"].Select(update => update.Arguments[0] as string ?? string.Empty))
            .Any(json => json.Contains($"\"role\":\"{role}\"", StringComparison.Ordinal) && json.Contains(fragment, StringComparison.Ordinal));

    private RenderFragment Drawer() => builder =>
    {
        builder.OpenComponent<DrawerRoot<NoPayload>>(0);
        builder.AddComponentParameter(1, nameof(DrawerRoot<NoPayload>.OnOpenChange), EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, args =>
            changes.Add((args.Value, args.Details.Reason, args.Details.Trigger?.Id))));
        builder.AddComponentParameter(2, nameof(DrawerRoot<NoPayload>.ChildContent), (RenderFragment)(root =>
        {
            root.OpenComponent<DrawerSwipeArea>(0);
            root.AddAttribute(1, "id", "area");
            root.CloseComponent();
            root.OpenComponent<DrawerPortal>(2);
            root.AddComponentParameter(3, nameof(DrawerPortal.ChildContent), (RenderFragment)(portal =>
            {
                portal.OpenComponent<DrawerBackdrop>(0);
                portal.CloseComponent();
                portal.OpenComponent<DrawerViewport>(1);
                portal.AddComponentParameter(2, nameof(DrawerViewport.ChildContent), (RenderFragment)(viewport =>
                {
                    viewport.OpenComponent<DrawerPopup>(0);
                    viewport.CloseComponent();
                }));
                portal.CloseComponent();
            }));
            root.CloseComponent();
        }));
        builder.CloseComponent();
    };
}

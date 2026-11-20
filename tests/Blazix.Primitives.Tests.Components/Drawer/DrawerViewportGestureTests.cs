using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

// M6 track 8b (issue 279), slice 1: the C# half of the Viewport's dismiss gesture. The `drawer` module's handle on the
// Viewport, the Popup and Backdrop elements it is handed, C#'s mirror of data-swiping, and the close a swipe release
// requests (DRW-PTR-01, PTR-14; DOM-07; design point 10 (a), chosen by the user 2026-11-19). The gesture itself is the
// module's (js/drawer.test.ts at L3, tests/browser/drawer.spec.ts at L4).
public sealed class DrawerViewportGestureTests : IDisposable
{
    private const string ReleaseJson = """
        {"kind":"PointerEvent","type":"pointerup","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":"mouse","detail":0,"buttons":0,
         "target":{"id":"base-ui-7","tagName":"DIV"},"defaultPrevented":false,"timeStamp":3.5}
        """;

    private readonly DialogTestContext test = new();
    private readonly BunitJSModuleInterop drawerModule;
    private readonly BunitJSModuleInterop drawerHandle;
    private readonly List<(bool Open, EventReason Reason)> changes = [];
    private readonly List<(SnapPointValue? Value, EventReason Reason)> snapChanges = [];
    private bool cancelClose;
    private bool cancelSnap;

    public DrawerViewportGestureTests()
    {
        drawerModule = test.Context.JSInterop.SetupModule("./_content/Blazix.Primitives/js/drawer.js");
        drawerHandle = drawerModule.SetupModule("attach", _ => true);
        drawerHandle.SetupVoid("update", _ => true).SetVoidResult();
        drawerHandle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: the viewport attaches the drawer module with the popup and backdrop elements and the root's state")]
    [Trait("Requirement", "DRW-PTR-01")]
    public void The_viewport_attaches_the_drawer_module()
    {
        IRenderedComponent<IComponent> cut = test.Context.Render(Drawer());

        cut.WaitForAssertion(() =>
        {
            JSRuntimeInvocation attach = Assert.Single(drawerModule.Invocations["attach"]);
            Assert.Equal(cut.Find("[data-testid=viewport]").GetAttribute("id") ?? string.Empty, attach.Arguments[0] as string ?? string.Empty);
            string options = LastOptions();
            Assert.Contains("\"role\":\"viewport\"", options, StringComparison.Ordinal);
            Assert.Contains("\"open\":true", options, StringComparison.Ordinal);
            Assert.Contains("\"mounted\":true", options, StringComparison.Ordinal);
            Assert.Contains("\"swipeDirection\":\"down\"", options, StringComparison.Ordinal);
            Assert.Contains("\"popup\":", options, StringComparison.Ordinal);
            Assert.Contains("\"backdrop\":", options, StringComparison.Ordinal);
        });
    }

    [Fact(DisplayName = "port: a dismiss report closes the drawer with reason swipe and answers the request in the next update")]
    [Trait("Requirement", "DRW-PTR-14")]
    public async Task A_dismiss_report_closes_the_drawer_and_answers_it()
    {
        IRenderedComponent<IComponent> cut = test.Context.Render(Drawer());
        cut.WaitForAssertion(() => Assert.Single(drawerModule.Invocations["attach"]));

        await cut.InvokeAsync(() => Twin().OnDismissRequest($$"""{"request":1,"event":{{ReleaseJson}}}"""));

        Assert.Equal([(false, EventReason.Swipe)], changes);
        cut.WaitForAssertion(() => Assert.Contains("\"answeredDismiss\":1", LastOptions(), StringComparison.Ordinal));
    }

    [Fact(DisplayName = "port: a canceled dismiss report keeps the drawer open and still answers the request")]
    [Trait("Requirement", "DRW-PTR-14")]
    public async Task A_canceled_dismiss_report_answers_the_request()
    {
        cancelClose = true;
        IRenderedComponent<IComponent> cut = test.Context.Render(Drawer());
        cut.WaitForAssertion(() => Assert.Single(drawerModule.Invocations["attach"]));

        await cut.InvokeAsync(() => Twin().OnDismissRequest($$"""{"request":1,"event":{{ReleaseJson}}}"""));

        cut.WaitForAssertion(() =>
        {
            string options = LastOptions();
            Assert.Contains("\"answeredDismiss\":1", options, StringComparison.Ordinal);
            Assert.Contains("\"open\":true", options, StringComparison.Ordinal);
        });
    }

    [Fact(DisplayName = "port: the dismiss point closes the drawer inside the dispatch, and answers stop when the close is canceled")]
    [Trait("Requirement", "DRW-PTR-14")]
    public async Task The_dismiss_point_closes_the_drawer_inside_the_dispatch()
    {
        cancelClose = true;
        IRenderedComponent<IComponent> cut = test.Context.Render(Drawer());
        cut.WaitForAssertion(() => Assert.Single(drawerModule.Invocations["attach"]));

        string canceled = await cut.InvokeAsync(() => Twin().Decide(DrawerTwin.DismissPoint, ReleaseJson));
        Assert.Equal("stop", canceled);

        cancelClose = false;
        string accepted = await cut.InvokeAsync(() => Twin().Decide(DrawerTwin.DismissPoint, ReleaseJson));
        Assert.Equal("run", accepted);
        Assert.Equal([(false, EventReason.Swipe), (false, EventReason.Swipe)], changes);

        // Only the accepted release counts: the module numbers the dismissals it did not roll back.
        cut.WaitForAssertion(() => Assert.Contains("\"answeredDismiss\":1", LastOptions(), StringComparison.Ordinal));
    }

    // Found by the L4 title "does not dismiss a controlled drawer via swipe when open is always true" on WebAssembly
    // (2026-11-19): an answer sent inside the dispatch reaches the module before it numbers the dismissal, so the module
    // ignores it, and a controlled drawer that stays open sends no later update to roll the dismissal back.
    [Fact(DisplayName = "port: the dismiss point's answer reaches the module only after the dispatch returns")]
    [Trait("Requirement", "DRW-PTR-15")]
    public async Task The_dismiss_point_answer_follows_the_dispatch()
    {
        IRenderedComponent<IComponent> cut = test.Context.Render(Drawer());
        cut.WaitForAssertion(() => Assert.Single(drawerModule.Invocations["attach"]));
        int before = drawerHandle.Invocations["update"].Count;

        int during = await cut.InvokeAsync(() =>
        {
            Assert.Equal("run", Twin().Decide(DrawerTwin.DismissPoint, ReleaseJson));
            return drawerHandle.Invocations["update"].Count;
        });

        Assert.Equal(before, during);
        cut.WaitForAssertion(() => Assert.Contains("\"answeredDismiss\":1", LastOptions(), StringComparison.Ordinal));
    }

    // The Backdrop's data-swiping is the module's write alone: Base UI's Backdrop state has no swiping field.
    [Fact(DisplayName = "port: a swiping report marks the popup's state as swiping")]
    [Trait("Requirement", "DRW-DOM-07")]
    public async Task A_swiping_report_marks_the_states_as_swiping()
    {
        DrawerPopupState? popupState = null;
        IRenderedComponent<IComponent> cut = test.Context.Render(Drawer(popupClass: state => { popupState = state; return null; }));
        cut.WaitForAssertion(() => Assert.Single(drawerModule.Invocations["attach"]));

        await cut.InvokeAsync(() => Twin().OnSwipingChange("""{"swiping":true}"""));
        cut.WaitForAssertion(() => Assert.True(popupState?.Swiping));

        await cut.InvokeAsync(() => Twin().OnSwipingChange("""{"swiping":false}"""));
        cut.WaitForAssertion(() => Assert.False(popupState?.Swiping));
    }

    // Slice 3 (2026-11-20): the C# half of the snap points. The module resolves and releases them (js/drawer.snap.test.ts);
    // C# sends the list, the active value and the flag, and applies the module's requests (DRW-PTR-11, STA-03).
    [Fact(DisplayName = "port: the viewport sends the snap points, the active snap point and the sequential flag to the module")]
    [Trait("Requirement", "DRW-PTR-11")]
    public void The_viewport_sends_the_snap_points()
    {
        IRenderedComponent<IComponent> cut = test.Context.Render(Drawer(snapPoints: [0.5, "200px"], defaultSnapPoint: "200px", sequential: true));

        cut.WaitForAssertion(() =>
        {
            string options = LastOptions();
            Assert.Contains("\"snapPoints\":[0.5,\"200px\"]", options, StringComparison.Ordinal);
            Assert.Contains("\"activeSnapPoint\":\"200px\"", options, StringComparison.Ordinal);
            Assert.Contains("\"snapToSequentialPoints\":true", options, StringComparison.Ordinal);
        });
    }

    [Fact(DisplayName = "port: a snap point report selects the indexed snap point with reason swipe and answers it in the next update")]
    [Trait("Requirement", "DRW-PTR-11")]
    public async Task A_snap_point_report_selects_the_snap_point_and_answers_it()
    {
        IRenderedComponent<IComponent> cut = test.Context.Render(Drawer(snapPoints: [0.5, "200px"], defaultSnapPoint: "200px"));
        cut.WaitForAssertion(() => Assert.Single(drawerModule.Invocations["attach"]));

        await cut.InvokeAsync(() => Twin().OnSnapPointRequest($$"""{"request":1,"index":0,"event":{{ReleaseJson}}}"""));
        Assert.Equal([((SnapPointValue?)0.5, EventReason.Swipe)], snapChanges);
        cut.WaitForAssertion(() =>
        {
            string options = LastOptions();
            Assert.Contains("\"answeredSnap\":1", options, StringComparison.Ordinal);
            Assert.Contains("\"activeSnapPoint\":0.5", options, StringComparison.Ordinal);
        });

        await cut.InvokeAsync(() => Twin().OnSnapPointRequest($$"""{"request":2,"index":null,"event":{{ReleaseJson}}}"""));
        Assert.Equal(((SnapPointValue?)null, EventReason.Swipe), snapChanges[^1]);
        cut.WaitForAssertion(() => Assert.Contains("\"answeredSnap\":2", LastOptions(), StringComparison.Ordinal));
    }

    [Fact(DisplayName = "port: the snap-point point requests the indexed snap point inside the dispatch, and answers stop when it is canceled")]
    [Trait("Requirement", "DRW-PTR-11")]
    public async Task The_snap_point_point_requests_the_snap_point_inside_the_dispatch()
    {
        IRenderedComponent<IComponent> cut = test.Context.Render(Drawer(snapPoints: [0.5, "200px"], defaultSnapPoint: "200px"));
        cut.WaitForAssertion(() => Assert.Single(drawerModule.Invocations["attach"]));

        Assert.Equal("run", await cut.InvokeAsync(() => Twin().Decide("snap-point:0", ReleaseJson)));
        Assert.Equal([((SnapPointValue?)0.5, EventReason.Swipe)], snapChanges);

        cancelSnap = true;
        Assert.Equal("stop", await cut.InvokeAsync(() => Twin().Decide("snap-point:none", ReleaseJson)));
        Assert.Equal(((SnapPointValue?)null, EventReason.Swipe), snapChanges[^1]);
        cut.WaitForAssertion(() => Assert.Contains("\"activeSnapPoint\":0.5", LastOptions(), StringComparison.Ordinal));
    }

    // DRW-ALG-01's canceled close on Blazor Server (DrawerViewport.tsx:562-566): the module reports the change to none and
    // then the dismissal it started optimistically; a canceled change to none drops the dismissal, so no close is requested,
    // and the next update names the canceled request.
    [Fact(DisplayName = "port: a dismissal after a canceled change to no snap point requests no close and is still answered")]
    [Trait("Requirement", "DRW-ALG-01")]
    public async Task A_dismissal_after_a_canceled_change_to_none_requests_no_close()
    {
        IRenderedComponent<IComponent> cut = test.Context.Render(Drawer(snapPoints: [0.5, "200px"], defaultSnapPoint: "200px"));
        cut.WaitForAssertion(() => Assert.Single(drawerModule.Invocations["attach"]));
        cancelSnap = true;

        await cut.InvokeAsync(() => Twin().OnSnapPointRequest($$"""{"request":1,"index":null,"event":{{ReleaseJson}}}"""));
        await cut.InvokeAsync(() => Twin().OnDismissRequest($$"""{"request":1,"event":{{ReleaseJson}}}"""));

        Assert.Empty(changes);
        cut.WaitForAssertion(() =>
        {
            string options = LastOptions();
            Assert.Contains("\"canceledSnap\":1", options, StringComparison.Ordinal);
            Assert.Contains("\"answeredDismiss\":1", options, StringComparison.Ordinal);
            Assert.Contains("\"open\":true", options, StringComparison.Ordinal);
        });

        // A later dismissal with no canceled change before it closes.
        cancelSnap = false;
        await cut.InvokeAsync(() => Twin().OnSnapPointRequest($$"""{"request":2,"index":null,"event":{{ReleaseJson}}}"""));
        await cut.InvokeAsync(() => Twin().OnDismissRequest($$"""{"request":2,"event":{{ReleaseJson}}}"""));
        Assert.Equal([(false, EventReason.Swipe)], changes);
    }

    private DrawerTwin Twin() => ((DotNetObjectReference<DrawerTwin>)drawerModule.Invocations["attach"][^1].Arguments[2]!).Value;

    private string LastOptions()
    {
        IReadOnlyList<JSRuntimeInvocation> updates = drawerHandle.Invocations["update"];
        return (updates.Count > 0 ? updates[^1].Arguments[0] : drawerModule.Invocations["attach"][^1].Arguments[1]) as string ?? string.Empty;
    }

    private RenderFragment Drawer(
        Func<DrawerPopupState, string?>? popupClass = null,
        IReadOnlyList<SnapPointValue>? snapPoints = null,
        SnapPointValue? defaultSnapPoint = null,
        bool sequential = false) => builder =>
    {
        builder.OpenComponent<DrawerRoot<NoPayload>>(0);
        builder.AddComponentParameter(1, nameof(DrawerRoot<NoPayload>.DefaultOpen), true);
        if (snapPoints is not null)
        {
            builder.AddComponentParameter(5, nameof(DrawerRoot<NoPayload>.SnapPoints), snapPoints);
            builder.AddComponentParameter(6, nameof(DrawerRoot<NoPayload>.DefaultSnapPoint), defaultSnapPoint);
            builder.AddComponentParameter(7, nameof(DrawerRoot<NoPayload>.SnapToSequentialPoints), sequential);
            builder.AddComponentParameter(8, nameof(DrawerRoot<NoPayload>.OnSnapPointChange), EventCallback.Factory.Create<ValueChangeEventArgs<SnapPointValue?>>(this, args =>
            {
                snapChanges.Add((args.Value, args.Details.Reason));
                if (cancelSnap)
                {
                    args.Details.Cancel();
                }
            }));
        }

        builder.AddComponentParameter(2, nameof(DrawerRoot<NoPayload>.OnOpenChange), EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, args =>
        {
            changes.Add((args.Value, args.Details.Reason));
            if (cancelClose)
            {
                args.Details.Cancel();
            }
        }));
        builder.AddComponentParameter(3, nameof(DrawerRoot<NoPayload>.ChildContent), (RenderFragment)(root =>
        {
            root.OpenComponent<DrawerPortal>(0);
            root.AddComponentParameter(1, nameof(DrawerPortal.ChildContent), (RenderFragment)(portal =>
            {
                portal.OpenComponent<DrawerBackdrop>(0);
                portal.CloseComponent();
                portal.OpenComponent<DrawerViewport>(2);
                portal.AddAttribute(3, "data-testid", "viewport");
                portal.AddComponentParameter(4, nameof(DrawerViewport.ChildContent), (RenderFragment)(viewport =>
                {
                    viewport.OpenComponent<DrawerPopup>(0);
                    if (popupClass is not null)
                    {
                        viewport.AddComponentParameter(1, nameof(DrawerPopup.ClassValue), popupClass);
                    }

                    viewport.CloseComponent();
                }));
                portal.CloseComponent();
            }));
            root.CloseComponent();
        }));
        builder.CloseComponent();
    };
}

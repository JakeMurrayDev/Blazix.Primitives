using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

// M6 track 8c (issue 279): the C# half of the drawer's CloseWatcher (DRW-DIS-01, EVT-01's `close-watcher`). The watcher
// rides the Popup's `drawer` handle (design point 1 (a) of 2026-11-18). Design point D1 (a), chosen by the user
// 2026-11-21: on WebAssembly the `close-watcher` point closes the drawer inside the watcher's `cancel` event and answers
// `prevent` when a consumer cancels it; elsewhere the module prevents a cancelable event and reports a numbered close
// request a round trip later. The watcher itself is the module's (tests/browser/drawer.spec.ts at L4).
public sealed class DrawerCloseWatcherTests : IDisposable
{
    private const string CancelJson = """
        {"kind":"Event","type":"cancel","key":null,"code":null,"altKey":false,"ctrlKey":false,"metaKey":false,
         "shiftKey":false,"pointerType":null,"detail":0,"buttons":0,"target":null,"defaultPrevented":false,"timeStamp":4.5}
        """;

    private readonly DialogTestContext test = new();
    private readonly BunitJSModuleInterop drawerModule;
    private readonly BunitJSModuleInterop drawerHandle;
    private readonly List<(bool Open, EventReason Reason)> changes = [];
    private readonly List<bool> canceledAfter = [];
    private bool cancelClose;

    public DrawerCloseWatcherTests()
    {
        drawerModule = test.Context.JSInterop.SetupModule("./_content/Blazix.Primitives/js/drawer.js");
        drawerHandle = drawerModule.SetupModule("attach", _ => true);
        drawerHandle.SetupVoid("update", _ => true).SetVoidResult();
        drawerHandle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: the close-watcher point closes the drawer with reason close-watcher inside the dispatch, and answers prevent when the close is canceled")]
    [Trait("Requirement", "DRW-DIS-01")]
    [Trait("Requirement", "DRW-EVT-01")]
    public async Task The_close_watcher_point_closes_the_drawer_inside_the_dispatch()
    {
        cancelClose = true;
        IRenderedComponent<IComponent> cut = test.Context.Render(Drawer());
        cut.WaitForAssertion(() => Assert.Single(PopupAttaches()));

        string canceled = await cut.InvokeAsync(() => Twin().Decide(DrawerTwin.CloseWatcherPoint, CancelJson));
        Assert.Equal("prevent", canceled);
        Assert.True(cut.Find("[role=dialog]").HasAttribute("data-open"));

        cancelClose = false;
        string accepted = await cut.InvokeAsync(() => Twin().Decide(DrawerTwin.CloseWatcherPoint, CancelJson));
        Assert.Equal("run", accepted);
        Assert.Equal([(false, EventReason.CloseWatcher), (false, EventReason.CloseWatcher)], changes);
        // The exit animation never completes here, so the popup stays mounted, closed.
        cut.WaitForAssertion(() => Assert.True(cut.Find("[role=dialog]").HasAttribute("data-closed")));
    }

    [Fact(DisplayName = "port: a close-watcher report closes the drawer with reason close-watcher")]
    [Trait("Requirement", "DRW-DIS-01")]
    [Trait("Requirement", "DRW-EVT-01")]
    public async Task A_close_watcher_report_closes_the_drawer()
    {
        IRenderedComponent<IComponent> cut = test.Context.Render(Drawer());
        cut.WaitForAssertion(() => Assert.Single(PopupAttaches()));

        await cut.InvokeAsync(() => Twin().OnCloseWatcherRequest($$"""{"request":1,"cancelable":true,"event":{{CancelJson}}}"""));

        Assert.Equal([(false, EventReason.CloseWatcher)], changes);
        // The exit animation never completes here, so the popup stays mounted, closed.
        cut.WaitForAssertion(() => Assert.True(cut.Find("[role=dialog]").HasAttribute("data-closed")));
    }

    [Fact(DisplayName = "port: a canceled close-watcher report keeps the drawer open")]
    [Trait("Requirement", "DRW-DIS-01")]
    public async Task A_canceled_close_watcher_report_keeps_the_drawer_open()
    {
        cancelClose = true;
        IRenderedComponent<IComponent> cut = test.Context.Render(Drawer());
        cut.WaitForAssertion(() => Assert.Single(PopupAttaches()));

        await cut.InvokeAsync(() => Twin().OnCloseWatcherRequest($$"""{"request":1,"cancelable":true,"event":{{CancelJson}}}"""));

        Assert.Equal([(false, EventReason.CloseWatcher)], changes);
        Assert.True(cut.Find("[role=dialog]").HasAttribute("data-open"));
    }

    // DRW-DIS-01, EVD-EVT-10; design point D2 (a), chosen by the user 2026-11-21.
    [Fact(DisplayName = "port: a close-watcher report that cannot be prevented closes the drawer even when the consumer cancels it")]
    [Trait("Requirement", "DRW-DIS-01")]
    [Trait("Requirement", "EVD-EVT-10")]
    public async Task A_close_watcher_report_that_cannot_be_prevented_closes_the_drawer()
    {
        cancelClose = true;
        IRenderedComponent<IComponent> cut = test.Context.Render(Drawer());
        cut.WaitForAssertion(() => Assert.Single(PopupAttaches()));

        await cut.InvokeAsync(() => Twin().OnCloseWatcherRequest($$"""{"request":1,"cancelable":false,"event":{{CancelJson}}}"""));

        Assert.Equal([(false, EventReason.CloseWatcher)], changes);
        Assert.Equal([false], canceledAfter);
        cut.WaitForAssertion(() => Assert.True(cut.Find("[role=dialog]").HasAttribute("data-closed")));
    }

    [Fact(DisplayName = "port: the popup sends the number of dialogs open inside the drawer, which makes it not topmost")]
    [Trait("Requirement", "DRW-DIS-01")]
    public void The_popup_sends_the_nested_open_dialog_count()
    {
        IRenderedComponent<IComponent> cut = test.Context.Render(Drawer());

        cut.WaitForAssertion(() => Assert.Contains("\"nestedOpenDialogCount\":0", LastPopupOptions(), StringComparison.Ordinal));
    }

    private DrawerTwin Twin() => ((DotNetObjectReference<DrawerTwin>)PopupAttaches()[^1].Arguments[2]!).Value;

    private List<JSRuntimeInvocation> PopupAttaches() =>
        drawerModule.Invocations["attach"].Where(attach => IsPopup(attach.Arguments[1])).ToList();

    private static bool IsPopup(object? options) =>
        options is string json && json.Contains("\"role\":\"popup\"", StringComparison.Ordinal);

    private string LastPopupOptions()
    {
        List<JSRuntimeInvocation> updates = drawerHandle.Invocations["update"].Where(update => IsPopup(update.Arguments[0])).ToList();
        return (updates.Count > 0 ? updates[^1].Arguments[0] : PopupAttaches()[^1].Arguments[1]) as string ?? string.Empty;
    }

    private RenderFragment Drawer() => builder =>
    {
        builder.OpenComponent<DrawerRoot<NoPayload>>(0);
        builder.AddComponentParameter(1, nameof(DrawerRoot<NoPayload>.DefaultOpen), true);
        builder.AddComponentParameter(2, nameof(DrawerRoot<NoPayload>.OnOpenChange), EventCallback.Factory.Create<ValueChangeEventArgs<bool>>(this, args =>
        {
            changes.Add((args.Value, args.Details.Reason));
            if (cancelClose)
            {
                args.Details.Cancel();
            }

            canceledAfter.Add(args.Details.IsCanceled);
        }));
        builder.AddComponentParameter(3, nameof(DrawerRoot<NoPayload>.ChildContent), (RenderFragment)(root =>
        {
            root.OpenComponent<DrawerPortal>(0);
            root.AddComponentParameter(1, nameof(DrawerPortal.ChildContent), (RenderFragment)(portal =>
            {
                portal.OpenComponent<DrawerViewport>(0);
                portal.AddComponentParameter(1, nameof(DrawerViewport.ChildContent), (RenderFragment)(viewport =>
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

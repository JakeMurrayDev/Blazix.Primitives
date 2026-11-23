using Iodine.Headless.Interop;
using Bunit;

// The namespace is the project's root: a namespace named after the family would hide the parts' types.
namespace Iodine.Headless.Tests.Components;

/// <summary>
/// L2 titles over the `toast` module's reports (ADR 06 §1, the twin contract's effect): a report the module sends through
/// a part's twin reaches the store and the parts' projection. The re-tagged hover rows (TST-PTR-01..04, TST-DOM-02; the
/// brief's Deliverable 7) assert here the report's effect; their real hover is L4 (tests/browser/toast.viewport.spec.ts).
/// </summary>
public sealed class ToastModuleReportTests : IDisposable
{
    private readonly ToastTestContext test = new();

    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "maps the active swipe direction to its data attribute")]
    [Trait("Requirement", "TST-DOM-06")]
    [Trait("Requirement", "TST-STA-26")]
    public void Maps_the_active_swipe_direction_to_its_data_attribute()
    {
        // ToastRoot.test.tsx:91 tests Base UI's mapping; the attribute is the module's here (L3), and C# mirrors the
        // direction into the state a consumer's functions read, never rendering it.
        List<ToastRootState> states = [];
        ToastObject toast = new() { Id = "t" };
        test.Context.RenderTree.Add<ToastProvider<NoPayload>>();
        IRenderedComponent<ToastRoot<NoPayload>> cut = test.Context.Render<ToastRoot<NoPayload>>(p => p
            .Add(x => x.Toast, toast)
            .Add(x => x.ClassValue, state =>
            {
                states.Add(state);
                return null;
            }));

        test.Run(cut, () => cut.Instance.Twin!.OnReport("""{"revision":1,"kind":"swipe-start"}"""));
        test.Run(cut, () => cut.Instance.Twin!.OnReport("""{"revision":2,"kind":"direction","direction":"left"}"""));

        Assert.Equal(SwipeDirection.Left, states[^1].SwipeDirection);
        Assert.True(states[^1].Swiping);
        Assert.Null(cut.Find("[role=dialog]").GetAttribute("data-swipe-direction"));
        Assert.Null(cut.Find("[role=dialog]").GetAttribute("data-swiping"));
    }

    [Fact(DisplayName = "sets the vertical offset CSS variable")]
    [Trait("Requirement", "TST-DOM-11")]
    [Trait("Requirement", "TST-ANI-02")]
    public void Sets_the_vertical_offset_CSS_variable()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost();
        test.Run(cut, () =>
        {
            test.Manager.Add(new ToastObject { Id = "older" });
            test.Manager.Add(new ToastObject { Id = "newer" });
        });

        // The newest toast's measurement report, as the module sends it at the attach.
        IRenderedComponent<ToastRoot<NoPayload>> newest = cut.FindComponents<ToastRoot<NoPayload>>()[0];
        test.Run(cut, () => newest.Instance.Twin!.OnReport("""{"revision":1,"kind":"height","height":40,"lifecycle":0}"""));

        IReadOnlyList<string?> styles = ToastTestContext.RootStyles(cut);
        Assert.Contains("--toast-offset-y: 0px", styles[0], StringComparison.Ordinal);
        Assert.Contains("--toast-height: 40px", styles[0], StringComparison.Ordinal);
        Assert.Contains("--toast-offset-y: 40px", styles[1], StringComparison.Ordinal);
    }

    [Fact(DisplayName = "port: the viewport's hover reports expand the stack and pause the timers, and the leave collapses and resumes them")]
    [Trait("Requirement", "TST-PTR-01")]
    [Trait("Requirement", "TST-PTR-02")]
    [Trait("Requirement", "TST-DOM-02")]
    public void The_viewports_hover_reports_expand_the_stack_and_pause_the_timers()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost();
        test.Run(cut, () => test.Manager.Add(new ToastObject { Id = "t" }));
        ToastTwin twin = cut.FindComponent<ToastViewport<NoPayload>>().Instance.Twin!;

        test.Run(cut, () => twin.OnReport("""{"revision":1,"kind":"enter"}"""));
        Assert.NotNull(cut.Find("[data-testid=viewport]").GetAttribute("data-expanded"));
        test.Advance(cut, 5001);
        Assert.Single(ToastTestContext.Roots(cut));

        test.Run(cut, () => twin.OnReport("""{"revision":2,"kind":"leave"}"""));
        Assert.Null(cut.Find("[data-testid=viewport]").GetAttribute("data-expanded"));
        test.Advance(cut, 4999);
        Assert.Null(ToastTestContext.Roots(cut)[0].GetAttribute("data-ending-style"));
        test.Advance(cut, 2);
        Assert.Equal(TransitionStatus.Ending, test.Manager.Toasts[0].TransitionStatus);
    }

    [Fact(DisplayName = "port: a touch press pauses the timers and a swipe start expands the stack")]
    [Trait("Requirement", "TST-PTR-04")]
    [Trait("Requirement", "TST-PTR-09")]
    [Trait("Requirement", "TST-PTR-10")]
    public void A_touch_press_and_a_swipe_start_keep_the_stack_expanded()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost();
        test.Run(cut, () => test.Manager.Add(new ToastObject { Id = "t" }));
        ToastTwin root = cut.FindComponents<ToastRoot<NoPayload>>()[0].Instance.Twin!;

        test.Run(cut, () => root.OnReport("""{"revision":1,"kind":"touch-press"}"""));
        test.Run(cut, () => root.OnReport("""{"revision":2,"kind":"swipe-start"}"""));

        Assert.NotNull(cut.Find("[data-testid=viewport]").GetAttribute("data-expanded"));
        test.Advance(cut, 5001);
        Assert.NotEqual(TransitionStatus.Ending, test.Manager.Toasts[0].TransitionStatus);
    }

    [Fact(DisplayName = "port: a deferred leave arrives once no toast is ending, and collapses the stack then")]
    [Trait("Requirement", "TST-PTR-03")]
    [Trait("Requirement", "TST-STA-15")]
    public void A_deferred_leave_collapses_the_stack()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost();
        test.Run(cut, () =>
        {
            test.Manager.Add(new ToastObject { Id = "a" });
            test.Manager.Add(new ToastObject { Id = "b" });
        });
        ToastTwin twin = cut.FindComponent<ToastViewport<NoPayload>>().Instance.Twin!;
        test.Run(cut, () => twin.OnReport("""{"revision":1,"kind":"enter"}"""));
        test.Run(cut, () => test.Manager.Close("b"));

        // The module holds the leave while C# reports a toast ending; it sends it when the ending toast is gone.
        Assert.NotNull(cut.Find("[data-testid=viewport]").GetAttribute("data-expanded"));
        test.Run(cut, () => twin.OnReport("""{"revision":2,"kind":"leave"}"""));

        Assert.Null(cut.Find("[data-testid=viewport]").GetAttribute("data-expanded"));
    }

    [Fact(DisplayName = "port: a close computes the focus hand-off before the store notifies, a timer's close included (TST §16 Q6)")]
    [Trait("Requirement", "TST-FOC-07")]
    [Trait("Requirement", "TST-FOC-08")]
    public void A_close_computes_the_focus_hand_off()
    {
        IRenderedComponent<ToastHost> cut = test.RenderHost();
        test.Run(cut, () =>
        {
            test.Manager.Add(new ToastObject { Id = "oldest", Timeout = 0 });
            test.Manager.Add(new ToastObject { Id = "middle", Timeout = 0 });
            test.Manager.Add(new ToastObject { Id = "newest", Timeout = 1000 });
        });
        ToastStore<NoPayload> store = cut.FindComponent<ToastProvider<NoPayload>>().Instance.Store;
        List<ToastHandOff?> seen = [];
        store.HandOffRequested += () => seen.Add(store.HandOff);

        test.Run(cut, () => test.Manager.Close("middle"));
        test.Advance(cut, 1000);
        test.Run(cut, () => test.Manager.Close("oldest"));

        // middle → the next after it, oldest; newest (its timer) → past the ending middle, oldest; then none remain.
        Assert.Equal([new ToastHandOff(1, "oldest"), new ToastHandOff(2, "oldest"), new ToastHandOff(3, null)], seen);
    }
}

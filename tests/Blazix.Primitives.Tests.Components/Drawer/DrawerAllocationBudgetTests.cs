using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.JSInterop;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 for Drawer (decision 26 (a) of the M6 plan, chosen by the user 2026-11-18): the re-render of an open
/// drawer with three snap points and unchanged parameters sends nothing to any module and stays within its budget. The
/// <c>update</c> path with three snap points is the <c>drawer</c> module's (8b). Budgets are the least of five
/// measurements, in the non-parallel collection.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class DrawerAllocationBudgetTests : IDisposable
{
    // First measurement 2026-11-18 (arm64, .NET 11, Release), the least of five after twenty renders, each class in its own
    // process as allocation-budgets.mjs runs it: 3808 bytes in five of five runs for the re-render of the harness with a
    // root, a probe, a portal, a viewport, a popup and a close, three snap points and an equal list each time (the list is
    // the harness's field, so the root's sequence comparison returns at the reference check). No module is sent an update.
    private const long RerenderBudgetBytes = 3808;
    // M6 track 8b slice 5 (issue 279), first measurement 2026-11-21 (macOS arm64, .NET 11, Release, the class alone in its
    // process, the least of five): a snap point step, the `drawer` module's OnSnapPointRequest report of a release as Blazor
    // Server delivers it, measured inside the renderer's dispatcher call: the report's deserialization, the request, the
    // renders it causes and the part of the answering `update` that runs before its first await. 29,992 bytes in four of four
    // processes. The Viewport's options are a new record with the snap list sent as the consumer's, and the update's JSON.
    private const long SnapStepBudgetBytes = 29992;

    private readonly DialogTestContext test = new();
    private readonly BunitJSModuleInterop drawerModule;
    private readonly BunitJSModuleInterop drawerHandle;

    public DrawerAllocationBudgetTests()
    {
        drawerModule = test.Context.JSInterop.SetupModule("./_content/Blazix.Primitives/js/drawer.js");
        drawerHandle = drawerModule.SetupModule("attach", _ => true);
        drawerHandle.SetupVoid("update", _ => true).SetVoidResult();
        drawerHandle.SetupVoid("dispose", _ => true).SetVoidResult();
    }

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: a re-render of an open drawer with three snap points and unchanged parameters sends nothing and stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_rerender_of_an_open_drawer_sends_nothing_and_stays_within_its_budget()
    {
        Action<ComponentParameterCollectionBuilder<DrawerSnapHarness>> parameters = p => p.Add(x => x.Shape, "default");
        IRenderedComponent<DrawerSnapHarness> cut = test.Context.Render(parameters);
        Action render = () => cut.Render(parameters);
        for (int i = 0; i < 20; i++)
        {
            render();
        }

        long allocated = AllocationSampler.LeastOfFive(render);
        int sends = Sends();

        cut.Render(parameters);

        Assert.Equal(sends, Sends());
        AllocationSampler.AssertWithinBudget(allocated, RerenderBudgetBytes, "a re-render");
    }

    [Fact(DisplayName = "port: a snap point step on an open drawer with three snap points stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public async Task A_snap_point_step_stays_within_its_budget()
    {
        IRenderedComponent<DrawerSnapHarness> cut = test.Context.Render<DrawerSnapHarness>(p => p.Add(x => x.Shape, "default"));
        cut.WaitForAssertion(() => Assert.Contains(drawerModule.Invocations["attach"], attach => IsViewport(attach.Arguments[1])));
        DrawerTwin twin = ((DotNetObjectReference<DrawerTwin>)drawerModule.Invocations["attach"].First(attach => IsViewport(attach.Arguments[1])).Arguments[2]!).Value;
        string[] reports = [.. Enumerable.Range(1, 25).Select(i => $$"""{"request":{{i}},"index":{{(i % 2 == 1 ? 0 : 1)}},"event":{{ReleaseJson}}}""")];
        for (int i = 0; i < 20; i++)
        {
            string json = reports[i];
            await cut.InvokeAsync(() => twin.OnSnapPointRequest(json));
        }

        long allocated = long.MaxValue;
        for (int run = 0; run < AllocationSampler.Runs; run++)
        {
            string json = reports[20 + run];
            long sample = 0;
            await cut.InvokeAsync(() =>
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                twin.OnSnapPointRequest(json);
                sample = GC.GetAllocatedBytesForCurrentThread() - before;
            });
            allocated = Math.Min(allocated, sample);
        }

        Assert.Equal(25, cut.Instance.Requests.Count);
        AllocationSampler.AssertWithinBudget(allocated, SnapStepBudgetBytes, "a snap point step");
    }

    private const string ReleaseJson = """
        {"kind":"PointerEvent","type":"pointerup","key":null,"code":null,"altKey":false,"ctrlKey":false,
         "metaKey":false,"shiftKey":false,"pointerType":"mouse","detail":0,"buttons":0,
         "target":{"id":"base-ui-7","tagName":"DIV"},"defaultPrevented":false,"timeStamp":3.5}
        """;

    private static bool IsViewport(object? options) => options is string json && json.Contains("\"role\":\"viewport\"", StringComparison.Ordinal);

    // Slice 5: the drawer module's handles are counted too; an unchanged re-render sends them nothing.
    private int Sends() =>
        new[] { test.InteractionsHandle, test.FocusManagerHandle, test.TransitionsHandle, test.PortalHandle, test.ButtonHandle, test.ScrollLockHandle, drawerHandle }
            .Sum(handle => DialogTestContext.UpdateOptions(handle).Count);
}

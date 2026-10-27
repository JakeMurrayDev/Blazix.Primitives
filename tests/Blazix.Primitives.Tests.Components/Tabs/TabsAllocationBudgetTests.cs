using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using static Blazix.Primitives.Tests.Components.TabsTestContext;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 for Tabs: a re-render with unchanged parameters (the parent passing the same values again) of a root
/// with its list, three tabs, an indicator and three panels, and a committed activation without receivers (an
/// uncontrolled root with no <c>OnValueChange</c> or <c>ValueChanged</c>), as the <c>tabs</c> module reports it on Server.
/// Each is measured around bUnit's synchronous work on the test thread; the re-render includes Blazor's own per-render
/// cost and the twins' comparison of their options, which sends nothing. The budgets were taken from the first
/// measurement and only tighten afterwards.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class TabsAllocationBudgetTests : IDisposable
{
    // First measurement 2026-10-27 (arm64, .NET 11 RC2, Release). 2592 bytes for the re-render: the root, its cascade and
    // the list render again; the tabs, the indicator and the panels, whose parameters are unchanged values, do not.
    // 48184 bytes for the activation: 1120 of them are the report's deserialization (the same report for the already
    // active tab, which requests nothing, allocates exactly that), then the request and the renders of the two tabs whose
    // state changed; the root and the list do not render. A report arrives outside an event dispatch, so each tab's render
    // is its own batch, and bUnit rebuilds the root's markup after each one: sampled with the runtime's allocation ticks,
    // strings, char arrays and string builders from that markup are almost all of it.
    // 2026-10-27, track 1's E1 and E2 wired in: 2608 bytes. The library's share is unchanged (a probe of the parts' class
    // functions puts all 16 bytes before the root renders): bUnit's parameters lambda builds `x => x.DefaultValue` on every
    // render, and Expression.Property copies the host's array of properties, 8 bytes each, and TabsRoot gained two, the
    // injected IJSRuntime and PartIdGenerator of its list of panels (CompositeAllocationBudgetTests, 4032 to 4056, is the
    // precedent).
    // 2616 on 2026-10-27 (M4 track 1, the host wrapper of ADR 02 Amendments 2026-10-27): the same harness share again.
    // `PartBase` gained the protected `HostWrapperTagName`, 8 bytes in Expression.Property's copy; the library's share is
    // unchanged (CompositeAllocationBudgetTests' note of the same day has the probe).
    private const long RerenderBudgetBytes = 2616;
    private const long ActivationBudgetBytes = 48184;

    private readonly TabsTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "a re-render of a root with a list, tabs, an indicator and panels with unchanged parameters stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_rerender_with_unchanged_parameters_stays_within_its_budget()
    {
        RenderFragment content = Fragments(
            List(Tab(0), Tab(1), Tab(2), Part<TabsIndicator>(_ => { })),
            Panel(0),
            Panel(1),
            Panel(2));
        IRenderedComponent<TabsRoot<int>> cut = test.Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.DefaultValue, 1)
            .AddUnmatched("class", "consumer")
            .AddChildContent(content));

        for (int i = 0; i < 20; i++)
        {
            cut.Render(p => p.Add(x => x.DefaultValue, 1).AddUnmatched("class", "consumer").AddChildContent(content));
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        cut.Render(p => p.Add(x => x.DefaultValue, 1).AddUnmatched("class", "consumer").AddChildContent(content));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(allocated <= RerenderBudgetBytes, $"a re-render allocated {allocated} bytes; the budget is {RerenderBudgetBytes}");
    }

    [Fact(DisplayName = "a committed activation without receivers stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public async Task A_committed_activation_without_receivers_stays_within_its_budget()
    {
        IRenderedComponent<TabsRoot<int>> cut = test.Context.Render<TabsRoot<int>>(p => p
            .Add(x => x.DefaultValue, 0)
            .AddChildContent(List(Tab(0), Tab(1), Tab(2))));
        TabsTwin twin = test.ListTwin();
        string[] selections = [.. Tabs(cut).Take(2).Select(tab => $$"""{"tabId":"{{tab.Id}}","direction":"right","event":{{ClickJson(tab.Id)}}}""")];
        for (int i = 0; i < 20; i++)
        {
            await cut.InvokeAsync(() => twin.OnSelect(selections[(i + 1) % 2]));
        }

        // Twenty activations left tab 0 active; this one activates tab 1. bUnit's dispatcher runs the call inline, so the
        // measurement stays on the test thread.
        long before = GC.GetAllocatedBytesForCurrentThread();
        Task reported = cut.InvokeAsync(() => twin.OnSelect(selections[1]));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        await reported;

        Assert.Equal("true", Tabs(cut)[1].GetAttribute("aria-selected"));
        Assert.True(allocated <= ActivationBudgetBytes, $"an activation allocated {allocated} bytes; the budget is {ActivationBudgetBytes}");
    }
}

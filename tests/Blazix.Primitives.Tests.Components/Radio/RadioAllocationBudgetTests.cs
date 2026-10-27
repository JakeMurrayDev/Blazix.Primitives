using Blazix.Primitives.Interop;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rule 7 for Radio and RadioGroup: a re-render with unchanged parameters (the parent passing the same values again)
/// of a radio with its indicator inside a group, of a group of three radios, and an indicator status report that changes
/// nothing, each measured around bUnit's synchronous work on the test thread. The re-renders include Blazor's own
/// per-render cost and the twins' comparison of their options, which sends nothing. The budgets were taken from the first
/// measurement and only tighten afterwards.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class RadioAllocationBudgetTests : IDisposable
{
    // First measurement 2026-10-23 (arm64, .NET 11 RC2, Release): 1016 bytes for a radio and its indicator, Checkbox's 704
    // plus the composite item's registration check and the group's cascading parameters; 1008 bytes for a group of three
    // radios, whose child content Blazor renders again (each radio compares its state and its twins' options and sends
    // nothing); 328 bytes for the status report, Checkbox's: the source-generated parse of its JSON.
    private const long RadioRerenderBudgetBytes = 1016;
    private const long GroupRerenderBudgetBytes = 1008;
    private const long UnchangedStatusBudgetBytes = 328;

    private readonly RadioTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "a re-render of a radio and its indicator inside a group with unchanged parameters stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_rerender_of_a_radio_and_its_indicator_stays_within_its_budget()
    {
        EventCallback<MouseEventArgs> onClick = EventCallback.Factory.Create<MouseEventArgs>(this, () => { });
        IRenderedComponent<RadioGroup<string>> group = test.Context.Render<RadioGroup<string>>(p => p
            .Add(x => x.DefaultValue, "a")
            .Add(x => x.Name, "fruit")
            .AddChildContent<RadioRoot<string>>(r => r
                .Add(x => x.Value, "a")
                .AddUnmatched("class", "consumer")
                .AddUnmatched("aria-label", "Apple")
                .AddUnmatched("onclick", onClick)
                .AddChildContent<RadioIndicator>()));
        IRenderedComponent<RadioRoot<string>> cut = group.FindComponent<RadioRoot<string>>();

        long allocated = Measure(cut);

        Assert.True(allocated <= RadioRerenderBudgetBytes, $"a re-render allocated {allocated} bytes; the budget is {RadioRerenderBudgetBytes}");
    }

    [Fact(DisplayName = "a re-render of a group of three radios with unchanged parameters stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_rerender_of_a_group_of_three_radios_stays_within_its_budget()
    {
        RenderFragment radios = RadioTestContext.StringRadios("a", "b", "c");
        IRenderedComponent<RadioGroup<string>> cut = test.Context.Render<RadioGroup<string>>(p => p
            .Add(x => x.DefaultValue, "b")
            .Add(x => x.Name, "fruit")
            .AddUnmatched("class", "consumer")
            .AddChildContent(radios));

        long allocated = Measure(cut);

        Assert.True(allocated <= GroupRerenderBudgetBytes, $"a re-render allocated {allocated} bytes; the budget is {GroupRerenderBudgetBytes}");
    }

    [Fact(DisplayName = "an indicator status report that changes nothing stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public async Task An_indicator_status_report_that_changes_nothing_stays_within_its_budget()
    {
        IRenderedComponent<RadioRoot<string>> cut = test.Context.Render<RadioRoot<string>>(p => p
            .Add(x => x.Value, string.Empty)
            .AddChildContent<RadioIndicator>());
        TransitionsTwin twin = test.TransitionsTwin();
        const string unchanged = """{"status":null,"mounted":true}""";
        for (int i = 0; i < 20; i++)
        {
            await cut.InvokeAsync(() => twin.OnStatusChange(unchanged));
        }

        // bUnit's dispatcher runs the call inline, so the measurement stays on the test thread.
        long before = GC.GetAllocatedBytesForCurrentThread();
        Task reported = cut.InvokeAsync(() => twin.OnStatusChange(unchanged));
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        await reported;

        Assert.True(allocated <= UnchangedStatusBudgetBytes, $"a status report allocated {allocated} bytes; the budget is {UnchangedStatusBudgetBytes}");
    }

    private static long Measure<TComponent>(IRenderedComponent<TComponent> cut)
        where TComponent : IComponent
    {
        for (int i = 0; i < 20; i++)
        {
            cut.Render();
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        cut.Render();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}

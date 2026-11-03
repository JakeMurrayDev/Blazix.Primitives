using Blazix.Primitives.Interop;
using Bunit;

namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// ADR 16 rules 7 and 8 for the open lifecycle on the standalone test host: a re-render of the root with unchanged
/// parameters sends nothing and stays within its budget, measured around bUnit's synchronous render on the test thread as
/// the other popup budgets are; and a canceled request or a status report that changes nothing renders no part, counted
/// through the popup's <c>ClassValue</c> (the lesson of issue 110).
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class PopupLifecycleAllocationBudgetTests : IDisposable
{
    // First measurement 2026-11-04 (arm64, .NET 11 RC2, Release; M5 track 2): 1976 bytes, least of five, in 3 of 3 runs, for
    // the parent passing the same values again to a closed root with its popup: the root, its fixed cascade and the popup
    // part render; the session compares its inputs with the options last sent and sends nothing. The budget is that
    // envelope with the CI runner's +112 bytes floor shift that InteractionsAllocationBudgetTests records (2026-10-29), so
    // 2088. It only tightens.
    private const long RerenderBudgetBytes = 2088;

    private readonly LifecycleTestContext test = new();

    /// <inheritdoc />
    public void Dispose() => test.Dispose();

    [Fact(DisplayName = "port: a re-render of the standalone root with unchanged parameters sends nothing and stays within its budget")]
    [Trait("Requirement", "ADR16-7")]
    public void A_rerender_of_the_standalone_root_with_unchanged_parameters_sends_nothing_and_stays_within_its_budget()
    {
        Action<ComponentParameterCollectionBuilder<TestLifecycleRoot>> parameters = p => p.AddChildContent<TestLifecyclePopup>();
        IRenderedComponent<TestLifecycleRoot> cut = test.Context.Render(parameters);
        int updates = test.InteractionsHandle.Invocations["update"].Count;

        for (int i = 0; i < 20; i++)
        {
            cut.Render(parameters);
        }

        long allocated = AllocationSampler.LeastOfFive(() => cut.Render(parameters));

        Assert.Equal(updates, test.InteractionsHandle.Invocations["update"].Count);
        Assert.True(allocated <= RerenderBudgetBytes, $"a re-render allocated {allocated} bytes; the budget is {RerenderBudgetBytes}");
    }

    [Fact(DisplayName = "port: a committed open renders the popup once, and a canceled close or an unchanged status report renders neither the root nor the popup")]
    [Trait("Requirement", "ADR16-8")]
    [Trait("Requirement", "FLT-EVT-02")]
    public async Task A_committed_open_renders_the_popup_once_and_a_canceled_close_or_an_unchanged_status_report_renders_neither_the_root_nor_the_popup()
    {
        int classValues = 0;
        bool cancel = false;
        IRenderedComponent<TestLifecycleRoot> cut = test.Context.Render<TestLifecycleRoot>(p => p
            .Add(x => x.OnOpenChange, (ValueChangeEventArgs<bool> args) =>
            {
                if (cancel)
                {
                    args.Details.Cancel();
                }
            })
            .AddChildContent<TestLifecyclePopup>(popup => popup.Add(x => x.ClassValue, _ =>
            {
                classValues++;
                return null;
            })));
        TestLifecycleRoot root = cut.Instance;
        int rootRenders = root.Renders;

        await cut.InvokeAsync(() => root.RequestOpenAsync(true, new OpenChangeEventDetails(EventReason.TriggerPress)));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[role=\"dialog\"]")));
        Assert.Equal(1, classValues);
        TransitionsTwin transitions = cut.FindComponent<TestLifecyclePopup>().Instance.Transitions!;
        await cut.InvokeAsync(() => transitions.OnStatusChange("""{"status":"idle","mounted":true}"""));
        int afterIdle = classValues;

        cancel = true;
        await cut.InvokeAsync(() => root.RequestOpenAsync(false, new OpenChangeEventDetails(EventReason.EscapeKey)));
        await cut.InvokeAsync(() => transitions.OnStatusChange("""{"status":"idle","mounted":true}"""));

        Assert.Equal(afterIdle, classValues);
        Assert.Equal(rootRenders, root.Renders);
    }
}

using Blazix.Primitives.Store;

namespace Blazix.Primitives.Tests.Collapsible;

/// <summary>
/// ADR 16 rule 7 for Collapsible: a status report that changes nothing allocates nothing, and a committed trigger press
/// with no receivers allocates the new shared-state record and nothing of the request path. Measured on the shared state a
/// part subscribes to, with a subscription that has a selection, as a trigger or a panel holds one.
/// </summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class CollapsibleAllocationBudgetTests
{
    // First measurement 2026-10-21 (arm64, .NET 11 RC2, Release): 0 bytes.
    private const long UnchangedStatusBudgetBytes = 0;

    // First measurement 2026-10-21 (arm64, .NET 11 RC2, Release): 40 bytes, the new shared-state record. The request's
    // details are created before the measurement, as the trigger's handler creates them from the click; the request path
    // (the change callback, the binding callback, the commit) completes synchronously and allocates nothing.
    private const long CommittedPressBudgetBytes = 40;

    [Fact(DisplayName = "a status report that changes no selection allocates nothing")]
    [Trait("Requirement", "ADR16-7")]
    public void A_status_report_that_changes_no_selection_allocates_nothing()
    {
        CollapsibleRootContext root = Opened();
        using SelectorSubscription<CollapsibleRootStoreState> subscription = new(root, () => { });
        subscription.Select(CollapsibleRootStoreState.StatusSelector);

        long allocated = Measure(() => root.MirrorStatus(TransitionStatus.Idle));

        Assert.True(allocated <= UnchangedStatusBudgetBytes, $"an unchanged status report allocated {allocated} bytes; the budget is {UnchangedStatusBudgetBytes}");
    }

    [Fact(DisplayName = "a committed trigger press with no receivers allocates only the new shared state")]
    [Trait("Requirement", "ADR16-7")]
    public void A_committed_trigger_press_with_no_receivers_allocates_only_the_new_shared_state()
    {
        CollapsibleRootContext root = Opened();
        using SelectorSubscription<CollapsibleRootStoreState> subscription = new(root, () => { });
        subscription.Select(CollapsibleRootStoreState.OpenSelector);
        ChangeEventDetails details = new(EventReason.TriggerPress);

        long allocated = Measure(() => root.RequestOpenAsync(!root.State.Open, details).GetAwaiter().GetResult());

        Assert.True(allocated <= CommittedPressBudgetBytes, $"a committed press allocated {allocated} bytes; the budget is {CommittedPressBudgetBytes}");
    }

    private static CollapsibleRootContext Opened()
    {
        CollapsibleRootContext root = new("base-ui-1", logger: null);
        root.OpenState.Update(valueSupplied: false, value: false, defaultValueSupplied: true, defaultValue: true);
        root.ApplyParameters(disabled: false, firstSet: true);
        return root;
    }

    private static long Measure(Action action)
    {
        for (int i = 0; i < 20; i++)
        {
            action();
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        action();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}

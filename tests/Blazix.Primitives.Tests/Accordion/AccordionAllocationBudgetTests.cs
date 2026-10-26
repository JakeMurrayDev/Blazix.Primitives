namespace Blazix.Primitives.Tests.Accordion;

/// <summary>
/// ADR 16 rule 7 for Accordion, on the shared state the parts subscribe to: a parameter set that changes nothing allocates
/// nothing, and a committed toggle with no receivers allocates the new value and each item's new records. The toggle is
/// measured at 3 and 20 items for design point D4 (option (a), pending the user).
/// </summary>
public sealed class AccordionAllocationBudgetTests
{
    // First measurement 2026-10-26 (arm64, .NET 11 RC2, Release): 0 bytes.
    private const long UnchangedParametersBudgetBytes = 0;

    [Fact(DisplayName = "a parameter set that changes nothing allocates nothing")]
    [Trait("Requirement", "ADR16-7")]
    public void A_parameter_set_that_changes_nothing_allocates_nothing()
    {
        (AccordionRootContext<string> root, _) = Build(3);

        long allocated = Measure(() => root.ApplyParameters(multiple: false, disabled: false, Orientation.Vertical, keepMounted: false, hiddenUntilFound: false));

        Assert.True(allocated <= UnchangedParametersBudgetBytes, $"an unchanged parameter set allocated {allocated} bytes; the budget is {UnchangedParametersBudgetBytes}");
    }

    // First measurement 2026-10-26 (arm64, .NET 11 RC2, Release): 352 bytes at 3 items, 1712 at 20. The value list, every item's
    // new state record (its value changed), and the opened and closed items' collapsible records; the request path (the change
    // callback, the binding callback, the commit) completes synchronously.
    [Theory(DisplayName = "a committed toggle with no receivers allocates the new value and the items' new records")]
    [InlineData(3, 352)]
    [InlineData(20, 1712)]
    [Trait("Requirement", "ADR16-7")]
    public void A_committed_toggle_with_no_receivers_allocates_the_new_value_and_the_items_records(int items, long budgetBytes)
    {
        (AccordionRootContext<string> root, AccordionItemContext<string>[] contexts) = Build(items);
        ChangeEventDetails details = new(EventReason.TriggerPress);
        int next = 0;

        long allocated = Measure(() =>
        {
            AccordionItemContext<string> item = contexts[next++ % 2];
            item.Collapsible.RequestOpenAsync(!item.Collapsible.State.Open, details).GetAwaiter().GetResult();
        });

        Assert.True(allocated <= budgetBytes, $"a committed toggle at {items} items allocated {allocated} bytes; the budget is {budgetBytes}");
    }

    private static (AccordionRootContext<string> Root, AccordionItemContext<string>[] Items) Build(int count)
    {
        AccordionRootContext<string> root = new(logger: null);
        root.ValueState.Update(false, null!, false, null!);
        root.ApplyParameters(multiple: false, disabled: false, Orientation.Vertical, keepMounted: false, hiddenUntilFound: false);
        AccordionItemContext<string>[] items = new AccordionItemContext<string>[count];
        for (int i = 0; i < count; i++)
        {
            CollapsibleRootContext collapsible = new($"base-ui-panel-{i}", logger: null);
            items[i] = new AccordionItemContext<string>(root, collapsible, $"base-ui-trigger-{i}");
            items[i].ApplyParameters($"item-{i}", itemDisabled: false);
        }

        return (root, items);
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

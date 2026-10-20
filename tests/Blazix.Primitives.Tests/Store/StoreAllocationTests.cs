using Blazix.Primitives.Store;

namespace Blazix.Primitives.Tests.Store;

/// <summary>
/// The store notification budget of ADR 16 rule 7: <c>GC.GetAllocatedBytesForCurrentThread()</c> around a write that
/// notifies N subscribers whose selections did not change. Budgets only tighten.
/// </summary>
public sealed class StoreAllocationTests
{
    private const int Subscribers = 64;
    private const long NotificationBudgetBytes = 0;

    [Fact(DisplayName = "a notification whose selections did not change allocates nothing")]
    [Trait("Requirement", "ADR16-7")]
    public void A_notification_whose_selections_did_not_change_allocates_nothing()
    {
        ComponentStore<CounterState> store = new(new CounterState(1, 1));
        int renders = 0;
        List<SelectorSubscription<CounterState>> subscriptions = [];
        for (int i = 0; i < Subscribers; i++)
        {
            SelectorSubscription<CounterState> subscription = new(store, () => renders++);
            subscription.Select(CounterState.CountSelector);
            subscription.Select(CounterState.Doubled);
            subscriptions.Add(subscription);
        }

        // The records are made before the measurement: a write allocates its new record, the notification nothing.
        CounterState[] records = new CounterState[16];
        for (int i = 0; i < records.Length; i++)
        {
            records[i] = new CounterState(1, i + 2);
        }

        store.SetState(records[0]);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 1; i < records.Length; i++)
        {
            store.SetState(records[i]);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, renders);
        Assert.True(allocated <= NotificationBudgetBytes, $"{allocated} bytes allocated over {records.Length - 1} notifications to {Subscribers} subscribers; budget {NotificationBudgetBytes}.");
        GC.KeepAlive(subscriptions);
    }

    [Fact(DisplayName = "Object.is on value types does not box")]
    [Trait("Requirement", "ADR16-7")]
    public void Object_is_on_value_types_does_not_box()
    {
        bool result = false;
        for (int i = 0; i < 4; i++)
        {
            result ^= Measure();
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            result ^= Measure();
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        GC.KeepAlive(result);

        static bool Measure() =>
            Equality.Is(1, 2) ^ Equality.Is(0.0, -0.0) ^ Equality.Is<double?>(double.NaN, null) ^ Equality.Is(EventReason.Drag, EventReason.Wheel)
            ^ Equality.Is((1, 2.0), (1, 2.0)) ^ Equality.Is("a", "b");
    }
}

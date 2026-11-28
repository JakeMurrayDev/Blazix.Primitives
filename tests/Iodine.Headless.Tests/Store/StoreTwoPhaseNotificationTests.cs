using Iodine.Headless.Store;

namespace Iodine.Headless.Tests.Store;

/// <summary>
/// UTL-STA-26 (reads never tear) as built, issue 417 (design point 10-142b (c), chosen by the user 2026-11-28; ADR 05
/// Amendments 2026-11-28): a write refreshes every <see cref="SelectorSubscription{TState}"/> of the store before it calls
/// any callback, so a callback that renders synchronously (a part's <c>StateHasChanged</c> outside a render batch) and
/// re-renders another part reads that part's selections for the new record, as React's batched render reads every
/// <c>useStore</c> snapshot of the commit. A nested write keeps UTL-ALG-03: its pass completes, then the outer one stops.
/// </summary>
public sealed class StoreTwoPhaseNotificationTests
{
    [Fact(DisplayName = "port: a subscription's callback reads a later subscription of the store already refreshed (issue 417)")]
    [Trait("Requirement", "UTL-STA-26")]
    public void A_callback_reads_a_later_subscription_already_refreshed()
    {
        ComponentStore<CounterState> store = new(new CounterState(5, 3));
        StoreSelection<CounterState, int>? later = null;
        List<int> seen = [];
        using SelectorSubscription<CounterState> first = new(store, () => seen.Add(later!.Value));
        first.Select(CounterState.CountSelector);
        using SelectorSubscription<CounterState> second = new(store, () => { });
        later = second.Select(CounterState.Doubled);

        store.Set(CounterState.CountField, 6);

        Assert.Equal([12], seen);
    }

    [Fact(DisplayName = "port: a callback reached after a nested write runs once, in the nested pass (issue 417)")]
    [Trait("Requirement", "UTL-STA-09")]
    [Trait("Requirement", "UTL-ALG-03")]
    [Trait("Coverage", "issue-417")]
    public void A_callback_refreshed_before_a_nested_write_runs_once_in_the_nested_pass()
    {
        ComponentStore<CounterState> store = new(new CounterState(5, 3));
        List<string> calls = [];
        StoreSelection<CounterState, int>? count = null;
        using SelectorSubscription<CounterState> writer = new(store, () =>
        {
            calls.Add("writer");
            if (store.State.Multiplier == 3)
            {
                store.Set(CounterState.MultiplierField, 4);
            }
        });
        writer.Select(CounterState.CountSelector);
        using SelectorSubscription<CounterState> reader = new(store, () => calls.Add($"reader {count!.Value}"));
        count = reader.Select(CounterState.CountSelector);

        // The first write changes both selections; the writer's callback writes a member neither selects. The reader's
        // callback still runs once, in the nested pass, and the outer pass does not run it again.
        store.Set(CounterState.CountField, 6);

        Assert.Equal(["writer", "reader 6"], calls);
        Assert.Equal(new CounterState(6, 4), store.State);
    }

    [Fact(DisplayName = "port: a subscription disposed by an earlier callback of the pass gets no callback (issue 417)")]
    [Trait("Requirement", "UTL-STA-13")]
    [Trait("Coverage", "issue-417")]
    public void A_subscription_disposed_by_an_earlier_callback_gets_no_callback()
    {
        ComponentStore<CounterState> store = new(new CounterState(5, 3));
        int laterCalls = 0;
        SelectorSubscription<CounterState>? later = null;
        using SelectorSubscription<CounterState> first = new(store, () => later!.Dispose());
        first.Select(CounterState.CountSelector);
        later = new SelectorSubscription<CounterState>(store, () => laterCalls++);
        later.Select(CounterState.CountSelector);

        store.Set(CounterState.CountField, 6);

        Assert.Equal(0, laterCalls);
    }
}

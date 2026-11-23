using Blazix.Primitives.Store;

namespace Blazix.Primitives.Tests.Store;

/// <summary>
/// The part subscription of ADR 05 §1 (UTL-STA-25..27): one store subscription per owner, every selection re-evaluated per
/// notification, the callback (a part's <c>StateHasChanged</c>) only when a selection changed. The L2 render-count test of
/// the wiring into <c>PartBase</c> follows Track 1's merge.
/// </summary>
public sealed class SelectorSubscriptionTests
{
    [Fact(DisplayName = "re-renders only when a selection changed")]
    [Trait("Requirement", "UTL-STA-25")]
    public void Re_renders_only_when_a_selection_changed()
    {
        ComponentStore<CounterState> store = new(new CounterState(5, 3));
        int renders = 0;
        using SelectorSubscription<CounterState> subscription = new(store, () => renders++);
        StoreSelection<CounterState, int> doubled = subscription.Select(CounterState.Doubled);

        store.Set(CounterState.MultiplierField, 4);
        Assert.Equal(0, renders);
        Assert.Equal(10, doubled.Value);

        store.Set(CounterState.CountField, 6);
        Assert.Equal(1, renders);
        Assert.Equal(12, doubled.Value);
    }

    [Fact(DisplayName = "one subscription per owner however many selections")]
    [Trait("Requirement", "UTL-STA-27")]
    public void One_subscription_per_owner_however_many_selections()
    {
        ComponentStore<CounterState> store = new(new CounterState(5, 3));
        int renders = 0;
        using SelectorSubscription<CounterState> subscription = new(store, () => renders++);
        StoreSelection<CounterState, int> count = subscription.Select(CounterState.CountSelector);
        StoreSelection<CounterState, int> doubled = subscription.Select(CounterState.Doubled);
        StoreSelection<CounterState, int> multiplied = subscription.Select(CounterState.Multiplied);

        // Three selections change on one write: one callback, as one subscription per read would also yield one render.
        store.Set(CounterState.CountField, 6);
        Assert.Equal(1, renders);
        Assert.Equal((6, 12, 18), (count.Value, doubled.Value, multiplied.Value));

        // One selection changes: one callback.
        store.Set(CounterState.MultiplierField, 1);
        Assert.Equal(2, renders);
        Assert.Equal((6, 12, 6), (count.Value, doubled.Value, multiplied.Value));
    }

    [Fact(DisplayName = "a selection reads the record current at the time it is made")]
    [Trait("Requirement", "UTL-STA-26")]
    public void A_selection_reads_the_record_current_at_the_time_it_is_made()
    {
        ComponentStore<CounterState> store = new(new CounterState(5, 3));
        int renders = 0;
        using SelectorSubscription<CounterState> subscription = new(store, () => renders++);
        store.Set(CounterState.CountField, 7);

        StoreSelection<CounterState, int> count = subscription.Select(CounterState.CountSelector);

        Assert.Equal(7, count.Value);
        Assert.Equal(0, renders);
    }

    [Fact(DisplayName = "changing a selection argument re-evaluates without a callback")]
    [Trait("Requirement", "UTL-STA-18")]
    public void Changing_a_selection_argument_re_evaluates_without_a_callback()
    {
        ComponentStore<ValuesState> store = new(new ValuesState(new Dictionary<string, string> { ["first"] = "one", ["second"] = "two" }));
        int renders = 0;
        using SelectorSubscription<ValuesState> subscription = new(store, () => renders++);
        StoreSelection<ValuesState, string, string?> value = subscription.Select(ValuesState.ValueByKey, "first");

        value.Arg = "second";

        Assert.Equal("two", value.Value);
        Assert.Equal("second", value.Arg);
        Assert.Equal(0, renders);
    }

    [Fact(DisplayName = "a disposed subscription no longer calls back")]
    [Trait("Requirement", "UTL-STA-25")]
    public void A_disposed_subscription_no_longer_calls_back()
    {
        ComponentStore<CounterState> store = new(new CounterState(5, 3));
        int renders = 0;
        SelectorSubscription<CounterState> subscription = new(store, () => renders++);
        subscription.Select(CounterState.CountSelector);

        subscription.Dispose();
        store.Set(CounterState.CountField, 6);

        Assert.Equal(0, renders);
    }

    [Fact(DisplayName = "a selection compares by Object.is")]
    [Trait("Requirement", "UTL-STA-25")]
    public void A_selection_compares_by_Object_is()
    {
        ComponentStore<ShapeState> store = new(new ShapeState(new Point(0, 0), double.NaN, null));
        int renders = 0;
        using SelectorSubscription<ShapeState> subscription = new(store, () => renders++);
        subscription.Select(static (ShapeState s) => s.Origin);
        subscription.Select(static (ShapeState s) => s.Ratio);

        store.Set(ShapeState.RatioField, double.NaN);
        store.Update(s => s with { Node = "x" });
        Assert.Equal(0, renders);

        store.Set(ShapeState.OriginField, new Point(0, 0));
        Assert.Equal(1, renders);
    }
}

using Blazix.Primitives.Store;

namespace Blazix.Primitives.Tests.Store;

/// <summary>
/// <c>packages/utils/src/store/Store.test.ts</c> (UTL-STA-07..14, UTL-ALG-03). <c>Store.create(state)</c> is
/// <c>new Store&lt;TState&gt;(state)</c> in C#, and a subclass is constructed with its own constructor.
/// </summary>
public sealed class StoreTests
{
    [Fact(DisplayName = "returns a Store instance seeded with the given state")]
    [Trait("Requirement", "UTL-STA-14")]
    public void Returns_a_Store_instance_seeded_with_the_given_state()
    {
        TestState initial = new(1, "a");
        Store<TestState> store = new(initial);

        Assert.IsType<Store<TestState>>(store);
        Assert.Same(initial, store.State);
    }

    [Fact(DisplayName = "produces an independent instance per call")]
    [Trait("Requirement", "UTL-STA-14")]
    public void Produces_an_independent_instance_per_call()
    {
        TestState initial = new(0, "a");
        Store<TestState> first = new(initial);
        Store<TestState> second = new(initial);

        first.Set(TestState.ValueField, 1);

        Assert.Equal(1, first.State.Value);
        Assert.Equal(0, second.State.Value);
    }

    [Fact(DisplayName = "constructs an instance of the subclass it is called on")]
    [Trait("Requirement", "UTL-STA-14")]
    public void Constructs_an_instance_of_the_subclass_it_is_called_on()
    {
        SubStore store = new(new CounterState(1, 1));

        store.Increment();

        Assert.Equal(2, store.State.Count);
    }

    [Fact(DisplayName = "notifies subscribers with the new state")]
    [Trait("Requirement", "UTL-STA-07")]
    [Trait("Requirement", "UTL-STA-08")]
    public void Notifies_subscribers_with_the_new_state()
    {
        Store<TestState> store = new(new TestState(0, "a"));
        List<TestState> calls = [];
        store.Subscribe(calls.Add);
        TestState next = new(1, "a");

        store.SetState(next);

        Assert.Equal([next], calls);
        Assert.Equal(1, store.State.Value);
    }

    [Fact(DisplayName = "does not notify when setState receives the current state reference")]
    [Trait("Requirement", "UTL-STA-08")]
    public void Does_not_notify_when_setState_receives_the_current_state_reference()
    {
        Store<TestState> store = new(new TestState(0, "a"));
        List<TestState> calls = [];
        store.Subscribe(calls.Add);

        store.SetState(store.State);

        Assert.Empty(calls);
    }

    [Fact(DisplayName = "unsubscribing stops notifications")]
    [Trait("Requirement", "UTL-STA-13")]
    public void Unsubscribing_stops_notifications()
    {
        Store<TestState> store = new(new TestState(0, "a"));
        List<TestState> calls = [];
        IDisposable unsubscribe = store.Subscribe(calls.Add);

        unsubscribe.Dispose();
        store.Set(TestState.ValueField, 1);

        Assert.Empty(calls);
    }

    [Fact(DisplayName = "set() writes a single key and skips same-value writes")]
    [Trait("Requirement", "UTL-STA-10")]
    public void Set_writes_a_single_key_and_skips_same_value_writes()
    {
        Store<TestState> store = new(new TestState(0, "a"));
        int calls = 0;
        store.Subscribe(_ => calls++);

        store.Set(TestState.ValueField, 1);
        Assert.Equal(new TestState(1, "a"), store.State);
        Assert.Equal(1, calls);

        store.Set(TestState.ValueField, 1);
        Assert.Equal(1, calls);
    }

    [Fact(DisplayName = "update() merges changed keys and skips no-op updates")]
    [Trait("Requirement", "UTL-STA-11")]
    public void Update_merges_changed_keys_and_skips_no_op_updates()
    {
        Store<TestState> store = new(new TestState(0, "a"));
        int calls = 0;
        store.Subscribe(_ => calls++);

        store.Update(s => s with { Value = 2, Label = "b" });
        Assert.Equal(new TestState(2, "b"), store.State);
        Assert.Equal(1, calls);

        store.Update(s => s with { Value = 2, Label = "b" });
        Assert.Equal(1, calls);
    }

    [Fact(DisplayName = "notifyAll() renews the state reference and notifies")]
    [Trait("Requirement", "UTL-STA-12")]
    public void NotifyAll_renews_the_state_reference_and_notifies()
    {
        Store<TestState> store = new(new TestState(0, "a"));
        int calls = 0;
        store.Subscribe(_ => calls++);
        TestState previous = store.State;

        store.NotifyAll();

        Assert.Equal(1, calls);
        Assert.NotSame(previous, store.State);
        Assert.Equal(previous, store.State);
    }

    [Fact(DisplayName = "a nested setState from a listener stops the outer notification pass")]
    [Trait("Requirement", "UTL-STA-09")]
    [Trait("Requirement", "UTL-ALG-03")]
    public void A_nested_setState_from_a_listener_stops_the_outer_notification_pass()
    {
        Store<TestState> store = new(new TestState(0, "a"));
        List<TestState> first = [];
        List<TestState> second = [];
        store.Subscribe(state =>
        {
            first.Add(state);
            if (state.Value == 1)
            {
                store.Set(TestState.ValueField, 2);
            }
        });
        store.Subscribe(second.Add);

        store.Set(TestState.ValueField, 1);

        // The nested set() notified every listener with the final state; the outer pass detected it and did not deliver
        // the stale state to `second`.
        Assert.Equal(2, store.State.Value);
        Assert.Equal(2, first.Count);
        Assert.Equal([new TestState(2, "a")], second);
    }

    // Port tests: the live ordered set of UTL §8.2 (no Base UI title).

    [Fact(DisplayName = "notifies listeners in subscription order")]
    [Trait("Requirement", "UTL-STA-08")]
    public void Notifies_listeners_in_subscription_order()
    {
        Store<TestState> store = new(new TestState(0, "a"));
        List<string> order = [];
        store.Subscribe(_ => order.Add("first"));
        store.Subscribe(_ => order.Add("second"));
        store.Subscribe(_ => order.Add("third"));

        store.Set(TestState.ValueField, 1);

        Assert.Equal(["first", "second", "third"], order);
    }

    [Fact(DisplayName = "registering the same listener twice notifies it once")]
    [Trait("Requirement", "UTL-STA-13")]
    public void Registering_the_same_listener_twice_notifies_it_once()
    {
        Store<TestState> store = new(new TestState(0, "a"));
        int calls = 0;
        Action<TestState> listener = _ => calls++;
        store.Subscribe(listener);
        store.Subscribe(listener);

        store.Set(TestState.ValueField, 1);

        Assert.Equal(1, calls);
    }

    [Fact(DisplayName = "a listener unsubscribed during a pass is not reached, and one subscribed during a pass is")]
    [Trait("Requirement", "UTL-STA-13")]
    public void A_listener_unsubscribed_during_a_pass_is_not_reached_and_one_subscribed_during_a_pass_is()
    {
        Store<TestState> store = new(new TestState(0, "a"));
        List<string> order = [];
        IDisposable? second = null;
        bool added = false;
        store.Subscribe(_ =>
        {
            order.Add("first");
            second!.Dispose();
            if (!added)
            {
                added = true;
                store.Subscribe(_ => order.Add("late"));
            }
        });
        second = store.Subscribe(_ => order.Add("second"));

        store.Set(TestState.ValueField, 1);

        Assert.Equal(["first", "late"], order);
    }

    [Fact(DisplayName = "set() compares records by reference, never structurally")]
    [Trait("Requirement", "UTL-STA-10")]
    public void Set_compares_records_by_reference_never_structurally()
    {
        Store<ShapeState> store = new(new ShapeState(new Point(1, 2), 1, null));
        int calls = 0;
        store.Subscribe(_ => calls++);

        store.Set(ShapeState.OriginField, new Point(1, 2));
        Assert.Equal(1, calls);

        store.Set(ShapeState.OriginField, store.State.Origin);
        Assert.Equal(1, calls);
    }

    [Fact(DisplayName = "set() follows Object.is for NaN and -0")]
    [Trait("Requirement", "UTL-STA-10")]
    public void Set_follows_Object_is_for_NaN_and_negative_zero()
    {
        Store<ShapeState> store = new(new ShapeState(new Point(0, 0), double.NaN, null));
        int calls = 0;
        store.Subscribe(_ => calls++);

        store.Set(ShapeState.RatioField, double.NaN);
        Assert.Equal(0, calls);

        store.Set(ShapeState.RatioField, 0.0);
        store.Set(ShapeState.RatioField, -0.0);
        Assert.Equal(2, calls);
        Assert.True(double.IsNegative(store.State.Ratio));
    }

    private sealed class SubStore(CounterState state) : Store<CounterState>(state)
    {
        public void Increment() => Set(CounterState.CountField, State.Count + 1);
    }
}

using Blazix.Primitives.Store;
using Blazix.Primitives.Tests.TestSupport;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Store;

/// <summary>
/// <c>packages/utils/src/store/ReactStore.test.tsx</c> (UTL-STA-15..24, UTL-EDGE-25). The React test components become
/// fake parts: each takes a <see cref="ParameterView"/> as <c>SetParametersAsync</c> would and runs the sync step of its
/// <c>OnParametersSet</c> (ADR 05 §1, ADR 01 decision 4); <c>setProps</c> is a second parameter set. The reactive reads
/// (<c>useState</c>) are a <see cref="SelectorSubscription{TState}"/> whose callback counts renders. Not translated:
/// "warns if useSyncedValues keys change between renders" (the keys of <see cref="ComponentStore{TState}.SyncValues{TValues}"/>
/// are fixed by its code, so UTL-EDGE-26 cannot occur) and "useStateSetter returns a stable callback that updates the store
/// state" (UTL-STA-23: a part keeps one <c>value =&gt; store.Set(field, value)</c> delegate in a field, ADR 16 rule 2).
/// </summary>
public sealed class ComponentStoreTests
{
    [Fact(DisplayName = "create() constructs a fully wired ReactStore instance")]
    [Trait("Requirement", "UTL-STA-14")]
    [Trait("Requirement", "UTL-STA-15")]
    public void Create_constructs_a_fully_wired_ReactStore_instance()
    {
        ComponentStore<TestState> store = new(new TestState(1, "a"));

        Assert.IsType<ComponentStore<TestState>>(store);
        Assert.Equal(1, store.State.Value);
        // The context bag is the subclass's own fields; the base store has none.
        Assert.Empty(typeof(ComponentStore<TestState>).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance));
    }

    [Fact(DisplayName = "writing the context bag never notifies")]
    [Trait("Requirement", "UTL-STA-15")]
    public void Writing_the_context_bag_never_notifies()
    {
        ContextStore store = new(new TestState(0, "a"));
        int calls = 0;
        store.Subscribe(_ => calls++);

        store.PopupId = "popup";
        store.OnOpenChange = () => { };

        Assert.Equal(0, calls);
    }

    [Fact(DisplayName = "syncs internal state from controlled prop")]
    [Trait("Requirement", "UTL-STA-22")]
    public void Syncs_internal_state_from_controlled_prop()
    {
        ComponentStore<TestState> store = new(new TestState(0, ""));
        ControlledPropPart part = new(store);

        part.SetParameters(Parameters(("Controlled", 1)));
        Assert.Equal(1, store.State.Value);

        store.Update(s => s with { Label = "y" });
        // Non-controlled keys still update
        Assert.Equal("y", store.State.Label);

        // Changing the controlled prop updates internal state
        part.SetParameters(Parameters(("Controlled", 7)));
        Assert.Equal(7, store.State.Value);
    }

    [Fact(DisplayName = "syncs internal state from controlled prop when the store changes")]
    [Trait("Requirement", "UTL-STA-22")]
    public void Syncs_internal_state_from_controlled_prop_when_the_store_changes()
    {
        ComponentStore<TestState> firstStore = new(new TestState(0, ""));
        ComponentStore<TestState> secondStore = new(new TestState(0, ""));
        ControlledPropPart part = new(firstStore);

        part.SetParameters(Parameters(("Store", firstStore), ("Controlled", 1)));
        Assert.Equal(1, firstStore.State.Value);
        Assert.Equal(0, secondStore.State.Value);

        part.SetParameters(Parameters(("Store", secondStore), ("Controlled", 1)));
        Assert.Equal(1, secondStore.State.Value);
    }

    [Fact(DisplayName = "warns on switching from uncontrolled to controlled")]
    [Trait("Requirement", "UTL-EDGE-25")]
    public void Warns_on_switching_from_uncontrolled_to_controlled()
    {
        CapturingLogger logger = new();
        ComponentStore<TestState> store = new(new TestState(0, "")) { Logger = logger };
        ControlledPropPart part = new(store);

        part.SetParameters(Parameters());
        Assert.Empty(logger.Messages);

        part.SetParameters(Parameters(("Controlled", 1)));
        Assert.Equal(
            ["A component is changing the uncontrolled state of Value to be controlled. Elements should not switch from uncontrolled to controlled (or vice versa)."],
            logger.Messages);
    }

    [Fact(DisplayName = "warns on switching from controlled to uncontrolled")]
    [Trait("Requirement", "UTL-EDGE-25")]
    public void Warns_on_switching_from_controlled_to_uncontrolled()
    {
        CapturingLogger logger = new();
        ComponentStore<TestState> store = new(new TestState(0, "")) { Logger = logger };
        ControlledPropPart part = new(store);

        part.SetParameters(Parameters(("Controlled", 1)));
        Assert.Empty(logger.Messages);

        // UTL-EDGE-25 is not deduplicated: every parameter set in the switched mode warns.
        part.SetParameters(Parameters());
        part.SetParameters(Parameters());
        Assert.Equal(
            [
                "A component is changing the controlled state of Value to be uncontrolled. Elements should not switch from uncontrolled to controlled (or vice versa).",
                "A component is changing the controlled state of Value to be uncontrolled. Elements should not switch from uncontrolled to controlled (or vice versa).",
            ],
            logger.Messages);
    }

    [Fact(DisplayName = "useProp updates a single key when the passed value changes")]
    [Trait("Requirement", "UTL-STA-19")]
    public void UseProp_updates_a_single_key_when_the_passed_value_changes()
    {
        ComponentStore<TestState> store = new(new TestState(0, ""));
        SyncedValuePart part = new(store);

        part.SetParameters(Parameters(("Value", 1)));
        Assert.Equal(1, store.State.Value);

        part.SetParameters(Parameters(("Value", 2)));
        Assert.Equal(2, store.State.Value);
    }

    [Fact(DisplayName = "useProp syncs the same value when the store changes")]
    [Trait("Requirement", "UTL-STA-19")]
    public void UseProp_syncs_the_same_value_when_the_store_changes()
    {
        ComponentStore<TestState> firstStore = new(new TestState(0, ""));
        ComponentStore<TestState> secondStore = new(new TestState(0, ""));
        SyncedValuePart part = new(firstStore);

        part.SetParameters(Parameters(("Store", firstStore), ("Value", 1)));
        Assert.Equal(1, firstStore.State.Value);
        Assert.Equal(0, secondStore.State.Value);

        part.SetParameters(Parameters(("Store", secondStore), ("Value", 1)));
        Assert.Equal(1, secondStore.State.Value);
    }

    [Fact(DisplayName = "useProps applies multiple keys from a props object")]
    [Trait("Requirement", "UTL-STA-21")]
    public void UseProps_applies_multiple_keys_from_a_props_object()
    {
        ComponentStore<TestState> store = new(new TestState(0, ""));
        SyncedValuesPart part = new(store);

        part.SetParameters(Parameters(("Value", 5), ("Label", "a")));
        Assert.Equal(5, store.State.Value);
        Assert.Equal("a", store.State.Label);

        part.SetParameters(Parameters(("Value", 6), ("Label", "b")));
        Assert.Equal(6, store.State.Value);
        Assert.Equal("b", store.State.Label);
    }

    [Fact(DisplayName = "useSyncedValues depends on entries instead of object identity")]
    [Trait("Requirement", "UTL-STA-21")]
    public void UseSyncedValues_depends_on_entries_instead_of_object_identity()
    {
        // Base UI spies on update(); the port's observable equivalent is the store notification it would cause.
        ComponentStore<TestState> store = new(new TestState(0, ""));
        int notifications = 0;
        store.Subscribe(_ => notifications++);
        SyncedValuesPart part = new(store);

        part.SetParameters(Parameters(("Value", 5), ("Label", "a")));
        Assert.Equal(1, notifications);
        TestState afterFirst = store.State;

        part.SetParameters(Parameters(("Value", 5), ("Label", new string('a', 1))));
        Assert.Equal(1, notifications);
        Assert.Same(afterFirst, store.State);

        part.SetParameters(Parameters(("Value", 6), ("Label", "a")));
        Assert.Equal(2, notifications);
        Assert.Equal(6, store.State.Value);
    }

    [Fact(DisplayName = "useSyncedValueWithCleanup synchronizes value and resets on cleanup")]
    [Trait("Requirement", "UTL-STA-20")]
    public void UseSyncedValueWithCleanup_synchronizes_value_and_resets_on_cleanup()
    {
        ComponentStore<ShapeState> store = new(new ShapeState(new Point(0, 0), 0, null));
        SyncedValueWithCleanup<ShapeState, string> node = new(ShapeState.NodeField);

        node.Sync(store, "first-node");
        Assert.Equal("first-node", store.State.Node);

        node.Sync(store, "second-node");
        Assert.Equal("second-node", store.State.Node);

        node.Dispose();
        Assert.Null(store.State.Node);
    }

    [Fact(DisplayName = "useSyncedValueWithCleanup resets the previous store when the store changes")]
    [Trait("Requirement", "UTL-STA-20")]
    public void UseSyncedValueWithCleanup_resets_the_previous_store_when_the_store_changes()
    {
        ComponentStore<ShapeState> firstStore = new(new ShapeState(new Point(0, 0), 0, null));
        ComponentStore<ShapeState> secondStore = new(new ShapeState(new Point(0, 0), 0, null));
        SyncedValueWithCleanup<ShapeState, string> node = new(ShapeState.NodeField);

        node.Sync(firstStore, "node");
        node.Sync(secondStore, "node");

        Assert.Null(firstStore.State.Node);
        Assert.Equal("node", secondStore.State.Node);
    }

    // UTL-STA-20 in Blazor's order (fidelity correction 2026-10-28): a keyed replacement syncs before the replaced part is
    // disposed, where React runs the cleanup first.
    [Fact(DisplayName = "port: useSyncedValueWithCleanup's cleanup after a replacement has synced leaves the replacement's value")]
    [Trait("Requirement", "UTL-STA-20")]
    public void UseSyncedValueWithCleanup_cleanup_after_a_replacement_has_synced_leaves_its_value()
    {
        ComponentStore<ShapeState> store = new(new ShapeState(new Point(0, 0), 0, null));
        SyncedValueWithCleanup<ShapeState, string> replaced = new(ShapeState.NodeField);
        SyncedValueWithCleanup<ShapeState, string> replacement = new(ShapeState.NodeField);

        replaced.Sync(store, "first-node");
        replacement.Sync(store, "second-node");
        replaced.Dispose();
        Assert.Equal("second-node", store.State.Node);

        replacement.Dispose();
        Assert.Null(store.State.Node);
    }

    [Fact(DisplayName = "port: useSyncedValueWithCleanup's cleanup after a replacement synced the same value leaves it")]
    [Trait("Requirement", "UTL-STA-20")]
    public void UseSyncedValueWithCleanup_cleanup_after_a_replacement_synced_the_same_value_leaves_it()
    {
        ComponentStore<ShapeState> store = new(new ShapeState(new Point(0, 0), 0, null));
        SyncedValueWithCleanup<ShapeState, string> replaced = new(ShapeState.NodeField);
        SyncedValueWithCleanup<ShapeState, string> replacement = new(ShapeState.NodeField);

        replaced.Sync(store, "node");
        replacement.Sync(store, "node");
        replaced.Dispose();
        Assert.Equal("node", store.State.Node);

        replacement.Dispose();
        Assert.Null(store.State.Node);
    }

    [Fact(DisplayName = "supports nested stores as state values")]
    [Trait("Requirement", "UTL-STA-12")]
    [Trait("Requirement", "UTL-STA-17")]
    [Trait("Requirement", "UTL-STA-25")]
    public void Supports_nested_stores_as_state_values()
    {
        ComponentStore<ParentState> parentStore = new(new ParentState(0));
        ComponentStore<ChildState> childStore = new(new ChildState(10, null));

        IDisposable? unsubscribeParentHandler = null;
        childStore.Observe(ChildState.ParentSelector, (newParent, _, store) =>
        {
            if (newParent is null)
            {
                unsubscribeParentHandler?.Dispose();
                return;
            }

            unsubscribeParentHandler = newParent.Subscribe(_ => store.NotifyAll());
        });
        childStore.Observe(ChildState.LocalCountSelector, (newCount, _, store) => store.State.Parent?.Set(ParentState.CountField, newCount));

        // The test component: one reactive read of `count`, rendered into an <output>.
        int renders = 1;
        using SelectorSubscription<ChildState> subscription = new(childStore, () => renders++);
        StoreSelection<ChildState, int> count = subscription.Select(ChildState.CountSelector);
        Assert.Equal(10, count.Value);

        childStore.Set(ChildState.CountField, 5);
        Assert.Equal(5, childStore.State.Count);
        Assert.Equal(5, count.Value);

        childStore.Set(ChildState.ParentField, parentStore);
        Assert.Equal(5, childStore.State.Count);
        Assert.Equal(0, childStore.Select(ChildState.CountSelector));
        Assert.Equal(0, count.Value);

        childStore.Set(ChildState.CountField, 20);
        Assert.Equal(20, childStore.State.Count);
        Assert.Equal(20, parentStore.State.Count);
        Assert.Equal(20, childStore.Select(ChildState.CountSelector));
        Assert.Equal(20, count.Value);

        parentStore.Set(ParentState.CountField, 15);
        Assert.Equal(15, parentStore.State.Count);
        Assert.Equal(20, childStore.State.Count);
        Assert.Equal(15, childStore.Select(ChildState.CountSelector));
        Assert.Equal(15, count.Value);

        Assert.Equal(5, renders);
    }

    [Fact(DisplayName = "updates useState result when selector arguments change in a fast component")]
    [Trait("Requirement", "UTL-STA-18")]
    [Trait("Requirement", "UTL-STA-27")]
    public void Updates_useState_result_when_selector_arguments_change_in_a_fast_component()
    {
        ComponentStore<ValuesState> store = new(new ValuesState(new Dictionary<string, string>
        {
            ["first"] = "one",
            ["second"] = "two",
            ["third"] = "three",
        }));
        using SelectorSubscription<ValuesState> subscription = new(store, () => { });
        StoreSelection<ValuesState, string, string?> value = subscription.Select(ValuesState.ValueByKey, "first");

        Assert.Equal("one", value.Value);

        value.Arg = "second";

        Assert.Equal("two", value.Value);
    }

    [Fact(DisplayName = "accepts selector functions")]
    [Trait("Requirement", "UTL-STA-24")]
    public void Accepts_selector_functions()
    {
        ComponentStore<CounterState> store = new(new CounterState(0, 1));
        List<(bool NewValue, bool OldValue)> calls = [];

        IDisposable unsubscribe = store.Observe(state => state.Count > 1, (newValue, oldValue, _) => calls.Add((newValue, oldValue)));

        Assert.Equal([(false, false)], calls);

        store.Set(CounterState.CountField, 2);
        Assert.Equal(2, calls.Count);
        Assert.Equal((true, false), calls[1]);

        store.Set(CounterState.CountField, 1);
        Assert.Equal(3, calls.Count);
        Assert.Equal((false, true), calls[2]);

        unsubscribe.Dispose();

        store.Set(CounterState.CountField, 3);
        Assert.Equal(3, calls.Count);
    }

    [Fact(DisplayName = "calls listener immediately with current selector result on subscription")]
    [Trait("Requirement", "UTL-STA-24")]
    public void Calls_listener_immediately_with_current_selector_result_on_subscription()
    {
        ComponentStore<CounterState> store = new(new CounterState(5, 3));
        List<(int NewValue, int OldValue)> calls = [];

        store.Observe(CounterState.Doubled, (newValue, oldValue, _) => calls.Add((newValue, oldValue)));

        Assert.Equal([(10, 10)], calls);
    }

    [Fact(DisplayName = "calls listener when selector result changes")]
    [Trait("Requirement", "UTL-STA-24")]
    public void Calls_listener_when_selector_result_changes()
    {
        ComponentStore<CounterState> store = new(new CounterState(5, 3));
        List<(int NewValue, int OldValue)> calls = [];

        store.Observe(CounterState.Doubled, (newValue, oldValue, _) => calls.Add((newValue, oldValue)));

        store.Set(CounterState.CountField, 10);
        store.Set(CounterState.CountField, 7);

        Assert.Equal(3, calls.Count);
        Assert.Equal((20, 10), calls[1]);
        Assert.Equal((14, 20), calls[2]);
    }

    [Fact(DisplayName = "does not call listener when selector result is unchanged")]
    [Trait("Requirement", "UTL-STA-24")]
    public void Does_not_call_listener_when_selector_result_is_unchanged()
    {
        ComponentStore<CounterState> store = new(new CounterState(5, 3));
        List<(int NewValue, int OldValue)> calls = [];

        store.Observe(CounterState.Doubled, (newValue, oldValue, _) => calls.Add((newValue, oldValue)));

        store.Set(CounterState.MultiplierField, 5);

        Assert.Single(calls); // Only initial call
    }

    [Fact(DisplayName = "calls listener when any dependency of the selector changes")]
    [Trait("Requirement", "UTL-STA-24")]
    public void Calls_listener_when_any_dependency_of_the_selector_changes()
    {
        ComponentStore<CounterState> store = new(new CounterState(5, 3));
        List<(int NewValue, int OldValue)> calls = [];

        store.Observe(CounterState.Multiplied, (newValue, oldValue, _) => calls.Add((newValue, oldValue)));

        store.Set(CounterState.CountField, 10);
        store.Set(CounterState.MultiplierField, 2);

        Assert.Equal([(15, 15), (30, 15), (20, 30)], calls);
    }

    [Fact(DisplayName = "provides the store instance to the listener")]
    [Trait("Requirement", "UTL-STA-24")]
    public void Provides_the_store_instance_to_the_listener()
    {
        ComponentStore<CounterState> store = new(new CounterState(5, 3));
        Store<CounterState>? receivedStore = null;

        store.Observe(CounterState.Doubled, (_, _, storeArg) => receivedStore = storeArg);

        Assert.Same(store, receivedStore);
    }

    [Fact(DisplayName = "returns an unsubscribe function that stops observing")]
    [Trait("Requirement", "UTL-STA-24")]
    public void Returns_an_unsubscribe_function_that_stops_observing()
    {
        ComponentStore<CounterState> store = new(new CounterState(5, 3));
        List<(int NewValue, int OldValue)> calls = [];

        IDisposable unsubscribe = store.Observe(CounterState.Doubled, (newValue, oldValue, _) => calls.Add((newValue, oldValue)));

        store.Set(CounterState.CountField, 10);
        Assert.Equal(2, calls.Count);

        unsubscribe.Dispose();

        store.Set(CounterState.CountField, 15);
        Assert.Equal(2, calls.Count); // No new calls after unsubscribe
    }

    [Fact(DisplayName = "supports multiple observers on the same selector")]
    [Trait("Requirement", "UTL-STA-24")]
    public void Supports_multiple_observers_on_the_same_selector()
    {
        ComponentStore<CounterState> store = new(new CounterState(5, 3));
        List<int> calls1 = [];
        List<int> calls2 = [];

        store.Observe(CounterState.Doubled, (newValue, _, _) => calls1.Add(newValue));
        store.Observe(CounterState.Doubled, (newValue, _, _) => calls2.Add(newValue));

        store.Set(CounterState.CountField, 10);

        Assert.Equal([10, 20], calls1);
        Assert.Equal([10, 20], calls2);
    }

    [Fact(DisplayName = "supports observers on different selectors")]
    [Trait("Requirement", "UTL-STA-24")]
    public void Supports_observers_on_different_selectors()
    {
        ComponentStore<CounterState> store = new(new CounterState(5, 3));
        List<int> doubledCalls = [];
        List<int> multipliedCalls = [];

        store.Observe(CounterState.Doubled, (newValue, _, _) => doubledCalls.Add(newValue));
        store.Observe(CounterState.Multiplied, (newValue, _, _) => multipliedCalls.Add(newValue));

        store.Set(CounterState.CountField, 10);
        store.Set(CounterState.MultiplierField, 2);

        Assert.Equal([10, 20], doubledCalls);
        Assert.Equal([15, 30, 20], multipliedCalls);
    }

    [Fact(DisplayName = "select() evaluates a named selector with an argument without subscribing")]
    [Trait("Requirement", "UTL-STA-17")]
    public void Select_evaluates_a_named_selector_with_an_argument_without_subscribing()
    {
        ComponentStore<ValuesState> store = new(new ValuesState(new Dictionary<string, string> { ["first"] = "one" }));

        Assert.Equal("one", store.Select(ValuesState.ValueByKey, "first"));
        Assert.Null(store.Select(ValuesState.ValueByKey, "missing"));
    }

    private static ParameterView Parameters(params (string Name, object? Value)[] parameters)
    {
        return ParameterView.FromDictionary(parameters.ToDictionary(parameter => parameter.Name, parameter => parameter.Value));
    }

    /// <summary>A store whose context bag holds a typed id and callback.</summary>
    private sealed class ContextStore(TestState state) : ComponentStore<TestState>(state)
    {
        public string? PopupId { get; set; }

        public Action? OnOpenChange { get; set; }
    }

    /// <summary><c>store.useControlledProp('value', controlled)</c>, with the store itself an optional parameter.</summary>
    private sealed class ControlledPropPart(ComponentStore<TestState> store)
    {
        private ComponentStore<TestState> store = store;

        public void SetParameters(ParameterView parameters)
        {
            if (parameters.TryGetValue("Store", out ComponentStore<TestState>? supplied) && supplied is not null)
            {
                store = supplied;
            }

            bool controlled = parameters.TryGetValue("Controlled", out int value);
            store.SyncControlledValue(TestState.ValueField, controlled, value);
        }
    }

    /// <summary><c>store.useSyncedValue('value', value)</c>.</summary>
    private sealed class SyncedValuePart(ComponentStore<TestState> store)
    {
        private ComponentStore<TestState> store = store;
        private int value;

        public void SetParameters(ParameterView parameters)
        {
            if (parameters.TryGetValue("Store", out ComponentStore<TestState>? supplied) && supplied is not null)
            {
                store = supplied;
            }

            if (parameters.TryGetValue("Value", out int suppliedValue))
            {
                value = suppliedValue;
            }

            store.SyncValue(TestState.ValueField, value);
        }
    }

    /// <summary><c>store.useSyncedValues(props)</c> over <c>value</c> and <c>label</c>.</summary>
    private sealed class SyncedValuesPart(ComponentStore<TestState> store)
    {
        private static readonly Func<TestState, (int Value, string Label), TestState> s_apply =
            (state, values) => state with { Value = values.Value, Label = values.Label };

        private int value;
        private string label = "";

        public void SetParameters(ParameterView parameters)
        {
            if (parameters.TryGetValue("Value", out int suppliedValue))
            {
                value = suppliedValue;
            }

            if (parameters.TryGetValue("Label", out string? suppliedLabel) && suppliedLabel is not null)
            {
                label = suppliedLabel;
            }

            store.SyncValues(s_apply, (value, label));
        }
    }
}

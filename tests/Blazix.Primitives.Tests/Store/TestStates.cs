using Blazix.Primitives.Store;

namespace Blazix.Primitives.Tests.Store;

/// <summary><c>type State = { value: number; label: string }</c> of <c>Store.test.ts</c> and <c>ReactStore.test.tsx</c>.</summary>
public sealed record TestState(int Value, string Label) : IStoreState<TestState>
{
    public static readonly StoreField<TestState, int> ValueField = new(nameof(Value), s => s.Value, (s, v) => s with { Value = v });
    public static readonly StoreField<TestState, string> LabelField = new(nameof(Label), s => s.Label, (s, v) => s with { Label = v });

    /// <inheritdoc />
    public static TestState Copy(TestState state) => state with { };

    /// <inheritdoc />
    public static bool ShallowEquals(TestState left, TestState right) =>
        Equality.Is(left.Value, right.Value) && Equality.Is(left.Label, right.Label);
}

/// <summary><c>type CounterState = { count: number; multiplier: number }</c> of the <c>observeSelector</c> group.</summary>
public sealed record CounterState(int Count, int Multiplier) : IStoreState<CounterState>
{
    public static readonly StoreField<CounterState, int> CountField = new(nameof(Count), s => s.Count, (s, v) => s with { Count = v });
    public static readonly StoreField<CounterState, int> MultiplierField = new(nameof(Multiplier), s => s.Multiplier, (s, v) => s with { Multiplier = v });

    // The named selectors of the group's `selectors` object.
    public static readonly Func<CounterState, int> CountSelector = s => s.Count;
    public static readonly Func<CounterState, int> Doubled = s => s.Count * 2;
    public static readonly Func<CounterState, int> Multiplied = s => s.Count * s.Multiplier;

    /// <inheritdoc />
    public static CounterState Copy(CounterState state) => state with { };

    /// <inheritdoc />
    public static bool ShallowEquals(CounterState left, CounterState right) =>
        Equality.Is(left.Count, right.Count) && Equality.Is(left.Multiplier, right.Multiplier);
}

/// <summary><c>type ParentState = { count: number }</c> of "supports nested stores as state values".</summary>
public sealed record ParentState(int Count) : IStoreState<ParentState>
{
    public static readonly StoreField<ParentState, int> CountField = new(nameof(Count), s => s.Count, (s, v) => s with { Count = v });
    public static readonly Func<ParentState, int> CountSelector = s => s.Count;

    /// <inheritdoc />
    public static ParentState Copy(ParentState state) => state with { };

    /// <inheritdoc />
    public static bool ShallowEquals(ParentState left, ParentState right) => Equality.Is(left.Count, right.Count);
}

/// <summary><c>type ChildState = { count: number; parent?: ParentStore }</c>: the parent store is a member, compared by reference.</summary>
public sealed record ChildState(int Count, ComponentStore<ParentState>? Parent) : IStoreState<ChildState>
{
    public static readonly StoreField<ChildState, int> CountField = new(nameof(Count), s => s.Count, (s, v) => s with { Count = v });
    public static readonly StoreField<ChildState, ComponentStore<ParentState>?> ParentField = new(nameof(Parent), s => s.Parent, (s, v) => s with { Parent = v });

    public static readonly Func<ChildState, int> CountSelector = s => s.Parent?.State.Count ?? s.Count;
    public static readonly Func<ChildState, ComponentStore<ParentState>?> ParentSelector = s => s.Parent;
    public static readonly Func<ChildState, int> LocalCountSelector = s => s.Count;

    /// <inheritdoc />
    public static ChildState Copy(ChildState state) => state with { };

    /// <inheritdoc />
    public static bool ShallowEquals(ChildState left, ChildState right) =>
        Equality.Is(left.Count, right.Count) && Equality.Is(left.Parent, right.Parent);
}

/// <summary><c>type State = { values: Record&lt;string, string&gt; }</c> of the selector-argument test.</summary>
public sealed record ValuesState(IReadOnlyDictionary<string, string> Values) : IStoreState<ValuesState>
{
    public static readonly Func<ValuesState, string, string?> ValueByKey = (s, key) => s.Values.TryGetValue(key, out string? value) ? value : null;

    /// <inheritdoc />
    public static ValuesState Copy(ValuesState state) => state with { };

    /// <inheritdoc />
    public static bool ShallowEquals(ValuesState left, ValuesState right) => Equality.Is(left.Values, right.Values);
}

/// <summary>A member holding a record: compared by reference by the store, never structurally (ADR 05 §1).</summary>
public sealed record Point(int X, int Y);

/// <summary>State with a record member, a double member and a nullable string member, for the equality cases.</summary>
public sealed record ShapeState(Point Origin, double Ratio, string? Node) : IStoreState<ShapeState>
{
    public static readonly StoreField<ShapeState, Point> OriginField = new(nameof(Origin), s => s.Origin, (s, v) => s with { Origin = v });
    public static readonly StoreField<ShapeState, double> RatioField = new(nameof(Ratio), s => s.Ratio, (s, v) => s with { Ratio = v });
    public static readonly StoreField<ShapeState, string?> NodeField = new(nameof(Node), s => s.Node, (s, v) => s with { Node = v });

    /// <inheritdoc />
    public static ShapeState Copy(ShapeState state) => state with { };

    /// <inheritdoc />
    public static bool ShallowEquals(ShapeState left, ShapeState right) =>
        Equality.Is(left.Origin, right.Origin) && Equality.Is(left.Ratio, right.Ratio) && Equality.Is(left.Node, right.Node);
}

/// <summary>
/// A member of every kind the generated comparison distinguishes: primitives, floating point, decimal, char, an enum and
/// their nullable forms, a struct with <c>==</c> and its nullable form, a tuple (IEquatable only) and its nullable form, a
/// string, a record, a store, <c>object</c> and an interface.
/// </summary>
[StoreState]
public sealed partial record EveryKindState(
    bool Flag,
    int Count,
    long Ticks,
    double Ratio,
    float Scale,
    Half Precision,
    decimal Amount,
    char Key,
    EventReason Reason,
    EventReason? LastReason,
    int? Index,
    double? Limit,
    Guid Id,
    Guid? OwnerId,
    (int Row, int Column) Cell,
    (int Row, int Column)? PreviousCell,
    string Label,
    Point Origin,
    ComponentStore<ParentState>? Parent,
    object Payload,
    IReadOnlyList<int> Items) : IStoreState<EveryKindState>;

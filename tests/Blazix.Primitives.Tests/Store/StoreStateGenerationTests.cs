using Blazix.Primitives.Store;

namespace Blazix.Primitives.Tests.Store;

/// <summary>
/// The generated <c>Copy</c> and <c>ShallowEquals</c> of a <c>[StoreState]</c> record (ADR 05 §1; UTL-STA-11/12) agree
/// with the hand-written members they replace, on every state record of the store tests: each case below gives the
/// verdict both must return. A record or store held as a member is equal only to itself, a string by value, a double by
/// <c>Object.is</c>.
/// </summary>
public sealed class StoreStateGenerationTests
{
    [Fact]
    public void TestState_generated_ShallowEquals_agrees_with_the_hand_written_one()
    {
        TestState state = new(1, "a");
        AssertAgree(
            [
                ("same record", state, state, true),
                ("copy", state, state with { }, true),
                ("equal string, another instance", state, state with { Label = new string('a', 1) }, true),
                ("Value differs", state, state with { Value = 2 }, false),
                ("Label differs", state, state with { Label = "b" }, false),
            ],
            TestState.ShallowEquals,
            static (left, right) => GeneratedTestState.ShallowEquals(new(left.Value, left.Label), new(right.Value, right.Label)));
    }

    [Fact]
    public void CounterState_generated_ShallowEquals_agrees_with_the_hand_written_one()
    {
        CounterState state = new(1, 2);
        AssertAgree(
            [
                ("same record", state, state, true),
                ("copy", state, state with { }, true),
                ("Count differs", state, state with { Count = 3 }, false),
                ("Multiplier differs", state, state with { Multiplier = 3 }, false),
            ],
            CounterState.ShallowEquals,
            static (left, right) => GeneratedCounterState.ShallowEquals(new(left.Count, left.Multiplier), new(right.Count, right.Multiplier)));
    }

    [Fact]
    public void ParentState_generated_ShallowEquals_agrees_with_the_hand_written_one()
    {
        ParentState state = new(1);
        AssertAgree(
            [
                ("same record", state, state, true),
                ("copy", state, state with { }, true),
                ("Count differs", state, state with { Count = 2 }, false),
            ],
            ParentState.ShallowEquals,
            static (left, right) => GeneratedParentState.ShallowEquals(new(left.Count), new(right.Count)));
    }

    [Fact]
    public void ChildState_generated_ShallowEquals_agrees_with_the_hand_written_one()
    {
        ComponentStore<ParentState> parent = new(new ParentState(1));
        ComponentStore<ParentState> sameValues = new(new ParentState(1));
        ChildState state = new(1, parent);
        AssertAgree(
            [
                ("same record", state, state, true),
                ("copy", state, state with { }, true),
                ("both without a parent", state with { Parent = null }, state with { Parent = null }, true),
                ("Count differs", state, state with { Count = 2 }, false),
                ("another store with the same state", state, state with { Parent = sameValues }, false),
                ("parent removed", state, state with { Parent = null }, false),
            ],
            ChildState.ShallowEquals,
            static (left, right) => GeneratedChildState.ShallowEquals(new(left.Count, left.Parent), new(right.Count, right.Parent)));
    }

    [Fact]
    public void ValuesState_generated_ShallowEquals_agrees_with_the_hand_written_one()
    {
        ValuesState state = new(new Dictionary<string, string> { ["a"] = "1" });
        AssertAgree(
            [
                ("same record", state, state, true),
                ("copy", state, state with { }, true),
                ("another dictionary with the same entries", state, new ValuesState(new Dictionary<string, string> { ["a"] = "1" }), false),
            ],
            ValuesState.ShallowEquals,
            static (left, right) => GeneratedValuesState.ShallowEquals(new(left.Values), new(right.Values)));
    }

    [Fact]
    public void ShapeState_generated_ShallowEquals_agrees_with_the_hand_written_one()
    {
        ShapeState state = new(new Point(0, 0), 0.5, "x");
        AssertAgree(
            [
                ("same record", state, state, true),
                ("copy", state, state with { }, true),
                ("NaN and NaN", state with { Ratio = double.NaN }, state with { Ratio = double.NaN }, true),
                ("equal string, another instance", state, state with { Node = new string('x', 1) }, true),
                ("both without a node", state with { Node = null }, state with { Node = null }, true),
                ("another record with the same values", state, state with { Origin = new Point(0, 0) }, false),
                ("0 and -0", state with { Ratio = 0.0 }, state with { Ratio = -0.0 }, false),
                ("node removed", state, state with { Node = null }, false),
                ("empty node and no node", state with { Node = string.Empty }, state with { Node = null }, false),
            ],
            ShapeState.ShallowEquals,
            static (left, right) => GeneratedShapeState.ShallowEquals(new(left.Origin, left.Ratio, left.Node), new(right.Origin, right.Ratio, right.Node)));
    }

    [Fact]
    public void Generated_Copy_returns_a_new_record_with_the_same_members()
    {
        AssertCopies(new GeneratedTestState(1, "a"));
        AssertCopies(new GeneratedCounterState(1, 2));
        AssertCopies(new GeneratedParentState(1));
        AssertCopies(new GeneratedChildState(1, new ComponentStore<ParentState>(new ParentState(1))));
        AssertCopies(new GeneratedValuesState(new Dictionary<string, string> { ["a"] = "1" }));
        AssertCopies(new GeneratedShapeState(new Point(0, 0), double.NaN, "x"));
    }

    private static void AssertAgree<TState>(
        (string Case, TState Left, TState Right, bool Equal)[] cases,
        Func<TState, TState, bool> handWritten,
        Func<TState, TState, bool> generated)
    {
        List<string> failures = [];
        foreach ((string name, TState left, TState right, bool equal) in cases)
        {
            foreach ((TState first, TState second, string order) in new[] { (left, right, string.Empty), (right, left, " (swapped)") })
            {
                bool expected = handWritten(first, second);
                bool actual = generated(first, second);
                if (expected != equal || actual != equal)
                {
                    failures.Add($"{name}{order}: expected {equal}, hand-written {expected}, generated {actual}");
                }
            }
        }

        Assert.Empty(failures);
    }

    private static void AssertCopies<TState>(TState state)
        where TState : class, IStoreState<TState>
    {
        TState copy = TState.Copy(state);
        Assert.NotSame(state, copy);
        Assert.True(TState.ShallowEquals(state, copy));
    }
}

// The same members as the hand-written records in TestStates.cs, with Copy and ShallowEquals generated.

[StoreState]
public sealed partial record GeneratedTestState(int Value, string Label) : IStoreState<GeneratedTestState>;

[StoreState]
public sealed partial record GeneratedCounterState(int Count, int Multiplier) : IStoreState<GeneratedCounterState>;

[StoreState]
public sealed partial record GeneratedParentState(int Count) : IStoreState<GeneratedParentState>;

[StoreState]
public sealed partial record GeneratedChildState(int Count, ComponentStore<ParentState>? Parent) : IStoreState<GeneratedChildState>;

[StoreState]
public sealed partial record GeneratedValuesState(IReadOnlyDictionary<string, string> Values) : IStoreState<GeneratedValuesState>;

[StoreState]
public sealed partial record GeneratedShapeState(Point Origin, double Ratio, string? Node) : IStoreState<GeneratedShapeState>;

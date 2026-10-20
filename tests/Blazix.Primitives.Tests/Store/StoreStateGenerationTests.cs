using Blazix.Primitives.Store;
using Microsoft.AspNetCore.Components;

namespace Blazix.Primitives.Tests.Store;

/// <summary>
/// The generated <c>Copy</c> and <c>ShallowEquals</c> of a <c>[StoreState]</c> record (ADR 05 §1; UTL-STA-11/12) agree
/// with the hand-written members they replaced, on every state record of the store tests: each case below gives the
/// verdict both must return, and each record's former hand-written comparison is kept here, verbatim, as the reference.
/// A record or store held as a member is equal only to itself, a string by value, a double by <c>Object.is</c>.
/// </summary>
public sealed class StoreStateGenerationTests
{
    [Fact]
    public void TestState_generated_ShallowEquals_agrees_with_the_hand_written_one_it_replaced()
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
            static (left, right) => Equality.Is(left.Value, right.Value) && Equality.Is(left.Label, right.Label),
            TestState.ShallowEquals);
    }

    [Fact]
    public void CounterState_generated_ShallowEquals_agrees_with_the_hand_written_one_it_replaced()
    {
        CounterState state = new(1, 2);
        AssertAgree(
            [
                ("same record", state, state, true),
                ("copy", state, state with { }, true),
                ("Count differs", state, state with { Count = 3 }, false),
                ("Multiplier differs", state, state with { Multiplier = 3 }, false),
            ],
            static (left, right) => Equality.Is(left.Count, right.Count) && Equality.Is(left.Multiplier, right.Multiplier),
            CounterState.ShallowEquals);
    }

    [Fact]
    public void ParentState_generated_ShallowEquals_agrees_with_the_hand_written_one_it_replaced()
    {
        ParentState state = new(1);
        AssertAgree(
            [
                ("same record", state, state, true),
                ("copy", state, state with { }, true),
                ("Count differs", state, state with { Count = 2 }, false),
            ],
            static (left, right) => Equality.Is(left.Count, right.Count),
            ParentState.ShallowEquals);
    }

    [Fact]
    public void ChildState_generated_ShallowEquals_agrees_with_the_hand_written_one_it_replaced()
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
            static (left, right) => Equality.Is(left.Count, right.Count) && Equality.Is(left.Parent, right.Parent),
            ChildState.ShallowEquals);
    }

    [Fact]
    public void ValuesState_generated_ShallowEquals_agrees_with_the_hand_written_one_it_replaced()
    {
        ValuesState state = new(new Dictionary<string, string> { ["a"] = "1" });
        AssertAgree(
            [
                ("same record", state, state, true),
                ("copy", state, state with { }, true),
                ("another dictionary with the same entries", state, new ValuesState(new Dictionary<string, string> { ["a"] = "1" }), false),
            ],
            static (left, right) => Equality.Is(left.Values, right.Values),
            ValuesState.ShallowEquals);
    }

    [Fact]
    public void ShapeState_generated_ShallowEquals_agrees_with_the_hand_written_one_it_replaced()
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
            static (left, right) => Equality.Is(left.Origin, right.Origin) && Equality.Is(left.Ratio, right.Ratio) && Equality.Is(left.Node, right.Node),
            ShapeState.ShallowEquals);
    }

    [Fact]
    public void ElementState_generated_ShallowEquals_agrees_with_Equality_Is()
    {
        // No record held an element reference before the generator, so the reference is the generic Equality.Is a
        // hand-written comparison would have called: the id by value and the context by reference.
        TestElementContext context = new();
        ElementState state = new(new ElementReference("a", context), new ElementReference("b", context));
        AssertAgree(
            [
                ("same record", state, state, true),
                ("copy", state, state with { }, true),
                ("equal id, another string instance", state, state with { Element = new ElementReference(new string('a', 1), context) }, true),
                ("both without an anchor", state with { Anchor = null }, state with { Anchor = null }, true),
                ("another element", state, state with { Element = new ElementReference("c", context) }, false),
                ("same id, another renderer's context", state, state with { Element = new ElementReference("a", new TestElementContext()) }, false),
                ("same id, no context", state, state with { Element = new ElementReference("a") }, false),
                ("anchor removed", state, state with { Anchor = null }, false),
                ("another anchor", state, state with { Anchor = new ElementReference("d", context) }, false),
            ],
            static (left, right) => Equality.Is(left.Element, right.Element) && Equality.Is(left.Anchor, right.Anchor),
            ElementState.ShallowEquals);
    }

    [Fact]
    public void Generated_Copy_returns_a_new_record_with_the_same_members()
    {
        AssertCopies(new TestState(1, "a"));
        AssertCopies(new CounterState(1, 2));
        AssertCopies(new ParentState(1));
        AssertCopies(new ChildState(1, new ComponentStore<ParentState>(new ParentState(1))));
        AssertCopies(new ValuesState(new Dictionary<string, string> { ["a"] = "1" }));
        AssertCopies(new ShapeState(new Point(0, 0), double.NaN, "x"));
        AssertCopies(new ElementState(new ElementReference("a", new TestElementContext()), null));
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

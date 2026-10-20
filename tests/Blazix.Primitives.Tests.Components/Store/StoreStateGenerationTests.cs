using Blazix.Primitives.Store;

namespace Blazix.Primitives.Tests.Components.Store;

/// <summary>
/// The generated <c>ShallowEquals</c> of a <c>[StoreState]</c> record (ADR 05 §1; UTL-STA-11) agrees with the hand-written
/// one it replaces on this project's store record, <see cref="CounterStoreState"/>; the L1 store tests do the same for
/// theirs. No renderer is involved.
/// </summary>
public sealed class StoreStateGenerationTests
{
    [Fact]
    public void CounterStoreState_generated_ShallowEquals_agrees_with_the_hand_written_one()
    {
        CounterStoreState state = new(1, 2);
        (string Case, CounterStoreState Left, CounterStoreState Right, bool Equal)[] cases =
        [
            ("same record", state, state, true),
            ("copy", state, state with { }, true),
            ("Count differs", state, state with { Count = 3 }, false),
            ("Multiplier differs", state, state with { Multiplier = 3 }, false),
        ];

        List<string> failures = [];
        foreach ((string name, CounterStoreState left, CounterStoreState right, bool equal) in cases)
        {
            bool handWritten = CounterStoreState.ShallowEquals(left, right);
            bool generated = GeneratedCounterStoreState.ShallowEquals(new(left.Count, left.Multiplier), new(right.Count, right.Multiplier));
            if (handWritten != equal || generated != equal)
            {
                failures.Add($"{name}: expected {equal}, hand-written {handWritten}, generated {generated}");
            }
        }

        Assert.Empty(failures);
    }
}

/// <summary>The members of <see cref="CounterStoreState"/>, with <c>Copy</c> and <c>ShallowEquals</c> generated.</summary>
[StoreState]
public sealed partial record GeneratedCounterStoreState(int Count, int Multiplier) : IStoreState<GeneratedCounterStoreState>;

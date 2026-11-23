using Blazix.Primitives.Store;

namespace Blazix.Primitives.Tests.Components.Store;

/// <summary>
/// The generated <c>ShallowEquals</c> of a <c>[StoreState]</c> record (ADR 05 §1; UTL-STA-11) agrees with the hand-written
/// one it replaced on this project's store record, <see cref="CounterStoreState"/>, kept here verbatim as the reference;
/// the L1 store tests do the same for theirs. No renderer is involved.
/// </summary>
public sealed class StoreStateGenerationTests
{
    [Fact]
    public void CounterStoreState_generated_ShallowEquals_agrees_with_the_hand_written_one_it_replaced()
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
            bool handWritten = Equality.Is(left.Count, right.Count) && Equality.Is(left.Multiplier, right.Multiplier);
            bool generated = CounterStoreState.ShallowEquals(left, right);
            if (handWritten != equal || generated != equal)
            {
                failures.Add($"{name}: expected {equal}, hand-written {handWritten}, generated {generated}");
            }
        }

        Assert.Empty(failures);
    }
}

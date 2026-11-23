namespace Blazix.Primitives.Fixtures.Client.Harness;

/// <summary>Numbers the interactive roots of one session in the order they first render (issue 3, ADR-02/1).</summary>
public sealed class ActivationCounter
{
    private int next;

    public int Next() => Interlocked.Increment(ref next);
}

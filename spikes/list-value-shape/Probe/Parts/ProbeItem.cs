namespace Probe.Parts;

// The test-only leaf of a typed `Items` list (ADR 08 Amendments 2026-11-03 (2): typed lists with `string` labels).
public sealed record ProbeItem<TValue>(TValue Value, string Label);

public enum Fruit
{
    Apple,
    Banana,
}

public sealed record Person(string Name);

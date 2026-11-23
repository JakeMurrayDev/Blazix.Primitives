namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The collection of every allocation-budget class of this project (ADR 16 rule 7), run with parallelization disabled:
/// after the parallel collections, one class at a time, so no other collection's work lands inside a measured window
/// (issue 112, settled by the orchestrator 2026-10-27).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AllocationBudgetsCollection
{
    /// <summary>The collection's name, which each budget class names in its <c>[Collection]</c> attribute.</summary>
    public const string Name = "AllocationBudgets";
}

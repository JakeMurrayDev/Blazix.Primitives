namespace Blazix.Primitives.Tests.Components;

/// <summary>
/// The collection of the classes that turn the development warnings switch off (as a production app has it), run with
/// parallelization disabled: after the parallel collections, one class at a time, since the switch is process-wide and every
/// other test runs with it on (Button/DevelopmentWarningsSwitch).
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DevelopmentWarningsOffCollection
{
    /// <summary>The collection's name, which each class names in its <c>[Collection]</c> attribute.</summary>
    public const string Name = "DevelopmentWarningsOff";
}

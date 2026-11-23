namespace Blazix.Primitives.Fixtures.Client;

/// <summary>One row of the <see cref="FixtureCatalog"/>: the component, whether it prerenders, how many roots the page renders.</summary>
public sealed record FixtureEntry(Type Component, bool Prerender = false, int Roots = 1);

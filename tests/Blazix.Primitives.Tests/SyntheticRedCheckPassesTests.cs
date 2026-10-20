using Blazix.Primitives.Tests.TestSupport;

namespace Blazix.Primitives.Tests;

// Synthetic pull request for the red check (ADR 11 section 3): this test already passes on the base branch's src/, so
// the red-check job must fail. Not for merge.
public sealed class SyntheticRedCheckPassesTests
{
    [Fact(DisplayName = "synthetic: passes against the base branch")]
    public void Passes_against_the_base_branch()
    {
        string partBase = Path.Combine(RepositoryRoot.Find(), "src", "Blazix.Primitives", "Rendering", "PartBase.cs");

        Assert.True(File.Exists(partBase));
    }
}

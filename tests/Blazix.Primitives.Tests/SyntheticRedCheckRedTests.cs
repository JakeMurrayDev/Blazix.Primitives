using Blazix.Primitives.Tests.TestSupport;

namespace Blazix.Primitives.Tests;

// Synthetic pull request for the red check (ADR 11 section 3): the pull request adds the marker file under src/ and this
// test looks for it, so it passes with the change and fails against the base branch's src/; the red-check job must pass.
// Not for merge.
public sealed class SyntheticRedCheckRedTests
{
    [Fact(DisplayName = "synthetic: fails against the base branch")]
    public void Fails_against_the_base_branch()
    {
        string marker = Path.Combine(RepositoryRoot.Find(), "src", "Blazix.Primitives", "Rendering", "SyntheticMarker.cs");

        Assert.True(File.Exists(marker));
    }
}

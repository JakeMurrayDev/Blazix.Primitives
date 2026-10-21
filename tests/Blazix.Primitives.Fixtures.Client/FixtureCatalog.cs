using Blazix.Primitives.Fixtures.Client.Allocation;
using Blazix.Primitives.Fixtures.Client.ButtonBehavior;
using Blazix.Primitives.Fixtures.Client.KeepAlive;
using Blazix.Primitives.Fixtures.Client.Prerender;
using Blazix.Primitives.Fixtures.Client.Probe;
using Blazix.Primitives.Fixtures.Client.Separator;

namespace Blazix.Primitives.Fixtures.Client;

/// <summary>
/// Every fixture the host serves under <c>/{mode}/{fixture}</c> (ADR 09 §1). Prerendering is off except where a row
/// says otherwise (ADR 06 §2); a fixture with several roots renders them in document order with <c>RootIndex</c>.
/// </summary>
public static class FixtureCatalog
{
    public static IReadOnlyDictionary<string, FixtureEntry> Entries { get; } = new Dictionary<string, FixtureEntry>(StringComparer.Ordinal)
    {
        ["probe"] = new(typeof(ProbeFixture)),
        ["prerender"] = new(typeof(PrerenderFixture), Prerender: true, Roots: 2),
        ["allocation"] = new(typeof(AllocationFixture)),
        ["keep-alive"] = new(typeof(KeepAliveFixture)),
        ["button-behavior"] = new(typeof(ButtonBehaviorFixture)),
        ["separator-showcase"] = new(typeof(SeparatorFixture)),
    };
}

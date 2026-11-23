using System.Globalization;
using Blazix.Primitives.Utilities;

namespace Blazix.Primitives.Tests.ListValues;

// ADR 16 rule 7 (the least of five runs): the collator filter's three predicates over 1,000 item texts per query allocate
// nothing on CoreCLR once warm (UTL-I18N-09..11; the windows are spans). The WebAssembly interpreter is measured by the
// `wasm`-only L4 probe.
/// <summary>The allocation probe of the collator filter on CoreCLR.</summary>
[Collection(AllocationBudgetsCollection.Name)]
public sealed class CollatorFilterAllocationProbeTests
{
    private const int Calls = 20;

    [Fact(DisplayName = "port: the collator filter allocates nothing over 1,000 items per query")]
    [Trait("Requirement", "ADR16-7")]
    [Trait("Requirement", "UTL-I18N-09")]
    public void The_collator_filter_allocates_nothing_over_1000_items_per_query()
    {
        CollatorFilter filter = CollatorFilter.Create(new CultureInfo("fr-FR"));
        string[] texts = [.. Enumerable.Range(0, 1000).Select(i => $"Résumé numéro {i}")];
        Exercise(filter, texts);

        long least = long.MaxValue;
        for (int run = 0; run < 5; run++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < Calls; i++)
            {
                Exercise(filter, texts);
            }

            least = Math.Min(least, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        Assert.True(least == 0, $"{least} bytes over {Calls} queries");
    }

    private static void Exercise(CollatorFilter filter, string[] texts)
    {
        int matches = 0;
        foreach (string text in texts)
        {
            if (filter.Contains(text, "numero 99") || filter.StartsWith(text, "RESUME") || filter.EndsWith(text, "zz"))
            {
                matches++;
            }
        }

        Assert.True(matches == texts.Length);
    }
}

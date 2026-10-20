using Microsoft.CodeAnalysis;

namespace Blazix.Primitives.Generators;

/// <summary>
/// Emits, for every <c>sealed partial record</c> marked <c>[PartState]</c>, the typed projection of its own public
/// primitive properties to <c>data-*</c> attributes in declaration order (RND-ALG-07; ADR 02 decision 4 as amended by
/// ADR 05: no reflection at render time), and the consumer-facing diagnostics BLZX1001–BLZX1004.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class PartStateGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
    }
}

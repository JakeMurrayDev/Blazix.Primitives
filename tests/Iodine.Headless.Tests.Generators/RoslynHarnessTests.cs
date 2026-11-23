using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Blazix.Primitives.Tests.Generators;

/// <summary>Proves the generator-test runner works: the Roslyn testing SDK drives a generator and sees no diagnostics.</summary>
public sealed class RoslynHarnessTests
{
    [Fact]
    public async Task An_empty_generator_reports_no_diagnostics()
    {
        CSharpSourceGeneratorTest<EmptyGenerator, DefaultVerifier> test = new()
        {
            TestCode = "internal sealed class Probe { }",
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        await test.RunAsync(TestContext.Current.CancellationToken);
    }

    private sealed class EmptyGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
        }
    }
}

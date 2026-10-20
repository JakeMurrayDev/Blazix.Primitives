using Blazix.Primitives.Analyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Blazix.Primitives.Tests.Generators.Analyzers;

/// <summary>
/// The sequence-number analyzer (ADR 16 rule 3): a sequence number passed to a <c>RenderTreeBuilder</c> that is not a
/// constant expression is a build error. The compilation references <c>Microsoft.AspNetCore.Components</c> 10.0 on top of
/// the .NET 10 reference assemblies, the newest the testing SDK resolves.
/// </summary>
public sealed class SequenceNumberAnalyzerTests
{
    private static readonly ReferenceAssemblies s_withComponents = ReferenceAssemblies.Net.Net100.AddPackages(
        [new PackageIdentity("Microsoft.AspNetCore.Components", "10.0.0")]);

    [Fact]
    [Trait("Requirement", "ADR16-3")]
    public async Task Literals_named_constants_and_constant_expressions_are_accepted()
    {
        const string source = """
            using Microsoft.AspNetCore.Components;
            using Microsoft.AspNetCore.Components.Rendering;

            internal sealed class Root : ComponentBase
            {
                private const int Host = 0;
                private const int Attributes = Host + 1;

                protected override void BuildRenderTree(RenderTreeBuilder builder)
                {
                    builder.OpenElement(Host, "div");
                    builder.AddAttribute(Attributes, "class", "a");
                    builder.AddAttribute(Attributes, "title", "b");
                    builder.AddElementReferenceCapture(2, _ => { });
                    builder.OpenRegion(100);
                    for (int i = 0; i < 3; i++)
                    {
                        builder.AddContent(0, "x");
                    }

                    builder.CloseRegion();
                    builder.AddNamedEvent("onsubmit", "form");
                    builder.CloseElement();
                }
            }
            """;

        await Verify(source);
    }

    [Fact]
    [Trait("Requirement", "ADR16-3")]
    public async Task A_method_that_is_not_the_builders_may_take_any_sequence()
    {
        const string source = """
            using Microsoft.AspNetCore.Components.Rendering;

            internal static class Emitter
            {
                public static void Emit(RenderTreeBuilder builder, int sequence)
                {
                    Helper(sequence);
                    Helper(sequence + 1);
                    builder.CloseElement();
                }

                private static void Helper(int sequence)
                {
                }
            }
            """;

        await Verify(source);
    }

    [Fact]
    [Trait("Requirement", "ADR16-3")]
    public async Task A_counter_is_an_error()
    {
        const string source = """
            using Microsoft.AspNetCore.Components;
            using Microsoft.AspNetCore.Components.Rendering;

            internal sealed class Root : ComponentBase
            {
                protected override void BuildRenderTree(RenderTreeBuilder builder)
                {
                    int seq = 0;
                    builder.OpenElement({|#0:seq++|}, "div");
                    builder.AddAttribute({|#1:seq++|}, "class", "a");
                    builder.AddContent({|#2:seq|}, "text");
                    builder.CloseElement();
                }
            }
            """;

        await Verify(
            source,
            Error(0, "seq++"),
            Error(1, "seq++"),
            Error(2, "seq"));
    }

    [Fact]
    [Trait("Requirement", "ADR16-3")]
    public async Task A_field_a_parameter_a_calculation_and_a_frames_number_are_errors()
    {
        const string source = """
            using Microsoft.AspNetCore.Components;
            using Microsoft.AspNetCore.Components.Rendering;
            using Microsoft.AspNetCore.Components.RenderTree;

            internal sealed class Root : ComponentBase
            {
                private int next;

                protected override void BuildRenderTree(RenderTreeBuilder builder)
                {
                    builder.OpenRegion({|#0:next|});
                    builder.CloseRegion();
                    Emit(builder, 3, default);
                }

                private static void Emit(RenderTreeBuilder builder, int count, RenderTreeFrame frame)
                {
                    builder.OpenElement({|#1:count|}, "p");
                    builder.AddAttribute({|#2:1 + count|}, "class", "a");
                    builder.AddAttribute({|#3:frame.Sequence|}, frame);
                    builder.CloseElement();
                }
            }
            """;

        await Verify(
            source,
            Error(0, "next"),
            Error(1, "count"),
            Error(2, "1 + count"),
            Error(3, "frame.Sequence"));
    }

    [Fact]
    [Trait("Requirement", "ADR16-3")]
    public async Task A_pragma_suppresses_the_rule_where_the_number_is_the_consumers_own()
    {
        const string source = """
            using Microsoft.AspNetCore.Components.Rendering;
            using Microsoft.AspNetCore.Components.RenderTree;

            internal static class Reemitter
            {
            #pragma warning disable BLZX2002
                public static void Reemit(RenderTreeBuilder builder, RenderTreeFrame frame)
                {
                    builder.AddAttribute(frame.Sequence, frame);
                }
            #pragma warning restore BLZX2002

                public static void Other(RenderTreeBuilder builder, RenderTreeFrame frame)
                {
                    builder.AddAttribute({|#0:frame.Sequence|}, frame);
                }
            }
            """;

        await Verify(source, Error(0, "frame.Sequence"));
    }

    private static DiagnosticResult Error(int location, string argument)
    {
        return new DiagnosticResult(SequenceNumberAnalyzer.DiagnosticId, Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .WithLocation(location)
            .WithArguments(argument);
    }

    private static async Task Verify(string source, params DiagnosticResult[] expected)
    {
        CSharpAnalyzerTest<SequenceNumberAnalyzer, DefaultVerifier> test = new()
        {
            TestCode = source,
            ReferenceAssemblies = s_withComponents,
        };
        test.ExpectedDiagnostics.AddRange(expected);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}

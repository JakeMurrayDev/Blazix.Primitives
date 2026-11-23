using Blazix.Primitives.Analyzers;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Blazix.Primitives.Tests.Generators.Analyzers;

/// <summary>
/// The nullable-union-parameter analyzer (ADR 05 and ADR 08, Amendments 2026-10-20): a <c>[Parameter]</c> or cascading
/// parameter typed by a declared <c>union</c> or a <c>[Union]</c> struct is declared nullable. The union attribute and
/// interface, which .NET 10 lacks, are declared in the source.
/// </summary>
public sealed class NullableUnionParameterAnalyzerTests
{
    private const string Shim = """

        namespace System.Runtime.CompilerServices
        {
            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
            public sealed class UnionAttribute : Attribute { }

            public interface IUnion
            {
                object? Value { get; }
            }
        }

        [Union]
        public readonly struct ModalValue : IUnion
        {
            private readonly bool modal;

            public ModalValue(bool value) { modal = value; }

            public object? Value => modal;
        }
        """;

    private static readonly ReferenceAssemblies s_withComponents = ReferenceAssemblies.Net.Net100.AddPackages(
        [new PackageIdentity("Microsoft.AspNetCore.Components", "10.0.0")]);

    [Fact]
    [Trait("Requirement", "ADR05-4")]
    public async Task Nullable_union_parameters_and_non_parameter_union_properties_are_accepted()
    {
        const string source = """
            using System;
            using System.Runtime.CompilerServices;
            using Microsoft.AspNetCore.Components;

            public sealed class Dialog : ComponentBase
            {
                [Parameter]
                public ModalValue? Modal { get; set; }

                [CascadingParameter]
                public ModalValue? InheritedModal { get; set; }

                [Parameter]
                public bool Open { get; set; }

                public ModalValue Resolved => Modal ?? new ModalValue(true);

                internal ModalValue Current { get; set; }
            }
            """ + Shim;

        await Verify(source);
    }

    [Theory]
    [Trait("Requirement", "ADR05-4")]
    [InlineData("[Parameter]")]
    [InlineData("[Parameter(CaptureUnmatchedValues = false)]")]
    [InlineData("[CascadingParameter]")]
    [InlineData("[CascadingParameter(Name = \"Modal\")]")]
    [InlineData("[SupplyParameterFromQuery]")]
    public async Task A_non_nullable_union_parameter_is_an_error(string attribute)
    {
        string source = $$"""
            using System;
            using System.Runtime.CompilerServices;
            using Microsoft.AspNetCore.Components;

            public sealed class Dialog : ComponentBase
            {
                {{attribute}}
                public ModalValue {|#0:Modal|} { get; set; }
            }
            """ + Shim;

        await Verify(source, Error(0, "Modal", "ModalValue"));
    }

    [Fact]
    [Trait("Requirement", "ADR05-4")]
    public async Task A_non_nullable_declared_union_parameter_is_an_error()
    {
        const string source = """
            using System;
            using Microsoft.AspNetCore.Components;

            public union AnchorValue(string, Uri);

            public sealed class Positioner : ComponentBase
            {
                [Parameter]
                public AnchorValue {|#0:Anchor|} { get; set; }

                [Parameter]
                public AnchorValue? Fallback { get; set; }
            }

            namespace System.Runtime.CompilerServices
            {
                [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
                public sealed class UnionAttribute : Attribute { }

                public interface IUnion
                {
                    object? Value { get; }
                }
            }
            """;

        await Verify(source, LanguageVersion.Preview, skipSuppressionCheck: false, Error(0, "Anchor", "AnchorValue"));
    }

    [Fact]
    [Trait("Requirement", "ADR05-4")]
    public async Task A_pragma_suppresses_the_rule()
    {
        const string source = """
            using System;
            using System.Runtime.CompilerServices;
            using Microsoft.AspNetCore.Components;

            public sealed class Dialog : ComponentBase
            {
            #pragma warning disable BLZX2006 // probing the default value on purpose
                [Parameter]
                public ModalValue Probe { get; set; }
            #pragma warning restore BLZX2006

                [Parameter]
                public ModalValue {|#0:Modal|} { get; set; }
            }
            """ + Shim;

        // The testing SDK wraps the source in its own pragma to check suppression, which the source's restore would undo.
        await Verify(source, languageVersion: null, skipSuppressionCheck: true, Error(0, "Modal", "ModalValue"));
    }

    private static DiagnosticResult Error(int location, string parameter, string union)
    {
        return new DiagnosticResult(NullableUnionParameterAnalyzer.DiagnosticId, Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .WithLocation(location)
            .WithArguments(parameter, union);
    }

    private static Task Verify(string source, params DiagnosticResult[] expected) => Verify(source, languageVersion: null, skipSuppressionCheck: false, expected);

    private static async Task Verify(string source, LanguageVersion? languageVersion, bool skipSuppressionCheck, params DiagnosticResult[] expected)
    {
        CSharpAnalyzerTest<NullableUnionParameterAnalyzer, DefaultVerifier> test = new()
        {
            TestCode = source,
            ReferenceAssemblies = s_withComponents,
        };
        if (languageVersion is { } version)
        {
            test.SolutionTransforms.Add((solution, projectId) =>
            {
                CSharpParseOptions options = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
                return solution.WithProjectParseOptions(projectId, options.WithLanguageVersion(version));
            });
        }

        if (skipSuppressionCheck)
        {
            test.TestBehaviors |= TestBehaviors.SkipSuppressionCheck;
        }

        test.ExpectedDiagnostics.AddRange(expected);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}

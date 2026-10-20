using Blazix.Primitives.Analyzers;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Blazix.Primitives.Tests.Generators.Analyzers;

/// <summary>
/// The field-prefix analyzer (ADR 08 §5 as amended 2026-10-20): <c>_x</c> and <c>m_x</c> are rejected on every field. The
/// repository's IDE1006 rules already reject them on private fields and constants (probed in M0 track 5), so the analyzer
/// covers the non-private, non-constant fields those rules do not reach, and only those.
/// </summary>
public sealed class FieldPrefixAnalyzerTests
{
    [Fact]
    [Trait("Requirement", "ADR08-5")]
    public async Task Fields_without_a_prefix_and_private_fields_left_to_IDE1006_are_accepted()
    {
        const string source = """
            public class Part
            {
                public const int Limit = 1;
                internal static readonly int s_shared = 1;
                public int count;
                internal int depth;
                protected int margin;
                public static int Total;

                // Private fields and constants belong to IDE1006, which the build runs from .editorconfig.
                private int _hidden;
                private static int m_cache;
                private const int _max = 2;
                public const int _Min = 0;
            }
            """;

        await Verify(source);
    }

    [Theory]
    [Trait("Requirement", "ADR08-5")]
    [InlineData("public int", "_count", "_")]
    [InlineData("internal int", "_count", "_")]
    [InlineData("protected int", "_count", "_")]
    [InlineData("protected internal int", "_count", "_")]
    [InlineData("private protected int", "_count", "_")]
    [InlineData("internal static int", "_count", "_")]
    [InlineData("public static readonly int", "_count", "_")]
    [InlineData("public int", "m_count", "m_")]
    [InlineData("internal readonly int", "m_count", "m_")]
    [InlineData("protected static int", "m_count", "m_")]
    public async Task A_non_private_field_with_a_prefix_is_an_error(string modifiers, string name, string prefix)
    {
        string source = $$"""
            public class Part
            {
                {{modifiers}} {|#0:{{name}}|};
            }
            """;

        await Verify(source, Error(0, name, prefix));
    }

    [Fact]
    [Trait("Requirement", "ADR08-5")]
    public async Task Each_variable_of_a_declaration_and_a_field_of_a_nested_type_or_struct_is_checked()
    {
        const string source = """
            public class Part
            {
                internal int {|#0:_left|}, right, {|#1:m_top|};

                private sealed class Entry
                {
                    public int {|#2:_value|};
                }
            }

            public struct Pair
            {
                public int {|#3:_first|};
            }
            """;

        await Verify(
            source,
            Error(0, "_left", "_"),
            Error(1, "m_top", "m_"),
            Error(2, "_value", "_"),
            Error(3, "_first", "_"));
    }

    [Fact]
    [Trait("Requirement", "ADR08-5")]
    public async Task A_pragma_suppresses_the_rule()
    {
        const string source = """
            public class Part
            {
            #pragma warning disable BLZX2004 // a name fixed by an interop contract
                public int _legacy;
            #pragma warning restore BLZX2004

                public int {|#0:_other|};
            }
            """;

        // The testing SDK wraps the source in its own pragma to check suppression, which the source's restore would undo.
        await Verify(source, skipSuppressionCheck: true, Error(0, "_other", "_"));
    }

    private static DiagnosticResult Error(int location, string field, string prefix)
    {
        return new DiagnosticResult(FieldPrefixAnalyzer.DiagnosticId, Microsoft.CodeAnalysis.DiagnosticSeverity.Error)
            .WithLocation(location)
            .WithArguments(field, prefix);
    }

    private static Task Verify(string source, params DiagnosticResult[] expected) => Verify(source, skipSuppressionCheck: false, expected);

    private static async Task Verify(string source, bool skipSuppressionCheck, params DiagnosticResult[] expected)
    {
        CSharpAnalyzerTest<FieldPrefixAnalyzer, DefaultVerifier> test = new()
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };
        if (skipSuppressionCheck)
        {
            test.TestBehaviors |= TestBehaviors.SkipSuppressionCheck;
        }

        test.ExpectedDiagnostics.AddRange(expected);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}

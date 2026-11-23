using Blazix.Primitives.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Blazix.Primitives.Tests.Generators.Analyzers;

/// <summary>
/// The inheritdoc analyzer (ADR 08 §5, Amendments 2026-10-20, with the issue-42 clarification that allows
/// <c>&lt;exception&gt;</c>): a member that overrides a base member or implements an interface member, implicitly or
/// explicitly, documents itself with <c>&lt;inheritdoc /&gt;</c>, optionally adding <c>&lt;remarks&gt;</c> and
/// <c>&lt;exception&gt;</c>, and nothing else.
/// </summary>
public sealed class InheritDocAnalyzerTests
{
    private const string Contracts = """

        public abstract class Base
        {
            /// <summary>Renders.</summary>
            public virtual void Render() { }

            /// <summary>Gets the size.</summary>
            /// <value>The size.</value>
            public virtual int Size => 0;
        }

        /// <summary>Hosts a part.</summary>
        public interface IHost
        {
            /// <summary>Attaches the host.</summary>
            /// <param name="id">The id.</param>
            /// <returns>The handle.</returns>
            int Attach(string id);

            /// <summary>Gets the name.</summary>
            string Name { get; }

            /// <summary>Occurs when the host changes.</summary>
            event System.Action Changed;
        }
        """;

    [Fact]
    [Trait("Requirement", "ADR08-5")]
    public async Task Inheritdoc_alone_or_with_remarks_and_exceptions_is_accepted()
    {
        const string source = """
            using System;

            public sealed class Part : Base, IHost, IDisposable
            {
                /// <inheritdoc />
                public override void Render() { }

                /// <inheritdoc />
                /// <remarks>Measures the host first.</remarks>
                public override int Size => 1;

                /// <inheritdoc />
                /// <exception cref="ArgumentException"><paramref name="id"/> is empty.</exception>
                /// <remarks>Idempotent per id.</remarks>
                public int Attach(string id) => 0;

                /// <inheritdoc/>
                public string Name => "part";

                /// <inheritdoc />
                public event Action? Changed;

                /// <inheritdoc />
                void IDisposable.Dispose() { }

                /// <inheritdoc />
                public override string ToString() => Name;

                /// <summary>Focuses the part; it overrides and implements nothing, so it documents itself.</summary>
                public void Focus() { }

                private void Update() { }
            }
            """ + Contracts;

        await Verify(source);
    }

    [Theory]
    [Trait("Requirement", "ADR08-5")]
    [InlineData("/// <summary>Renders the part.</summary>\n    public override void {|#0:Render|}() { }", "Render", "remove `<summary>`")]
    [InlineData("public override void {|#0:Render|}() { }", "Render", "add `/// <inheritdoc />`")]
    [InlineData("// Renders the part.\n    public override void {|#0:Render|}() { }", "Render", "add `/// <inheritdoc />`")]
    [InlineData("/// <remarks>Renders twice.</remarks>\n    public override void {|#0:Render|}() { }", "Render", "add `<inheritdoc />`")]
    [InlineData("/// Renders the part.\n    public override void {|#0:Render|}() { }", "Render", "move its text into `<remarks>` or remove it")]
    [InlineData("/// <inheritdoc />\n    /// <value>One.</value>\n    public override int {|#0:Size|} => 1;", "Size", "remove `<value>`")]
    [InlineData("/// <inheritdoc />\n    /// <example>part.Render()</example>\n    public override void {|#0:Render|}() { }", "Render", "remove `<example>`")]
    public async Task An_override_that_restates_or_lacks_its_documentation_is_an_error(string member, string name, string problem)
    {
        string source = $$"""
            public sealed class Part : Base
            {
                {{member}}
            }

            public sealed class Other : Base
            {
                /// <inheritdoc />
                public override void Render() { }

                /// <inheritdoc />
                public override int Size => 2;
            }
            """ + Contracts;

        await Verify(source, Error(0, name, problem));
    }

    [Fact]
    [Trait("Requirement", "ADR08-5")]
    public async Task Implicit_and_explicit_implementations_that_restate_their_documentation_are_errors()
    {
        const string source = """
            using System;

            public sealed class Part : IHost, IDisposable, IEquatable<Part>
            {
                /// <summary>Attaches the part.</summary>
                /// <param name="id">The id.</param>
                /// <returns>The handle.</returns>
                public int {|#0:Attach|}(string id) => 0;

                /// <summary>Gets the part's name.</summary>
                public string {|#1:Name|} => "part";

                /// <summary>Occurs when the part changes.</summary>
                public event Action? {|#2:Changed|};

                /// <inheritdoc />
                /// <returns>Nothing.</returns>
                void IDisposable.{|#3:Dispose|}() { }

                bool IEquatable<Part>.{|#4:Equals|}(Part? other) => false;
            }
            """ + Contracts;

        await Verify(
            source,
            Error(0, "Attach", "remove `<summary>`"),
            Error(1, "Name", "remove `<summary>`"),
            Error(2, "Changed", "remove `<summary>`"),
            Error(3, "IDisposable.Dispose", "remove `<returns>`"),
            Error(4, "IEquatable<Part>.Equals", "add `/// <inheritdoc />`"));
    }

    [Fact]
    [Trait("Requirement", "ADR08-5")]
    public async Task Overrides_of_object_members_in_structs_and_records_are_checked()
    {
        const string source = """
            public readonly struct Pair
            {
                /// <summary>Hashes the pair.</summary>
                public override int {|#0:GetHashCode|}() => 0;

                public override bool {|#1:Equals|}(object? obj) => false;
            }

            public sealed record Entry(string Name)
            {
                /// <summary>Formats the entry.</summary>
                /// <returns>The name.</returns>
                public override string {|#2:ToString|}() => Name;

                /// <inheritdoc />
                public bool Equals(Entry? other) => other is not null && other.Name == Name;

                /// <inheritdoc />
                public override int GetHashCode() => Name.GetHashCode();
            }
            """;

        await Verify(
            source,
            Error(0, "GetHashCode", "remove `<summary>`"),
            Error(1, "Equals", "add `/// <inheritdoc />`"),
            Error(2, "ToString", "remove `<summary>`"));
    }

    [Fact]
    [Trait("Requirement", "ADR08-5")]
    public async Task A_tree_parsed_without_documentation_comments_is_not_checked()
    {
        const string source = """
            public sealed class Part : Base
            {
                /// <summary>Renders the part.</summary>
                public override void Render() { }
            }
            """ + Contracts;

        await Verify(source, DocumentationMode.None, skipSuppressionCheck: false);
    }

    [Fact]
    [Trait("Requirement", "ADR08-5")]
    public async Task A_pragma_suppresses_the_rule()
    {
        const string source = """
            public sealed class Part : Base
            {
            #pragma warning disable BLZX2007 // the inherited summary is wrong for this override
                /// <summary>Renders nothing.</summary>
                public override void Render() { }
            #pragma warning restore BLZX2007

                /// <summary>Gets one.</summary>
                public override int {|#0:Size|} => 1;
            }
            """ + Contracts;

        // The testing SDK wraps the source in its own pragma to check suppression, which the source's restore would undo.
        await Verify(source, documentationMode: null, skipSuppressionCheck: true, Error(0, "Size", "remove `<summary>`"));
    }

    private static DiagnosticResult Error(int location, string member, string problem)
    {
        return new DiagnosticResult(InheritDocAnalyzer.DiagnosticId, DiagnosticSeverity.Error)
            .WithLocation(location)
            .WithArguments(member, problem);
    }

    private static Task Verify(string source, params DiagnosticResult[] expected) => Verify(source, documentationMode: null, skipSuppressionCheck: false, expected);

    private static async Task Verify(string source, DocumentationMode? documentationMode, bool skipSuppressionCheck, params DiagnosticResult[] expected)
    {
        CSharpAnalyzerTest<InheritDocAnalyzer, DefaultVerifier> test = new()
        {
            TestCode = source,
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100,
        };

        // The library builds with documentation comments diagnosed; a test pins the mode instead of relying on the default.
        DocumentationMode mode = documentationMode ?? DocumentationMode.Diagnose;
        test.SolutionTransforms.Add((solution, projectId) =>
        {
            CSharpParseOptions options = (CSharpParseOptions)solution.GetProject(projectId)!.ParseOptions!;
            return solution.WithProjectParseOptions(projectId, options.WithDocumentationMode(mode));
        });

        if (skipSuppressionCheck)
        {
            test.TestBehaviors |= TestBehaviors.SkipSuppressionCheck;
        }

        test.ExpectedDiagnostics.AddRange(expected);

        await test.RunAsync(TestContext.Current.CancellationToken);
    }
}

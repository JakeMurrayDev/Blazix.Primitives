using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Blazix.Primitives.Analyzers;

// ADR 08 §5, Amendments 2026-10-20 (documentation comments on overriding and implementing members; the clarification of
// issue 42 that allows <exception> beside <remarks>)
/// <summary>
/// Requires a member that overrides a base member or implements an interface member to document itself with
/// <c>&lt;inheritdoc /&gt;</c>.
/// </summary>
/// <remarks>
/// The contract is written once, on the member that declares it. The overriding or implementing member's documentation
/// comment is <c>&lt;inheritdoc /&gt;</c>, optionally followed by <c>&lt;remarks&gt;</c> for behavior it adds and
/// <c>&lt;exception&gt;</c> for exceptions it adds, and nothing else. A syntax tree parsed without documentation comments
/// is not checked.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InheritDocAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Represents the id of the diagnostic this analyzer reports, <c>BLZX2007</c>.</summary>
    public const string DiagnosticId = "BLZX2007";

    // ADR 08 Amendments 2026-10-20
    private static readonly DiagnosticDescriptor s_rule = new(
        DiagnosticId,
        "An overriding or implementing member inherits its documentation",
        "Blazix: `{0}` overrides or implements an inherited member, so its documentation comment is `<inheritdoc />` with at most `<remarks>` and `<exception>` added; {1}",
        "Blazix.Primitives",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A member that overrides a base member or implements an interface member, implicitly or explicitly, inherits the documentation of the member it overrides or implements. Its comment is `/// <inheritdoc />`, optionally with `<remarks>` for added behavior and `<exception>` for added exceptions; `<summary>`, `<param>`, `<returns>`, `<value>` and every other element restate the contract and are rejected.");

    private static readonly ImmutableHashSet<string> s_allowedElements = ImmutableHashSet.Create(StringComparer.Ordinal, "inheritdoc", "remarks", "exception");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        // The tests-first stub: it registers nothing, so every violation test is red until the implementation lands.
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
    }
}

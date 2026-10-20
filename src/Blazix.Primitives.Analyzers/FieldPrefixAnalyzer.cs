using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Blazix.Primitives.Analyzers;

// ADR 08 §5 as amended 2026-10-20. IDE1006 (.editorconfig) already rejects `_x` and `m_x` on private fields and on
// constants; this rule covers the fields it does not reach: every non-private field that is not a constant.
/// <summary>
/// Rejects the <c>_</c> and <c>m_</c> prefixes on fields that are not private.
/// </summary>
/// <remarks>
/// Private fields and constants are checked by the naming rules of the repository's <c>.editorconfig</c> (IDE1006): private
/// instance fields are camelCase with no prefix, private static fields are <c>s_camelCase</c> and constants are PascalCase.
/// The naming rules apply to private fields only, so this analyzer reports an internal, protected or public field whose
/// name starts with <c>_</c> or <c>m_</c>.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FieldPrefixAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Represents the id of the diagnostic this analyzer reports, <c>BLZX2004</c>.</summary>
    public const string DiagnosticId = "BLZX2004";

    // ADR 08 §5
    private static readonly DiagnosticDescriptor s_rule = new(
        DiagnosticId,
        "A field name carries no `_` or `m_` prefix",
        "Blazix: field `{0}` starts with `{1}`; no field carries a `_` or `m_` prefix",
        "Blazix.Primitives",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "No field of the library starts with `_` or `m_`. The naming rules of the repository's .editorconfig check private fields and constants; this rule checks the internal, protected and public fields they do not reach.");

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

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Blazix.Primitives.Analyzers;

// ADR 05 and ADR 08, Amendments 2026-10-20 (union-binding spike): a union-typed [Parameter] is nullable
/// <summary>
/// Requires a component parameter whose type is a union to be declared nullable.
/// </summary>
/// <remarks>
/// The default value of a union holds no case, and a <c>switch</c> over its case types throws at run time, so a parameter
/// that was not supplied must read as <see langword="null"/>: a union-typed <c>[Parameter]</c> or cascading parameter is
/// declared <c>U?</c>.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NullableUnionParameterAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Represents the id of the diagnostic this analyzer reports, <c>BLZX2006</c>.</summary>
    public const string DiagnosticId = "BLZX2006";

    // ADR 05 Amendments 2026-10-20
    private static readonly DiagnosticDescriptor s_rule = new(
        DiagnosticId,
        "A union-typed parameter is nullable",
        "Blazix: parameter `{0}` has the union type `{1}`; declare it `{1}?`, since a default union holds no case",
        "Blazix.Primitives",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A default union holds no case and a switch over its case types throws, so a component parameter typed by a declared `union` or a `[Union]` struct is declared nullable and reads `null` when the consumer did not supply it.");

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

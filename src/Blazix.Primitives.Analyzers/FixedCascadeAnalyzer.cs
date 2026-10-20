using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Blazix.Primitives.Analyzers;

/// <summary>
/// ADR 16 rule 9: every <c>CascadingValue&lt;T&gt;</c> the library renders is <c>IsFixed = true</c>, so change reaches parts by
/// subscription only (ADR 05 §1). The library renders from C# (ADR 02 decision 4), so a cascade is an
/// <c>OpenComponent&lt;CascadingValue&lt;T&gt;&gt;</c> (or <c>OpenComponent(seq, typeof(CascadingValue&lt;T&gt;))</c>) on a
/// <c>RenderTreeBuilder</c>; between it and its <c>CloseComponent</c>, at the same nesting depth and in the same block,
/// an <c>AddComponentParameter</c> or <c>AddAttribute</c> must set <c>IsFixed</c> to the constant <c>true</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FixedCascadeAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "BLZX2001";

    private static readonly DiagnosticDescriptor s_rule = new(
        DiagnosticId,
        "A library cascade must be fixed",
        "CascadingValue<{0}> is rendered without IsFixed set to true; library cascades are fixed and change reaches parts by subscription (ADR 16 rule 9)",
        "Blazix.Primitives",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "ADR 16 rule 9: a CascadingValue whose value changes re-renders every subscriber; the library cascades fixed values and parts subscribe to the store or context they need.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
    }
}

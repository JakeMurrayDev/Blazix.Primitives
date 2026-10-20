using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Blazix.Primitives.Analyzers;

// ADR 16 rule 3
/// <summary>
/// Requires every sequence number the library passes to a <c>RenderTreeBuilder</c> to be a constant expression.
/// </summary>
/// <remarks>
/// The diff matches the frames of a render with the last render's by sequence number, so a number must stand for a place
/// in the source (a literal or a named constant), never for a count of what ran before it.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SequenceNumberAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Represents the id of the diagnostic this analyzer reports, <c>BLZX2002</c>.</summary>
    public const string DiagnosticId = "BLZX2002";

    // ADR 16 rule 3
    private static readonly DiagnosticDescriptor s_rule = new(
        DiagnosticId,
        "A render tree sequence number must be a constant",
        "Blazix: `{0}` is not a constant sequence number; use an integer literal or a named constant, never a counter or a calculation",
        "Blazix.Primitives",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Blazor diffs a render with the last one by sequence number, so a sequence number stands for a place in the source. A counter or a calculation loses that and degrades the diff; a frame re-emitted from the consumer's own fragment is the one place that keeps the consumer's number, and suppresses the rule there.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(s_rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            INamedTypeSymbol? builder = start.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder");
            if (builder is null)
            {
                return;
            }

            start.RegisterOperationAction(operation => AnalyzeInvocation(operation, builder), OperationKind.Invocation);
        });
    }

    // Every RenderTreeBuilder method that takes a sequence number names the parameter `sequence`.
    private static void AnalyzeInvocation(OperationAnalysisContext context, INamedTypeSymbol builder)
    {
        IInvocationOperation invocation = (IInvocationOperation)context.Operation;
        if (!SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, builder))
        {
            return;
        }

        foreach (IArgumentOperation argument in invocation.Arguments)
        {
            if (argument.Parameter is { Name: "sequence" } && !argument.Value.ConstantValue.HasValue)
            {
                context.ReportDiagnostic(Diagnostic.Create(s_rule, argument.Value.Syntax.GetLocation(), argument.Value.Syntax.ToString()));
            }
        }
    }
}
